using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

public static class SubscriptionUserInfoParser
{
    public static SubscriptionTrafficInfo? Parse(string? header, string url = "")
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        long upload = 0;
        long download = 0;
        long total = 0;
        DateTime? expire = null;

        foreach (var part in header.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = part[..eq].Trim().ToLowerInvariant();
            var value = part[(eq + 1)..].Trim();
            switch (key)
            {
                case "upload":
                    long.TryParse(value, out upload);
                    break;
                case "download":
                    long.TryParse(value, out download);
                    break;
                case "total":
                    long.TryParse(value, out total);
                    break;
                case "expire":
                    if (long.TryParse(value, out var exp) && exp > 0)
                    {
                        expire = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
                    }
                    break;
            }
        }

        if (upload == 0 && download == 0 && total == 0 && expire == null)
        {
            return null;
        }

        return new SubscriptionTrafficInfo
        {
            Url = url,
            UploadBytes = upload,
            DownloadBytes = download,
            TotalBytes = total,
            ExpireUtc = expire,
            FetchedUtc = DateTime.UtcNow
        };
    }
}
