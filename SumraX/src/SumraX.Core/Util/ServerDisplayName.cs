using System.Text;
using System.Text.RegularExpressions;
using SumraX.Core.Models;

namespace SumraX.Core.Util;

/// <summary>
/// Short labels for home / picker (parity with Android <c>ServerDisplayName.kt</c>).
/// </summary>
public static class ServerDisplayName
{
    private static readonly Regex EmojiLabelPattern = new(
        @"^[\p{So}\p{Cs}]+\s*\p{L}{1,20}.*",
        RegexOptions.Compiled);

    public static void ApplyToProfile(ServerProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = UnpackRemarks(profile.Name);
        }
    }

    /// <summary>Removes <c>?serverDescription=</c> query tails and decodes embedded base64 remarks.</summary>
    public static string UnpackRemarks(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var s = Uri.UnescapeDataString(raw.Trim());
        var qIdx = s.IndexOf('?', StringComparison.Ordinal);
        if (qIdx < 0)
        {
            return s;
        }

        var head = s[..qIdx].Trim();
        var query = s[(qIdx + 1)..];
        string? decodedDesc = null;

        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = segment[..eq].Trim();
            if (!key.Equals("serverDescription", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = segment[(eq + 1)..];
            decodedDesc = TryDecodeBase64Remarks(value) ?? Uri.UnescapeDataString(value);
            break;
        }

        if (string.IsNullOrWhiteSpace(decodedDesc))
        {
            return head;
        }

        decodedDesc = decodedDesc.Trim();
        if (string.IsNullOrWhiteSpace(head))
        {
            return decodedDesc;
        }

        if (head.Contains(' ', StringComparison.Ordinal) ||
            head.Contains('|', StringComparison.Ordinal) ||
            head.Length > 12)
        {
            return head;
        }

        if (head.Contains('|', StringComparison.Ordinal) || decodedDesc.Contains('|', StringComparison.Ordinal))
        {
            return $"{head}\n{decodedDesc}";
        }

        return $"{head} | {decodedDesc}";
    }

    public static string FromProfile(string? remarks, string? server)
    {
        var line = UnpackRemarks(remarks);
        var firstLine = line
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static l => l.Trim())
            .FirstOrDefault(static l => l.Length > 0) ?? string.Empty;

        if (firstLine.Length == 0)
        {
            return ShortenHost(server);
        }

        var parts = firstLine
            .Split('|')
            .Select(static p => p.Trim())
            .Where(static p => p.Length > 0)
            .ToList();

        foreach (var part in parts)
        {
            if (CandidateCountryOrAlias(part))
            {
                return part.Length > 40 ? part[..40] : part;
            }
        }

        foreach (var part in parts)
        {
            if (!LooksLikeTechnical(part))
            {
                return part.Length > 40 ? part[..40] : part;
            }
        }

        return firstLine.Length > 40 ? firstLine[..40] : firstLine;
    }

    private static bool CandidateCountryOrAlias(string part)
    {
        if (part.Length > 36 || LooksLikeTechnical(part))
        {
            return false;
        }

        if (EmojiLabelPattern.IsMatch(part))
        {
            return true;
        }

        return part.Length <= 12 && !part.Contains('.');
    }

    private static bool LooksLikeTechnical(string part)
    {
        var lower = part.ToLowerInvariant();
        return lower.Contains("://", StringComparison.Ordinal) ||
               lower.Contains(".workers.", StringComparison.Ordinal) ||
               lower.Contains("railway", StringComparison.Ordinal) ||
               lower.Contains('@') ||
               lower.StartsWith("vless", StringComparison.Ordinal) ||
               lower.StartsWith("vmess", StringComparison.Ordinal) ||
               lower.StartsWith("trojan", StringComparison.Ordinal);
    }

    private static string ShortenHost(string? server)
    {
        var s = server?.Trim() ?? string.Empty;
        if (s.Length == 0)
        {
            return string.Empty;
        }

        var host = s.Split(':')[0];
        return host.Length > 32 ? host[..32] : host;
    }

    private static string? TryDecodeBase64Remarks(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var padded = value.Trim();
        padded = padded.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        try
        {
            var bytes = Convert.FromBase64String(padded);
            var text = Encoding.UTF8.GetString(bytes).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}