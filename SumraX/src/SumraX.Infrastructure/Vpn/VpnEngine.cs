using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using NLog;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Security;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Manages Xray-core / sing-box process lifecycle, TUN or system proxy.
/// </summary>
public sealed class VpnEngine : IVpnEngine, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISettingsRepository _settings;
    private readonly IKillSwitchService? _killSwitch;
    private readonly ISystemProxyManager _systemProxy;
    private readonly ILocalizationService _loc;
    private readonly ISingBoxBootstrap _singBoxBootstrap;
    private VpnCoreKind _activeCore = VpnCoreKind.Xray;
    private bool _activeTunMode;
    private TunProvider _activeTunProvider = TunProvider.SumraXTun;
    private Process? _process;
    private Process? _tunProcess;
    private ConnectionStatus _status = new();
    private DateTime? _connectedAt;
    private CancellationTokenSource? _monitorCts;

    public VpnEngine(
        ISettingsRepository settings,
        IKillSwitchService killSwitch,
        ISystemProxyManager systemProxy,
        ILocalizationService loc,
        ISingBoxBootstrap singBoxBootstrap)
    {
        _settings = settings;
        _killSwitch = killSwitch;
        _systemProxy = systemProxy;
        _loc = loc;
        _singBoxBootstrap = singBoxBootstrap;
    }

    public ConnectionState State => _status.State;
    public ConnectionStatus CurrentStatus => CloneStatus();
    public bool IsTunMode => _activeTunMode;
    public event EventHandler<ConnectionStatus>? StatusChanged;

    public async Task ConnectAsync(ServerProfile server, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync(cancellationToken);
        UpdateStatus(ConnectionState.Connecting, server.Name);

        if (!CoreSelector.IsSupported(server.Protocol))
        {
            UpdateStatus(ConnectionState.Error, server.Name, $"Unsupported protocol: {server.Protocol}");
            return;
        }

        try
        {
            var settings = await _settings.GetAsync();
            var connected = await TryConnectInternalAsync(server, settings, forceProxyMode: false, cancellationToken);
            if (!connected && settings.UseTunMode && TunSupport.IsAvailable(settings))
            {
                Logger.Warn("TUN connect failed, retrying with system proxy");
                connected = await TryConnectInternalAsync(server, settings, forceProxyMode: true, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Warn("Connect cancelled or timed out for {Server}", server.Name);
            await DisconnectAsync(CancellationToken.None);
            UpdateStatus(ConnectionState.Error, server.Name, _loc.Get("TunConnectTimeout"));
        }
    }

    private async Task<bool> TryConnectInternalAsync(
        ServerProfile server,
        AppSettings settings,
        bool forceProxyMode,
        CancellationToken cancellationToken)
    {
        var core = CoreSelector.Select(server, settings, forceProxyMode);
        _activeCore = core;
        _activeTunMode = CoreSelector.UseTunMode(settings, forceProxyMode);
        _activeTunProvider = settings.TunProvider;
        var corePath = CoreSelector.GetCorePath(core);
        var configPath = CoreSelector.GetConfigPath(core);

        if (core == VpnCoreKind.SingBox || (_activeTunMode && TunProviderSupport.UsesSingBox(settings.TunProvider)))
        {
            try
            {
                await _singBoxBootstrap.EnsureAsync(cancellationToken: cancellationToken);
                if (core == VpnCoreKind.SingBox)
                {
                    corePath = CoreSelector.GetCorePath(core);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus(ConnectionState.Error, server.Name, _loc.Format("SingBoxDownloadFailed", ex.Message));
                return false;
            }
        }

        if (!File.Exists(corePath))
        {
            var msg = core == VpnCoreKind.SingBox ? _loc.Get("SingBoxNotFound") : _loc.Get("XrayNotFound");
            UpdateStatus(ConnectionState.Error, server.Name, msg);
            return false;
        }

        if (_activeTunMode && TunSupport.GetMissingReason(settings) is { } tunReason)
        {
            UpdateStatus(ConnectionState.Error, server.Name, tunReason);
            return false;
        }

        try
        {
            await EnsureLocalPortsAvailableAsync(settings, cancellationToken);

            string configJson;
            try
            {
                configJson = CoreSelector.BuildConfig(server, settings, forceProxyMode);
            }
            catch (NotSupportedException ex)
            {
                UpdateStatus(ConnectionState.Error, server.Name, ex.Message);
                return false;
            }

            await File.WriteAllTextAsync(configPath, configJson, cancellationToken);
            await KillOrphanCoreProcessesAsync();
            await TunProviderSupport.KillAllOrphansAsync();
            if (_activeTunMode)
            {
                await TunAdapterCleanup.CleanupAllWintunAsync();
                await Task.Delay(300, cancellationToken);
            }
            else
            {
                await TunAdapterCleanup.CleanupOrphanedAsync();
            }

            TrustedBinaryGuard.EnsureTrustedCore(corePath);

            var stderr = new StringBuilder();
            var stdout = new StringBuilder();
            var args = core == VpnCoreKind.SingBox
                ? $"run -c \"{configPath}\" -D \"{Path.GetDirectoryName(corePath)!}\""
                : $"run -c \"{configPath}\"";

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = corePath,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(corePath)!,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            if (core == VpnCoreKind.SingBox)
            {
                _process.StartInfo.Environment["ENABLE_DEPRECATED_LEGACY_DNS_SERVERS"] = "true";
            }

            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (stderr)
                    {
                        stderr.AppendLine(e.Data);
                    }
                }
            };
            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (stdout)
                    {
                        stdout.AppendLine(e.Data);
                    }
                }
            };

            if (!_process.Start())
            {
                UpdateStatus(ConnectionState.Error, server.Name, _loc.Get("XrayStartFailed"));
                return false;
            }

            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();

            for (var i = 0; i < 30 && !_process.HasExited; i++)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (_process.HasExited)
            {
                await Task.Delay(500, cancellationToken);
                string err;
                lock (stderr)
                {
                    err = stderr.ToString().Trim();
                }

                if (string.IsNullOrWhiteSpace(err))
                {
                    lock (stdout)
                    {
                        err = stdout.ToString().Trim();
                    }
                }

                Logger.Error("Core exited early ({Core}, code={Code}): {Error}",
                    core, _process.ExitCode, string.IsNullOrWhiteSpace(err) ? "(no output)" : err);
                UpdateStatus(ConnectionState.Error, server.Name, FormatCoreError(err, settings));
                _process.Dispose();
                _process = null;
                return false;
            }

            if (_activeTunMode)
            {
                if (!await StartTunLayerAsync(server, settings, cancellationToken))
                {
                    return false;
                }
            }

            await ApplySystemProxyAsync(_systemProxy, settings, _activeTunMode);

            _connectedAt = DateTime.UtcNow;
            server.LastUsedAt = DateTime.UtcNow;
            StartMonitor(server, forceProxyMode);
            UpdateStatus(ConnectionState.Connected, server.Name);
            Logger.Info("Connected to {Server} (TUN={Tun}, provider={Provider}, core={Core})",
                server.Name, _activeTunMode, _activeTunProvider, core);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Connection failed");
            UpdateStatus(ConnectionState.Error, server.Name, ex.Message);
            return false;
        }
    }

    private bool _killSwitchByDrop;

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _monitorCts?.Cancel();
        _monitorCts = null;

        if (_tunProcess is { HasExited: false })
        {
            try
            {
                _tunProcess.Kill(entireProcessTree: true);
                await _tunProcess.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error stopping TUN process");
            }
        }

        _tunProcess?.Dispose();
        _tunProcess = null;

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error stopping core process");
            }
        }

        _process?.Dispose();
        _process = null;
        _connectedAt = null;
        await KillOrphanCoreProcessesAsync();
        await TunProviderSupport.KillAllOrphansAsync();
        await TunAdapterCleanup.CleanupOrphanedAsync();

        await _systemProxy.ClearProxyAsync();

        _activeCore = VpnCoreKind.Xray;
        _activeTunMode = false;

        if (_killSwitchByDrop)
        {
            _killSwitchByDrop = false;
        }
        else
        {
            var settings = await _settings.GetAsync();
            if (!settings.KillSwitch && _killSwitch?.IsEnabled == true)
            {
                await _killSwitch.DisableAsync();
            }
        }

        UpdateStatus(ConnectionState.Disconnected);
    }

    public Task<bool> IsCoreRunningAsync() =>
        Task.FromResult(_process is { HasExited: false });

    private void StartMonitor(ServerProfile server, bool forceProxyMode)
    {
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if ((_process == null || _process.HasExited) ||
                    (_activeTunMode && (_tunProcess == null || _tunProcess.HasExited)))
                {
                    var settings = await _settings.GetAsync();
                    if (settings.AutoReconnect && _status.State == ConnectionState.Connected)
                    {
                        UpdateStatus(ConnectionState.Reconnecting, server.Name);
                        await ConnectAsync(server, token);
                    }
                    else
                    {
                        await _systemProxy.ClearProxyAsync();

                        if (settings.KillSwitch && _killSwitch != null && !token.IsCancellationRequested)
                        {
                            _killSwitchByDrop = true;
                            await _killSwitch.EnableAsync();
                        }

                        UpdateStatus(ConnectionState.Disconnected, server.Name, _loc.Get("ConnectionLost"));
                    }

                    break;
                }

                if (_connectedAt.HasValue)
                {
                    _status.ConnectedDuration = DateTime.UtcNow - _connectedAt.Value;
                    RaiseStatus();
                }

                await Task.Delay(1000, token);
            }
        }, token);
    }

    private void UpdateStatus(ConnectionState state, string? serverName = null, string? error = null)
    {
        _status.State = state;
        _status.ServerName = serverName;
        _status.ErrorMessage = error;
        if (state == ConnectionState.Connected && _connectedAt.HasValue)
        {
            _status.ConnectedDuration = DateTime.UtcNow - _connectedAt.Value;
        }

        try
        {
            RaiseStatus();
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Status notification failed (UI handler threw)");
        }
    }

    private void RaiseStatus() => StatusChanged?.Invoke(this, CloneStatus());

    private ConnectionStatus CloneStatus() => new()
    {
        State = _status.State,
        PublicIp = _status.PublicIp,
        Country = _status.Country,
        CountryCode = _status.CountryCode,
        PingMs = _status.PingMs,
        DownloadMbps = _status.DownloadMbps,
        UploadMbps = _status.UploadMbps,
        ConnectedDuration = _status.ConnectedDuration,
        ServerName = _status.ServerName,
        ErrorMessage = _status.ErrorMessage
    };

    private string FormatTunError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return _loc.Get("TunStartFailed");
        }

        if (stderr.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
        {
            return _loc.Get("TunAdminRequired");
        }

        if (IsAdapterConflict(stderr))
        {
            return _loc.Get("TunAdapterBlocked");
        }

        var line = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(line) ? stderr : line;
    }

    private async Task<bool> StartTunLayerAsync(
        ServerProfile server,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (attempt > 0)
            {
                Logger.Info("Retrying TUN after adapter cleanup");
                await StopTunProcessAsync();
                await TunAdapterCleanup.CleanupAllWintunAsync();
                await Task.Delay(500, cancellationToken);
            }

            var provider = settings.TunProvider;
            var tunStderr = new StringBuilder();

            try
            {
                if (provider == TunProvider.Tun2Proxy)
                {
                    _tunProcess = Tun2ProxyManager.Start(settings, server, tunStderr);
                }
                else
                {
                    await SingBoxTunManager.WriteConfigAsync(settings, cancellationToken);
                    _tunProcess = SingBoxTunManager.Start(settings, tunStderr);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "TUN start failed ({Provider})", provider);
                await StopCoreProcessAsync();
                UpdateStatus(ConnectionState.Error, server.Name, ex.Message);
                return false;
            }

            if (!await SingBoxTunManager.WaitForInterfaceAsync(_tunProcess, cancellationToken))
            {
                await Task.Delay(300, cancellationToken);
                var tunErr = tunStderr.ToString().Trim();
                Logger.Error("TUN interface failed ({Provider}): {Error}", provider, tunErr);
                await StopTunProcessAsync();

                if (attempt == 0 && IsAdapterConflict(tunErr))
                {
                    continue;
                }

                await StopCoreProcessAsync();
                UpdateStatus(ConnectionState.Error, server.Name, FormatTunError(tunErr));
                return false;
            }

            if (_tunProcess.HasExited)
            {
                await Task.Delay(300, cancellationToken);
                var tunErr = tunStderr.ToString().Trim();
                Logger.Error("TUN exited ({Provider}): {Error}", provider, tunErr);
                await StopTunProcessAsync();

                if (attempt == 0 && IsAdapterConflict(tunErr))
                {
                    continue;
                }

                await StopCoreProcessAsync();
                UpdateStatus(ConnectionState.Error, server.Name, FormatTunError(tunErr));
                _tunProcess.Dispose();
                _tunProcess = null;
                return false;
            }

            if (TunProviderSupport.UsesSingBox(provider))
            {
                SingBoxTunManager.ConfigureInterfaceDns(settings);
            }

            await TunAdapterCleanup.CleanupOrphanedAsync(TunAdapterCleanup.ActiveInterfaceName);
            return true;
        }

        await StopCoreProcessAsync();
        UpdateStatus(ConnectionState.Error, server.Name, _loc.Get("TunAdapterBlocked"));
        return false;
    }

    private static bool IsAdapterConflict(string stderr) =>
        stderr.Contains("0x000000B7", StringComparison.OrdinalIgnoreCase) ||
        stderr.Contains("0xC0000035", StringComparison.OrdinalIgnoreCase) ||
        stderr.Contains("WintunCreateAdapter", StringComparison.OrdinalIgnoreCase) ||
        stderr.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
        stderr.Contains("уже существует", StringComparison.OrdinalIgnoreCase);

    private static async Task ApplySystemProxyAsync(ISystemProxyManager systemProxy, AppSettings settings, bool tunMode)
    {
        if (!settings.UseSystemProxy)
        {
            return;
        }

        if (tunMode)
        {
            await systemProxy.SetSocksProxyAsync("127.0.0.1", settings.LocalSocksPort, settings.BypassLocalForSystemProxy);
        }
        else
        {
            await systemProxy.SetProxyAsync("127.0.0.1", settings.LocalHttpPort, settings.BypassLocalForSystemProxy);
        }
    }

    private async Task StopTunProcessAsync()
    {
        if (_tunProcess is not { HasExited: false })
        {
            return;
        }

        try
        {
            _tunProcess.Kill(entireProcessTree: true);
            await _tunProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Error stopping TUN process");
        }

        _tunProcess.Dispose();
        _tunProcess = null;
    }

    private async Task StopCoreProcessAsync()
    {
        if (_process is not { HasExited: false })
        {
            return;
        }

        try
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Error stopping core process");
        }

        _process.Dispose();
        _process = null;
    }

    private string FormatCoreError(string stderr, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return _loc.Get("XrayExited");
        }

        if (stderr.Contains("bind", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase))
        {
            return _loc.Format("XrayPortInUse", settings.LocalSocksPort, settings.LocalHttpPort);
        }

        if (stderr.Contains("wintun", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
        {
            return _loc.Get("TunAdminRequired");
        }

        var line = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(l => !l.StartsWith("Xray ", StringComparison.Ordinal) &&
                                !l.StartsWith("A unified", StringComparison.Ordinal) &&
                                !l.StartsWith("sing-box", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(line) ? stderr : line;
    }

    private async Task EnsureLocalPortsAvailableAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await KillOrphanCoreProcessesAsync();
        await TunProviderSupport.KillAllOrphansAsync();

        // Saved settings can have socks==http (old bug). Fix before anything else.
        if (settings.LocalSocksPort == settings.LocalHttpPort ||
            settings.LocalSocksPort < 1024 ||
            settings.LocalHttpPort < 1024)
        {
            settings.LocalSocksPort = settings.LocalSocksPort is >= 1024 and <= 65535
                ? settings.LocalSocksPort
                : 10808;
            settings.LocalHttpPort = settings.LocalSocksPort == 10808 ? 10809 : settings.LocalSocksPort + 1;
            if (settings.LocalHttpPort == settings.LocalSocksPort || settings.LocalHttpPort > 65535)
            {
                settings.LocalHttpPort = FindFreePort(10809, excludePort: settings.LocalSocksPort);
            }

            await _settings.SaveAsync(settings);
            Logger.Warn("Normalized proxy ports to {Socks}/{Http}", settings.LocalSocksPort, settings.LocalHttpPort);
        }

        for (var attempt = 0; attempt < 15; attempt++)
        {
            if (!IsPortInUse(settings.LocalSocksPort) &&
                !IsPortInUse(settings.LocalHttpPort) &&
                settings.LocalSocksPort != settings.LocalHttpPort)
            {
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        if (IsPortInUse(settings.LocalSocksPort) ||
            IsPortInUse(settings.LocalHttpPort) ||
            settings.LocalSocksPort == settings.LocalHttpPort)
        {
            settings.LocalSocksPort = FindFreePort(settings.LocalSocksPort);
            settings.LocalHttpPort = FindFreePort(Math.Max(settings.LocalHttpPort, settings.LocalSocksPort + 1),
                excludePort: settings.LocalSocksPort);
            if (settings.LocalHttpPort == settings.LocalSocksPort)
            {
                settings.LocalHttpPort = FindFreePort(settings.LocalSocksPort + 1, excludePort: settings.LocalSocksPort);
            }

            await _settings.SaveAsync(settings);
            Logger.Warn("Local proxy ports were busy; switched to {Socks}/{Http}", settings.LocalSocksPort, settings.LocalHttpPort);
        }
    }

    private static bool IsPortInUse(int port)
    {
        var props = IPGlobalProperties.GetIPGlobalProperties();
        return props.GetActiveTcpListeners().Any(ep => ep.Port == port);
    }

    private static int AllocateEphemeralLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static int FindFreePort(int preferred, int excludePort = -1)
    {
        var start = Math.Clamp(preferred, 1024, 65000);
        for (var port = start; port < start + 200; port++)
        {
            if (port == excludePort)
            {
                continue;
            }

            if (!IsPortInUse(port))
            {
                return port;
            }
        }

        var ephemeral = AllocateEphemeralLoopbackPort();
        if (ephemeral == excludePort)
        {
            ephemeral = AllocateEphemeralLoopbackPort();
        }

        return ephemeral;
    }

    private static async Task KillOrphanCoreProcessesAsync()
    {
        await KillOrphanProcessAsync(AppPaths.XrayPath, "xray");
        await KillOrphanProcessAsync(AppPaths.SingBoxPath, "sing-box");
    }

    private static async Task KillOrphanProcessAsync(string ourBinaryPath, string _)
    {
        if (!File.Exists(ourBinaryPath))
        {
            return;
        }

        var ourPath = Path.GetFullPath(ourBinaryPath);
        var exeName = Path.GetFileNameWithoutExtension(ourBinaryPath);
        foreach (var proc in Process.GetProcessesByName(exeName))
        {
            try
            {
                var path = proc.MainModule?.FileName;
                if (path != null &&
                    string.Equals(Path.GetFullPath(path), ourPath, StringComparison.OrdinalIgnoreCase) &&
                    !proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                }
            }
            catch
            {
                // ignore processes we cannot access
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _monitorCts?.Cancel();
        _process?.Dispose();
        _tunProcess?.Dispose();
    }
}
