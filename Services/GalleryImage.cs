using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace AiDesigner.Services;

public static class GalleryImage
{
    private const int MaxAttempts = 6;
    private const double Margin = 0.9;

    public static byte[] FitToLimit(byte[] png, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(png);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (png.Length <= maxBytes)
            return png;

        using var original = Image.Load(png);
        var encoder = new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression };
        var result = png;
        var scale = 1.0;

        for (var attempt = 0; attempt < MaxAttempts && result.Length > maxBytes; attempt++)
        {
            // Розмір PNG приблизно пропорційний кількості пікселів.
            scale *= Math.Sqrt((double)maxBytes / result.Length) * Margin;
            var width = Math.Max(1, (int)(original.Width * scale));
            var height = Math.Max(1, (int)(original.Height * scale));

            using var resized = original.Clone(x => x.Resize(width, height));
            using var output = new MemoryStream();
            resized.Save(output, encoder);
            result = output.ToArray();
        }

        return result;
    }
}
