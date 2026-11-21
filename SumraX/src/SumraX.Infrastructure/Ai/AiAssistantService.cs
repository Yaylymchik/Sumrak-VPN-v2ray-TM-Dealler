using System.Text;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;

namespace SumraX.Infrastructure.Ai;

/// <summary>
/// Offline AI assistant: broad Q&amp;A, diagnostics, and automatic internal repairs.
/// </summary>
public sealed class AiAssistantService : IAiAssistant
{
    private readonly List<AiMessage> _history = [];
    private readonly IServerRepository _servers;
    private readonly ISettingsRepository _settings;
    private readonly IVpnEngine _vpn;
    private readonly ILocalizationService _loc;
    private readonly AiAutoRepair _repair;

    private static readonly (string[] Keys, string LocKey)[] Topics =
    [
        (["привет", "здравств", "hello", "hi", "salam", "hey"], "AiGreeting"),
        (["что умеешь", "что ты", "помощь", "help", "capabilities", "kömek"], "AiCapabilities"),
        (["профил", "подписк", "истек", "бесплат", "user", "profil", "abuna"], "AiProfileHelp"),
        (["прокси", "tun", "туннел", "tunnel", "режим", "системн", "proksi"], "AiProxyModeHelp"),
        (["xray", "ядро", "core", "çekir"], "AiXrayHelp"),
        (["лог", "log", "journal"], "AiLogsHelp"),
        (["язык", "language", "dil", "локал"], "AiLanguageHelp"),
        (["отключ", "disconnect", "kes"], "AiDisconnectHelp"),
        (["подключ", "connect", "baglan", "как включ"], "AiSetupHelp"),
        (["безопас", "security", "dpapi", "шифр", "gorag"], "AiSecurityHelp"),
        (["бэкап", "backup", "резерв", "restore", "ätiýaç"], "AiBackupHelp"),
        (["автозапуск", "autostart", "windows"], "AiAutoStartHelp"),
        (["dns", "утеч", "leak", "syzma"], "AiDnsHelp"),
        (["kill", "switch", "блок"], "AiKillSwitchHelp"),
        (["import", "импорт", "qr", "ссыл", "link", "abuna", "подписк", "vless"], "AiImportHelp"),
        (["скорост", "speed", "tizlik", "ping", "пинг"], "AiSpeedHelp"),
        (["сервер", "server", "optimal", "best", "ýokary", "saýla"], "AiTopicRecommend"),
        (["настрой", "setup", "configure", "sazla"], "AiSetupHelp"),
    ];

    public AiAssistantService(
        IServerRepository servers,
        ISettingsRepository settings,
        IVpnEngine vpn,
        ISpeedTestService speedTest,
        IXrayBootstrap bootstrap,
        ILocalizationService loc)
    {
        _servers = servers;
        _settings = settings;
        _vpn = vpn;
        _loc = loc;
        _repair = new AiAutoRepair(vpn, servers, settings, bootstrap, speedTest, loc);
    }

    public IReadOnlyList<AiMessage> History => _history;

    public void ClearHistory() => _history.Clear();

    public async Task<string> AskAsync(string question, ConnectionStatus? status = null)
    {
        _history.Add(new AiMessage { IsUser = true, Content = question });
        status ??= _vpn.CurrentStatus;
        var snapshot = await BuildSnapshotAsync(status);
        var answer = await BuildAnswerAsync(question, status, snapshot);
        _history.Add(new AiMessage { IsUser = false, Content = answer });
        return answer;
    }

    private async Task<string> BuildAnswerAsync(string question, ConnectionStatus status, AiSystemSnapshot snapshot)
    {
        var q = question.ToLowerInvariant();

        if (IsFixRequest(q))
        {
            var (_, report) = await _repair.RunAsync();
            return report;
        }

        if (IsStatusQuestion(q))
        {
            return BuildStatusAnswer(snapshot, status);
        }

        if (IsConnectionProblem(q) || ContainsAny(q, "ошиб", "error", "failed", "bag", "ýalňyş", "säwlik"))
        {
            var sb = new StringBuilder();
            if (IsConnectionProblem(q))
            {
                sb.AppendLine(_loc.Get("AiNotWorking"));
                sb.AppendLine();
            }
            sb.AppendLine(ExplainError(status));
            sb.AppendLine();
            sb.AppendLine(_loc.Get("AiFixHint"));
            var (_, report) = await _repair.RunAsync();
            sb.AppendLine();
            sb.Append(report);
            return sb.ToString().Trim();
        }

        var topic = MatchTopic(q);
        if (topic != null)
        {
            if (topic == "AiTopicRecommend")
            {
                return await RecommendServerAsync();
            }
            return _loc.Get(topic);
        }

        return BuildSmartAnswer(question, snapshot, status);
    }

    private async Task<AiSystemSnapshot> BuildSnapshotAsync(ConnectionStatus status)
    {
        var servers = await _servers.GetAllAsync();
        var settings = await _settings.GetAsync();
        ServerProfile? selected = null;
        if (!string.IsNullOrWhiteSpace(settings.LastSelectedServerId))
        {
            selected = await _servers.GetByIdAsync(settings.LastSelectedServerId);
        }

        return new AiSystemSnapshot
        {
            ServerCount = servers.Count,
            SelectedServerName = selected?.Name ?? status.ServerName,
            VpnState = status.State,
            ErrorMessage = status.ErrorMessage,
            XrayReady = File.Exists(AppPaths.XrayPath),
            ConfigExists = File.Exists(AppPaths.ConfigPath),
            RecentLogErrors = AiAutoRepair.ReadRecentLogErrors(),
            Language = settings.Language
        };
    }

    private string BuildStatusAnswer(AiSystemSnapshot snapshot, ConnectionStatus status)
    {
        var sb = new StringBuilder(_loc.Get("AiContextTitle"));
        sb.AppendLine();
        sb.AppendLine(_loc.Format("AiContextServers", snapshot.ServerCount));
        if (!string.IsNullOrWhiteSpace(snapshot.SelectedServerName))
        {
            sb.AppendLine(_loc.Format("AiContextSelected", snapshot.SelectedServerName));
        }
        sb.AppendLine(_loc.Format("AiContextState", FormatState(status.State)));
        sb.AppendLine(_loc.Format("AiContextXray", snapshot.XrayReady ? _loc.Get("AiYes") : _loc.Get("AiNo")));
        if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
        {
            sb.AppendLine(_loc.Format("AiDiagError", status.ErrorMessage));
        }
        if (!string.IsNullOrWhiteSpace(status.PublicIp))
        {
            sb.AppendLine(_loc.Format("AiContextIp", status.PublicIp));
        }
        return sb.ToString().Trim();
    }

    private string BuildSmartAnswer(string question, AiSystemSnapshot snapshot, ConnectionStatus status)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_loc.Format("AiSmartIntro", question.Trim()));
        sb.AppendLine();
        sb.Append(BuildStatusAnswer(snapshot, status));

        if (snapshot.ServerCount == 0)
        {
            sb.AppendLine();
            sb.AppendLine(_loc.Get("AiImportHelp"));
        }
        else if (status.State != ConnectionState.Connected)
        {
            sb.AppendLine();
            sb.AppendLine(_loc.Get("AiFixHint"));
        }

        sb.AppendLine();
        sb.AppendLine(_loc.Get("AiCapabilities"));
        return sb.ToString().Trim();
    }

    private static string? MatchTopic(string q)
    {
        string? best = null;
        var bestScore = 0;
        foreach (var (keys, locKey) in Topics)
        {
            var score = keys.Count(k => q.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (score > bestScore)
            {
                bestScore = score;
                best = locKey;
            }
        }
        return bestScore > 0 ? best : null;
    }

    private static bool IsFixRequest(string q) =>
        ContainsAny(q,
            "исправ", "почини", "починить", "устран", "fix", "repair", "diagnose",
            "автоисправ", "п почин", "налад", "remont", "düzet");

    private static bool IsStatusQuestion(string q) =>
        ContainsAny(q,
            "статус", "состояние", "status", "сколько сервер", "какой ip", "какой ip",
            "what is my ip", "þagdaý", "ýagdaý", "neçe serwer");

    private static bool IsConnectionProblem(string q) =>
        ContainsAny(q,
            "не работ", "не подключ", "не коннект", "не идет", "не идёт", "не открыв",
            "не груз", "не ловит", "не соедин", "не работает", "не включа",
            "not work", "doesn't work", "doesnt work", "can't connect", "cannot connect",
            "no internet", "интернет", "internet ýok", "işlemeýär", "baglanmaýar");

    private string ExplainError(ConnectionStatus? status)
    {
        var sb = new StringBuilder(_loc.Get("AiDiagTitle"));
        sb.AppendLine();

        if (status != null)
        {
            sb.AppendLine(_loc.Format("AiDiagState", FormatState(status.State)));
            if (!string.IsNullOrWhiteSpace(status.ServerName))
            {
                sb.AppendLine(_loc.Format("AiDiagServer", status.ServerName));
            }
        }

        if (!string.IsNullOrWhiteSpace(status?.ErrorMessage))
        {
            sb.AppendLine(_loc.Format("AiDiagError", status.ErrorMessage));
        }

        if (status?.State == ConnectionState.Error)
        {
            if (status.ErrorMessage?.Contains("xray", StringComparison.OrdinalIgnoreCase) == true)
            {
                sb.AppendLine(_loc.Get("AiDiagXray"));
            }
            sb.AppendLine(_loc.Get("AiDiagVerify"));
            sb.AppendLine(_loc.Get("AiDiagTls"));
            sb.AppendLine(_loc.Get("AiDiagTryOther"));
        }
        else if (status?.State == ConnectionState.Connected)
        {
            sb.AppendLine(_loc.Get("AiDiagStable"));
        }
        else
        {
            sb.AppendLine(_loc.Get("AiDiagDisconnected"));
        }

        return sb.ToString().Trim();
    }

    private string FormatState(ConnectionState state) => state switch
    {
        ConnectionState.Connected => _loc.Get("AiStateConnected"),
        ConnectionState.Connecting => _loc.Get("AiStateConnecting"),
        ConnectionState.Reconnecting => _loc.Get("AiStateReconnecting"),
        ConnectionState.Error => _loc.Get("AiStateError"),
        _ => _loc.Get("AiStateDisconnected")
    };

    private async Task<string> RecommendServerAsync()
    {
        var servers = await _servers.GetAllAsync();
        if (servers.Count == 0)
        {
            return _loc.Get("AiRecommendNone");
        }

        var best = servers
            .Where(s => s.PingMs > 0)
            .OrderBy(s => s.PingMs)
            .FirstOrDefault() ?? servers.OrderBy(s => s.Name).First();

        var ping = best.PingMs > 0 ? best.PingMs.ToString() : "—";
        return _loc.Format("AiRecommendServer", best.Name, ping, best.Protocol);
    }

    private static bool ContainsAny(string text, params string[] keys) =>
        keys.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
