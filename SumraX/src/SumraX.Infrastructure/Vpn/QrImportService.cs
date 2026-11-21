using SumraX.Core.Interfaces;
using SumraX.Core.Models;
#if WINDOWS
using ZXing.Windows.Compatibility;
#endif

namespace SumraX.Infrastructure.Vpn;

public sealed class QrImportService
{
    private readonly IConfigImportService _import;

    public QrImportService(IConfigImportService import)
    {
        _import = import;
    }

    public async Task<ServerProfile?> ImportFromImageFileAsync(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(imagePath);
        return await ImportFromStreamAsync(stream);
    }

    public Task<ServerProfile?> ImportFromStreamAsync(Stream imageStream)
    {
#if WINDOWS
        using var bitmap = new System.Drawing.Bitmap(imageStream);
        var reader = new BarcodeReader
        {
            Options = { TryHarder = true, PossibleFormats = [ZXing.BarcodeFormat.QR_CODE] }
        };
        var text = reader.Decode(bitmap)?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult<ServerProfile?>(null);
        }

        return _import.ImportFromLinkAsync(text);
#else
        _ = imageStream;
        return Task.FromResult<ServerProfile?>(null);
#endif
    }
}
