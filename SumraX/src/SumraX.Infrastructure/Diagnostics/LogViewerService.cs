using SumraX.Core.Interfaces;

namespace SumraX.Infrastructure.Diagnostics;

public sealed class LogViewerService : ILogViewerService
{
    public string? GetLatestLogPath()
    {
        if (!Directory.Exists(AppPaths.LogsRoot))
        {
            return null;
        }

        return Directory.GetFiles(AppPaths.LogsRoot, "sumrax-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public async Task<string> ReadTailAsync(int maxLines = 400, CancellationToken cancellationToken = default)
    {
        var path = GetLatestLogPath();
        if (path == null || !File.Exists(path))
        {
            return string.Empty;
        }

        var lines = new List<string>();
        await foreach (var line in ReadLinesReverseAsync(path, cancellationToken))
        {
            lines.Add(line);
            if (lines.Count >= maxLines)
            {
                break;
            }
        }

        lines.Reverse();
        return string.Join(Environment.NewLine, lines);
    }

    private static async IAsyncEnumerable<string> ReadLinesReverseAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const int bufferSize = 4096;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var position = stream.Length;
        var carry = string.Empty;

        while (position > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readSize = (int)Math.Min(bufferSize, position);
            position -= readSize;
            stream.Seek(position, SeekOrigin.Begin);
            var buffer = new byte[readSize];
            _ = await stream.ReadAsync(buffer.AsMemory(0, readSize), cancellationToken);

            var chunk = System.Text.Encoding.UTF8.GetString(buffer) + carry;
            var parts = chunk.Split('\n');
            carry = parts[0];

            for (var i = parts.Length - 1; i >= 1; i--)
            {
                var line = parts[i].TrimEnd('\r');
                if (line.Length > 0)
                {
                    yield return line;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(carry))
        {
            yield return carry.TrimEnd('\r');
        }
    }
}
