namespace SumraX.Core.Models;

public enum DiagnosticStatus
{
    Ok,
    Warning,
    Error
}

public sealed class DiagnosticItem
{
    public string Name { get; init; } = string.Empty;
    public DiagnosticStatus Status { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class DiagnosticReport
{
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<DiagnosticItem> Items { get; init; } = [];
    public bool AllOk => Items.All(i => i.Status == DiagnosticStatus.Ok);
}
