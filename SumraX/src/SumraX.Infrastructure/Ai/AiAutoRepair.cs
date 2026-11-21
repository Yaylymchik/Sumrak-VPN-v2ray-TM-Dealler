using System.Text;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;

namespace SumraX.Infrastructure.Ai;

internal sealed class AiAutoRepair
{
    private readonly IVpnEngine _vpn;
    private readonly IServerRepository _servers;
    private readonly ISettingsRepository _settings;
    private readonly IXrayBootstrap _bootstrap;
    private readonly ISpeedTestService _speedTest;
    private readonly ILocalizationService _loc;

    public AiAutoRepair(
        IVpnEngine vpn,
        IServerRepository servers,
        ISettingsRepository settings,
        IXrayBootstrap bootstrap,
        ISpeedTestService speedTest,
        ILocalizationService loc)
    {
        _vpn = vpn;
        _servers = servers;
        _settings = settings;
        _bootstrap = bootstrap;
        _speedTest = speedTest;
        _loc = loc;
    }

    public async Task<(bool Success, string Report)> RunAsync(CancellationToken cancellationToken = default)
    {
        var actions = new List<string>();
        actions.Add(_loc.Get("AiFixRunning"));

        if (!File.Exists(AppPaths.XrayPath))
        {
            try
            {
                await _bootstrap.EnsureAsync(null, cancellationToken);
                actions.Add(_loc.Get("AiFixXrayInstalled"));
            }
            catch (Exception ex)
            {
                actions.Add(_loc.Format("AiFixXrayFailed", ex.Message));
                return (false, FormatReport(actions));
            }
        }
        else
        {
            actions.Add(_loc.Get("AiFixXrayOk"));
        }

        var servers = (await _servers.GetAllAsync()).ToList();
        if (servers.Count == 0)
        {
            actions.Add(_loc.Get("AiFixNoServers"));
            return (false, FormatReport(actions));
        }

        var settings = await _settings.GetAsync();
        var server = await ResolveServerAsync(servers, settings);
        if (string.IsNullOrWhiteSpace(settings.LastSelectedServerId) ||
            settings.LastSelectedServerId != server.Id)
        {
            settings.LastSelectedServerId = server.Id;
            await _settings.SaveAsync(settings);
            actions.Add(_loc.Format("AiFixServerSelected", server.Name));
        }

        try
        {
            var json = XrayConfigBuilder.Build(server, settings);
            AppPaths.EnsureDirectories();
            await File.WriteAllTextAsync(AppPaths.ConfigPath, json, cancellationToken);
            actions.Add(_loc.Get("AiFixConfigOk"));
        }
        catch (Exception ex)
        {
            actions.Add(_loc.Format("AiFixConfigFailed", ex.Message));
            return (false, FormatReport(actions));
        }

        var best = await FindFastestServerAsync(servers, cancellationToken);
        if (best != null && best.Id != server.Id)
        {
            server = best;
            settings.LastSelectedServerId = server.Id;
            await _settings.SaveAsync(settings);
            actions.Add(_loc.Format("AiFixSwitchedServer", server.Name, server.PingMs));
        }

        var state = _vpn.CurrentStatus.State;
        if (state is ConnectionState.Error or ConnectionState.Disconnected)
        {
            actions.Add(_loc.Get("AiFixReconnecting"));
            await _vpn.ConnectAsync(server, cancellationToken);
            await Task.Delay(1200, cancellationToken);
        }
        else if (state == ConnectionState.Connected)
        {
            actions.Add(_loc.Get("AiFixAlreadyConnected"));
        }

        var after = _vpn.CurrentStatus;
        if (after.State == ConnectionState.Connected)
        {
            actions.Add(_loc.Format("AiFixReconnectOk", after.ServerName ?? server.Name));
            return (true, FormatReport(actions));
        }

        actions.Add(_loc.Format("AiFixReconnectFailed",
            string.IsNullOrWhiteSpace(after.ErrorMessage) ? _loc.Get("AiStateDisconnected") : after.ErrorMessage));

        var logErrors = ReadRecentLogErrors();
        if (!string.IsNullOrWhiteSpace(logErrors))
        {
            actions.Add(_loc.Format("AiLogTail", logErrors));
        }

        return (false, FormatReport(actions));
    }

    private async Task<ServerProfile> ResolveServerAsync(IReadOnlyList<ServerProfile> servers, AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.LastSelectedServerId))
        {
            var selected = await _servers.GetByIdAsync(settings.LastSelectedServerId);
            if (selected != null)
            {
                return selected;
            }
        }

        return servers[0];
    }

    private async Task<ServerProfile?> FindFastestServerAsync(List<ServerProfile> servers, CancellationToken cancellationToken)
    {
        var candidates = servers.Take(8).ToList();
        await _speedTest.MeasureAllPingsAsync(candidates, cancellationToken);
        foreach (var s in candidates)
        {
            await _servers.UpdateMetricsAsync(s.Id, s.PingMs, s.LoadPercent, s.DownloadMbps, s.UploadMbps);
        }

        return candidates.Where(s => s.PingMs > 0).MinBy(s => s.PingMs);
    }

    internal static string ReadRecentLogErrors()
    {
        try
        {
            if (!Directory.Exists(AppPaths.LogsRoot))
            {
                return string.Empty;
            }

            var latest = Directory.GetFiles(AppPaths.LogsRoot, "sumrax-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (latest == null)
            {
                return string.Empty;
            }

            var errors = File.ReadAllLines(latest)
                .Where(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                .TakeLast(4)
                .Select(l => l.Length > 180 ? l[..180] + "…" : l)
                .ToList();

            return errors.Count == 0 ? string.Empty : string.Join("\n", errors);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string FormatReport(IReadOnlyList<string> actions)
    {
        var sb = new StringBuilder(_loc.Get("AiFixTitle"));
        sb.AppendLine();
        foreach (var action in actions)
        {
            sb.AppendLine($"• {action}");
        }
        return sb.ToString().Trim();
    }
}
