namespace SumraX.Core.Models;

public enum SpeedTestPhase
{
    Idle,
    Ping,
    Download,
    Upload,
    Done
}

public sealed class SpeedTestProgress
{
    public SpeedTestPhase Phase { get; init; }
    public double MegabitsPerSecond { get; init; }
    public int PingMs { get; init; }
}
