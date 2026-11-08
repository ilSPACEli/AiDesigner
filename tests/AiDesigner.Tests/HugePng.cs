using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests;

internal static class HugePng
{
    public const int Width = 8000;
    public const int Height = 7000;

    private static readonly Lazy<byte[]> Bytes = new(Build);

    public static byte[] Create() => Bytes.Value;

    private static byte[] Build()
    {
        using var image = new Image<L8>(Width, Height, new L8(128));
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder { CompressionLevel = PngCompressionLevel.BestSpeed });
        return stream.ToArray();
    }
}
