using System.Text.RegularExpressions;

namespace SumraX.Infrastructure.Vpn;

public static class SubscriptionUrlNormalizer
{
    private static readonly Regex DocExportRegex = new(
        @"([a-zA-Z0-9_-]{15,})/export\?format=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        input = input.Trim().Trim('"', '\'', ' ', '\t', '\r', '\n');
        input = input.Replace("\\", "");

        if (input.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            input = "https://" + input;
        }

        if (input.StartsWith("doc.google.com", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("docs.google.com", StringComparison.OrdinalIgnoreCase))
        {
            input = "https://" + input;
        }

        if (!input.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            input.Contains("export?format=", StringComparison.OrdinalIgnoreCase))
        {
            var match = DocExportRegex.Match(input);
            if (match.Success)
            {
                return $"https://doc.google.com/document/d/{match.Groups[1].Value}/export?format=txt";
            }
        }

        if (Regex.IsMatch(input, @"^[a-zA-Z0-9_-]{20,}$"))
        {
            return $"https://doc.google.com/document/d/{input}/export?format=txt";
        }

        if (!input.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            input.Contains("document/d/", StringComparison.OrdinalIgnoreCase))
        {
            input = "https://" + input.TrimStart('/');
        }

        input = CryptLinkDecryptor.UnwrapCryptLink(input);
        if (CryptLinkDecryptor.IsCryptLink(input))
        {
            if (CryptLinkDecryptor.TryDecrypt(input, out var decrypted) && !string.IsNullOrWhiteSpace(decrypted))
            {
                return Normalize(decrypted);
            }
        }

        return input;
    }

    public static bool IsSubscriptionUrl(string input)
    {
        input = Normalize(input);
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (CryptLinkDecryptor.IsCryptLink(input))
        {
            return false;
        }

        if (ProxyLinkParser.IsSupportedScheme(input))
        {
            return false;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            return true;
        }

        return input.Contains("export?format=", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProtocolLink(string input) => ProxyLinkParser.IsSupportedScheme(input.Trim());
}
