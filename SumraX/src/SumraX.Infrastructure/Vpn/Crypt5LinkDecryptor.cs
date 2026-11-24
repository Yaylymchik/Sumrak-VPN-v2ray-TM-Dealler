using System.Text;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// sumrax://crypt5 — C# port of Android EncryptedCryptResolver (no JNI).
/// </summary>
public static class Crypt5LinkDecryptor
{
    public static bool TryDecryptPayload(string payload, out string decrypted)
    {
        decrypted = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        foreach (var candidate in PayloadCandidates(payload.Trim()))
        {
            var step1 = ReorderSixCharChunks(candidate);
            foreach (var mid in new[] { step1, ShuffleEightCharChunks(step1), Reverse(step1) })
            {
                foreach (var tail in new[] { mid, SwapAdjacentPairs(mid), ShuffleEightCharChunks(SwapAdjacentPairs(mid)) })
                {
                    if (TryDecodeFlexibleBase64(tail, out var text) && LooksLikeUrl(text))
                    {
                        decrypted = text.Trim();
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> PayloadCandidates(string payload)
    {
        var baseNorm = payload.Trim().Replace(' ', '+');
        yield return baseNorm;
        foreach (var suffix in new[] { "=ff", "#ff", "%23ff" })
        {
            if (baseNorm.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && baseNorm.Length > suffix.Length)
            {
                yield return baseNorm[..^suffix.Length];
            }
        }

        if (payload.Contains('%'))
        {
            var decoded = TryUnescape(payload);
            if (decoded != null)
            {
                yield return decoded;
            }
        }
    }

    private static string? TryUnescape(string payload)
    {
        try
        {
            return Uri.UnescapeDataString(payload).Trim().Replace(' ', '+');
        }
        catch
        {
            return null;
        }
    }

    private static string ReorderSixCharChunks(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var chunk in Chunk(input, 6))
        {
            if (chunk.Length > 5)
            {
                sb.Append(chunk[1]).Append(chunk[3]).Append(chunk[5])
                    .Append(chunk[0]).Append(chunk[2]).Append(chunk[4]);
            }
            else
            {
                sb.Append(chunk);
            }
        }

        return sb.ToString();
    }

    private static string ShuffleEightCharChunks(string input)
    {
        var parts = new List<string>();
        foreach (var chunk in Chunk(input, 8, includePartial: true))
        {
            if (chunk.Length > 7)
            {
                parts.Add(
                    string.Concat(
                        chunk[5], chunk[7], chunk[1], chunk[0], chunk[2], chunk[3], chunk[6], chunk[4]));
            }
            else
            {
                parts.Add(chunk);
            }
        }

        return string.Concat(parts);
    }

    private static string SwapAdjacentPairs(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var pair in Chunk(input, 2, includePartial: true))
        {
            if (pair.Length > 1)
            {
                sb.Append(pair[1]).Append(pair[0]);
            }
            else
            {
                sb.Append(pair);
            }
        }

        return sb.ToString();
    }

    private static string Reverse(string s) => new string(s.Reverse().ToArray());

    private static IEnumerable<string> Chunk(string input, int size, bool includePartial = false)
    {
        for (var i = 0; i < input.Length; i += size)
        {
            var len = Math.Min(size, input.Length - i);
            if (len < size && !includePartial)
            {
                yield break;
            }

            yield return input.Substring(i, len);
        }
    }

    private static bool LooksLikeUrl(string text) =>
        text.Contains("://", StringComparison.Ordinal) &&
        (text.StartsWith("http", StringComparison.OrdinalIgnoreCase)
         || text.StartsWith("vmess:", StringComparison.OrdinalIgnoreCase)
         || text.StartsWith("vless:", StringComparison.OrdinalIgnoreCase)
         || text.StartsWith("trojan:", StringComparison.OrdinalIgnoreCase)
         || text.StartsWith("ss://", StringComparison.OrdinalIgnoreCase)
         || text.StartsWith("happ://", StringComparison.OrdinalIgnoreCase)
         || text.StartsWith("sumrax://", StringComparison.OrdinalIgnoreCase));

    private static bool TryDecodeFlexibleBase64(string input, out string text)
    {
        text = string.Empty;
        foreach (var s in new[] { input, input.TrimEnd('=') })
        {
            if (TryDecodeBase64(s, base64Url: false, out text) && !string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (TryDecodeBase64(s, base64Url: true, out text) && !string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
        }

        if (LooksLikeUrl(input))
        {
            text = input;
            return true;
        }

        return false;
    }

    private static bool TryDecodeBase64(string input, bool base64Url, out string text)
    {
        text = string.Empty;
        try
        {
            var norm = input.Replace('-', '+').Replace('_', '/');
            var mod = norm.Length % 4;
            if (mod > 0)
            {
                norm = norm.PadRight(norm.Length + (4 - mod), '=');
            }

            var bytes = Convert.FromBase64String(norm);
            text = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}