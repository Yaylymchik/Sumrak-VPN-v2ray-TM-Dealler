using SumraX.Core.Enums;

namespace SumraX.App.Services;

internal static class CyberTerminalFeed
{
    private static readonly Random Rng = Random.Shared;

    private static readonly string[] IdleLines =
    [
        "scanning local interfaces...",
        "loading sing-box core module",
        "cipher suite: AES-256-GCM / ChaCha20-Poly1305",
        "DNS resolver standby [127.0.0.1:53]",
        "TUN adapter offline — awaiting auth",
        "geo-ip database sync...",
        "routing table snapshot saved",
        "entropy pool refresh OK",
        "firewall policy: STRICT",
        "node pool indexed",
        "ARP probe on gateway [OK]",
        "checking kill-switch state...",
        "proxy chain: idle",
        "session token: pending",
        "awaiting operator input..."
    ];

    private static readonly string[] ConnectingLines =
    [
        "inject route table entry tun0",
        "TLS handshake initiated",
        "exchanging session keys",
        "negotiating transport layer",
        "sing-box: applying configuration",
        "TUN interface bring-up",
        "DNS hijack routing active",
        "MTU probe 1420 bytes",
        "certificate pin verify...",
        "wireguard/reality handshake",
        "core selector: sing-box",
        "retry watchdog armed",
        "buffering outbound packets",
        "syncing remote endpoint..."
    ];

    private static readonly string[] ConnectedLines =
    [
        "tunnel ESTABLISHED",
        "forwarding traffic via egress node",
        "AES-256-GCM stream active",
        "kill-switch: ARMED",
        "DNS leak test: PASS",
        "IPv6 egress: filtered",
        "heartbeat OK",
        "NAT traversal stable",
        "packet filter: ACCEPT",
        "traffic obfuscation layer ON",
        "session integrity verified",
        "remote attestation OK",
        "latency probe scheduled",
        "uplink cipher rekey OK"
    ];

    private static readonly string[] ErrorLines =
    [
        "CONNECTION RESET by peer",
        "sing-box process exited unexpectedly",
        "handshake timeout — retry advised",
        "route injection failed",
        "DNS bootstrap unreachable",
        "core stderr: non-zero exit",
        "endpoint refused connection",
        "config schema mismatch — check logs",
        "diagnostics recommended"
    ];

    public static string Next(ConnectionState state, string? serverName, string? protocol, int pingMs)
    {
        var line = state switch
        {
            ConnectionState.Connected => Pick(ConnectedLines),
            ConnectionState.Connecting or ConnectionState.Reconnecting => Pick(ConnectingLines),
            ConnectionState.Error => Pick(ErrorLines),
            _ => Pick(IdleLines)
        };

        if (Rng.NextDouble() < 0.22)
        {
            return $"0x{Rng.Next(0x10000000, 0x7FFFFFFF):X8}  {RandomHex(16)}  {RandomHex(8)}";
        }

        if (Rng.NextDouble() < 0.18 && !string.IsNullOrWhiteSpace(serverName))
        {
            line = line.Replace("egress node", serverName, StringComparison.OrdinalIgnoreCase);
            line = $"{line} → {serverName}";
        }

        if (Rng.NextDouble() < 0.15 && !string.IsNullOrWhiteSpace(protocol))
        {
            line = $"{protocol.ToUpperInvariant()} / {line}";
        }

        if (state == ConnectionState.Connected && pingMs > 0 && Rng.NextDouble() < 0.25)
        {
            line = $"latency probe: {pingMs}ms — {line}";
        }

        if (Rng.NextDouble() < 0.12)
        {
            line = $"[{DateTime.Now:HH:mm:ss}] {line}";
        }

        if (Rng.NextDouble() < 0.1)
        {
            line = $"rx={Rng.Next(120, 9999)} tx={Rng.Next(80, 4200)} | {line}";
        }

        return line;
    }

    public static string SessionBanner(ConnectionState state) => state switch
    {
        ConnectionState.Connected => "[SESSION] secure tunnel online",
        ConnectionState.Connecting => "[SESSION] initiating handshake",
        ConnectionState.Reconnecting => "[SESSION] reconnecting...",
        ConnectionState.Error => "[SESSION] link failure detected",
        _ => "[SESSION] standby mode"
    };

    private static string Pick(string[] pool) => pool[Rng.Next(pool.Length)];

    private static string RandomHex(int bytes) =>
        Convert.ToHexString(RandomNumber(bytes)).ToLowerInvariant();

    private static byte[] RandomNumber(int bytes)
    {
        var buf = new byte[bytes];
        Rng.NextBytes(buf);
        return buf;
    }
}
