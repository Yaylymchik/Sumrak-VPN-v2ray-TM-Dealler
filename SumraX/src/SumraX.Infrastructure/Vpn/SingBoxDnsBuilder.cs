using System.Net;
using System.Text.Json.Nodes;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// sing-box 1.12+ typed DNS servers (legacy "address" field removed in 1.14).
/// </summary>
public static class SingBoxDnsBuilder
{
    public static JsonObject Build(AppSettings settings)
    {
        var direct = ParseServer(
            string.IsNullOrWhiteSpace(settings.DnsServer) ? "1.1.1.1" : settings.DnsServer.Trim(),
            "dns-direct");
        var remote = ParseServer(
            string.IsNullOrWhiteSpace(settings.RemoteDns) ? "https://cloudflare-dns.com/dns-query" : settings.RemoteDns.Trim(),
            "dns-remote");

        if (remote["type"]?.GetValue<string>() is "https" or "tls" or "h3")
        {
            remote["domain_resolver"] = "dns-direct";
        }

        return new JsonObject
        {
            ["servers"] = new JsonArray { direct, remote },
            ["final"] = "dns-remote",
            ["strategy"] = "ipv4_only"
        };
    }

    private static JsonObject ParseServer(string input, string tag)
    {
        input = input.Trim().Trim('"');

        if (input.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject { ["type"] = "local", ["tag"] = tag };
        }

        if (input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(input);
            var server = new JsonObject
            {
                ["type"] = "https",
                ["tag"] = tag,
                ["server"] = uri.Host,
                ["server_port"] = uri.Port > 0 ? uri.Port : 443
            };
            var path = uri.AbsolutePath;
            if (!string.IsNullOrEmpty(path) && path != "/")
            {
                server["path"] = path;
            }

            return server;
        }

        if (input.StartsWith("h3://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(input);
            var server = new JsonObject
            {
                ["type"] = "h3",
                ["tag"] = tag,
                ["server"] = uri.Host,
                ["server_port"] = uri.Port > 0 ? uri.Port : 443
            };
            var path = uri.AbsolutePath;
            if (!string.IsNullOrEmpty(path) && path != "/")
            {
                server["path"] = path;
            }

            return server;
        }

        if (input.StartsWith("tls://", StringComparison.OrdinalIgnoreCase))
        {
            var host = input["tls://".Length..].Split('/')[0];
            return new JsonObject { ["type"] = "tls", ["tag"] = tag, ["server"] = host };
        }

        if (input.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            var host = input["tcp://".Length..].Split('/')[0];
            return new JsonObject { ["type"] = "tcp", ["tag"] = tag, ["server"] = host };
        }

        if (input.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
        {
            input = input["udp://".Length..];
        }

        if (input.StartsWith("quic://", StringComparison.OrdinalIgnoreCase))
        {
            var host = input["quic://".Length..].Split('/')[0];
            return new JsonObject { ["type"] = "quic", ["tag"] = tag, ["server"] = host };
        }

        if (IPAddress.TryParse(input, out _))
        {
            return new JsonObject { ["type"] = "udp", ["tag"] = tag, ["server"] = input };
        }

        return new JsonObject { ["type"] = "tls", ["tag"] = tag, ["server"] = input };
    }
}
