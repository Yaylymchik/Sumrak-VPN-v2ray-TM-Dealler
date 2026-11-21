using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Core.Util;

namespace SumraX.Infrastructure.Data;

public sealed class SqliteServerRepository : IServerRepository
{
    private static readonly SemaphoreSlim DbLock = new(1, 1);
    private static int _walInitialized;

    private readonly ISecureStorage _secureStorage;

    public SqliteServerRepository(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
        EnsureSchema();
    }

    public async Task<IReadOnlyList<ServerProfile>> GetAllAsync()
    {
        await DbLock.WaitAsync();
        try
        {
            await using var conn = Open();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT payload FROM servers ORDER BY is_favorite DESC, name ASC";
            var list = new List<ServerProfile>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var json = _secureStorage.Decrypt(reader.GetString(0));
                var item = JsonSerializer.Deserialize<ServerProfile>(json);
                if (item != null)
                {
                    list.Add(item);
                }
            }

            return list;
        }
        finally
        {
            DbLock.Release();
        }
    }

    public async Task<ServerProfile?> GetByIdAsync(string id)
    {
        await DbLock.WaitAsync();
        try
        {
            await using var conn = Open();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT payload FROM servers WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            var result = await cmd.ExecuteScalarAsync();
            if (result is not string encrypted)
            {
                return null;
            }

            return JsonSerializer.Deserialize<ServerProfile>(_secureStorage.Decrypt(encrypted));
        }
        finally
        {
            DbLock.Release();
        }
    }

    public async Task SaveAsync(ServerProfile server)
    {
        await DbLock.WaitAsync();
        try
        {
            ServerDisplayName.ApplyToProfile(server);
            var payload = _secureStorage.Encrypt(JsonSerializer.Serialize(server));
            await using var conn = Open();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO servers(id, name, is_favorite, payload)
                VALUES($id, $name, $favorite, $payload)
                ON CONFLICT(id) DO UPDATE SET name=$name, is_favorite=$favorite, payload=$payload
                """;
            cmd.Parameters.AddWithValue("$id", server.Id);
            cmd.Parameters.AddWithValue("$name", server.Name);
            cmd.Parameters.AddWithValue("$favorite", server.IsFavorite ? 1 : 0);
            cmd.Parameters.AddWithValue("$payload", payload);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            DbLock.Release();
        }
    }

    public async Task<int> SaveManyAsync(IEnumerable<ServerProfile> servers)
    {
        await DbLock.WaitAsync();
        try
        {
            var count = 0;
            await using var conn = Open();
            await using var tx = await conn.BeginTransactionAsync();
            foreach (var server in servers)
            {
                ServerDisplayName.ApplyToProfile(server);
                var payload = _secureStorage.Encrypt(JsonSerializer.Serialize(server));
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = (SqliteTransaction)tx;
                cmd.CommandText = """
                    INSERT INTO servers(id, name, is_favorite, payload)
                    VALUES($id, $name, $favorite, $payload)
                    ON CONFLICT(id) DO UPDATE SET name=$name, is_favorite=$favorite, payload=$payload
                    """;
                cmd.Parameters.AddWithValue("$id", server.Id);
                cmd.Parameters.AddWithValue("$name", server.Name);
                cmd.Parameters.AddWithValue("$favorite", server.IsFavorite ? 1 : 0);
                cmd.Parameters.AddWithValue("$payload", payload);
                await cmd.ExecuteNonQueryAsync();
                count++;
            }

            await tx.CommitAsync();
            return count;
        }
        finally
        {
            DbLock.Release();
        }
    }

    public async Task DeleteAsync(string id)
    {
        await DbLock.WaitAsync();
        try
        {
            await using var conn = Open();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM servers WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            DbLock.Release();
        }
    }

    public async Task SetFavoriteAsync(string id, bool favorite)
    {
        var server = await GetByIdAsync(id);
        if (server == null)
        {
            return;
        }

        server.IsFavorite = favorite;
        await SaveAsync(server);
    }

    public async Task UpdateMetricsAsync(string id, int pingMs, double load, double download, double upload)
    {
        var server = await GetByIdAsync(id);
        if (server == null)
        {
            return;
        }

        server.PingMs = pingMs;
        server.LoadPercent = load;
        server.DownloadMbps = download;
        server.UploadMbps = upload;
        await SaveAsync(server);
    }

    private static SqliteConnection Open()
    {
        var conn = new SqliteConnection(
            $"Data Source={AppPaths.DatabasePath};Mode=ReadWriteCreate;Cache=Shared;Pooling=True");
        conn.Open();
        if (Interlocked.Exchange(ref _walInitialized, 1) == 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            cmd.ExecuteNonQuery();
        }

        return conn;
    }

    private void EnsureSchema()
    {
        AppPaths.EnsureDirectories();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS servers(
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                payload TEXT NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }
}

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ISecureStorage _secureStorage;
    private AppSettings? _cache;
    private bool _loadDegraded;

    public JsonSettingsRepository(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public async Task<AppSettings> GetAsync()
    {
        if (_cache != null)
        {
            return _cache;
        }

        await FileLock.WaitAsync();
        try
        {
            if (_cache != null)
            {
                return _cache;
            }

            AppPaths.EnsureDirectories();
            if (!File.Exists(AppPaths.SettingsPath))
            {
                _cache = new AppSettings();
                await WriteFileAsync(_cache);
                _loadDegraded = false;
                return _cache;
            }

            try
            {
                _cache = await ReadSettingsFileAsync(AppPaths.SettingsPath);
                _loadDegraded = false;
                return _cache;
            }
            catch
            {
                var bak = AppPaths.SettingsPath + ".bak";
                if (File.Exists(bak))
                {
                    try
                    {
                        _cache = await ReadSettingsFileAsync(bak);
                        _loadDegraded = false;
                        // Restore primary file from readable backup.
                        try
                        {
                            await WriteFileAsync(_cache);
                        }
                        catch
                        {
                            // Cache is still valid even if restore write fails.
                        }

                        return _cache;
                    }
                    catch
                    {
                        // fall through
                    }
                }

                // Keep the unreadable file on disk until an explicit Save quarantines it.
                _loadDegraded = true;
                _cache = new AppSettings();
                return _cache;
            }
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await FileLock.WaitAsync();
        try
        {
            AppPaths.EnsureDirectories();
            if (_loadDegraded)
            {
                QuarantineUnreadable(AppPaths.SettingsPath);
                QuarantineUnreadable(AppPaths.SettingsPath + ".bak");
                _loadDegraded = false;
            }

            _cache = settings;
            await WriteFileAsync(settings);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private static void QuarantineUnreadable(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var dest = path + ".corrupt-" + DateTime.UtcNow.Ticks;
            File.Move(path, dest);
        }
        catch
        {
            try
            {
                File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks, true);
            }
            catch
            {
                // ignore — Save will overwrite
            }
        }
    }

    private async Task<AppSettings> ReadSettingsFileAsync(string path)
    {
        var raw = (await File.ReadAllTextAsync(path)).Trim();
        var json = TryDecryptOrPlain(raw);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    private string TryDecryptOrPlain(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "{}";
        }

        try
        {
            return _secureStorage.Decrypt(raw);
        }
        catch
        {
            if (raw.StartsWith('{'))
            {
                return raw;
            }

            throw;
        }
    }

    private async Task WriteFileAsync(AppSettings settings)
    {
        var encrypted = _secureStorage.Encrypt(JsonSerializer.Serialize(settings, JsonOptions));
        var temp = AppPaths.SettingsPath + ".tmp";
        var bak = AppPaths.SettingsPath + ".bak";
        await File.WriteAllTextAsync(temp, encrypted);
        if (File.Exists(AppPaths.SettingsPath))
        {
            try
            {
                File.Replace(temp, AppPaths.SettingsPath, bak, true);
            }
            catch (IOException)
            {
                File.Copy(AppPaths.SettingsPath, bak, true);
                File.Delete(AppPaths.SettingsPath);
                File.Move(temp, AppPaths.SettingsPath);
            }
        }
        else
        {
            File.Move(temp, AppPaths.SettingsPath);
        }
    }

    public void ClearCache()
    {
        _cache = null;
        _loadDegraded = false;
    }
}

public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly ISecureStorage _secureStorage;
    private UserProfile? _cache;

    public UserProfileRepository(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public async Task<UserProfile> GetAsync()
    {
        if (_cache != null)
        {
            return _cache;
        }

        var value = await _secureStorage.LoadSecureAsync("user_profile");
        _cache = value == null
            ? new UserProfile()
            : JsonSerializer.Deserialize<UserProfile>(value) ?? new UserProfile();
        return _cache;
    }

    public async Task SaveAsync(UserProfile profile)
    {
        _cache = profile;
        await _secureStorage.SaveSecureAsync("user_profile", JsonSerializer.Serialize(profile));
    }
}
