using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Ai;

internal sealed class AiSystemSnapshot
{
    public int ServerCount { get; init; }
    public string? SelectedServerName { get; init; }
    public ConnectionState VpnState { get; init; }
    public string? ErrorMessage { get; init; }
    public bool XrayReady { get; init; }
    public bool ConfigExists { get; init; }
    public string? RecentLogErrors { get; init; }
    public AppLanguage Language { get; init; }
}
