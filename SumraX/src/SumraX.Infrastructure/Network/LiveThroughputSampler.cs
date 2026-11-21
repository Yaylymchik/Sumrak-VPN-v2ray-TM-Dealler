using System.Net.NetworkInformation;

namespace SumraX.Infrastructure.Network;

/// <summary>
/// Samples aggregate NIC throughput (bytes/sec → Mbps) for live speed display.
/// </summary>
public sealed class LiveThroughputSampler
{
    private long _lastRx;
    private long _lastTx;
    private DateTime _lastAt;
    private bool _initialized;

    public (double DownMbps, double UpMbps) Sample()
    {
        var (rx, tx) = SumCounters();
        var now = DateTime.UtcNow;

        if (!_initialized)
        {
            _initialized = true;
            _lastRx = rx;
            _lastTx = tx;
            _lastAt = now;
            return (0, 0);
        }

        var sec = (now - _lastAt).TotalSeconds;
        if (sec < 0.05)
        {
            return (0, 0);
        }

        var down = Math.Max(0, (rx - _lastRx) * 8.0 / sec / 1_000_000);
        var up = Math.Max(0, (tx - _lastTx) * 8.0 / sec / 1_000_000);
        _lastRx = rx;
        _lastTx = tx;
        _lastAt = now;
        return (down, up);
    }

    public void Reset() => _initialized = false;

    private static (long Rx, long Tx) SumCounters()
    {
        long rx = 0, tx = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up ||
                ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var s = ni.GetIPv4Statistics();
            rx += s.BytesReceived;
            tx += s.BytesSent;
        }

        return (rx, tx);
    }
}
