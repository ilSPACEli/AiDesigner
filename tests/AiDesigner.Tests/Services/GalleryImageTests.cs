using AiDesigner.Services;
using AiDesigner.Tests.Analysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Services;

// Копія результату для галереї (#53, CR-08).
public class GalleryImageTests
{
    private static byte[] NoisyPng(int width, int height)
    {
        var random = new Random(7);
        using var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(rows =>
        {
            var bytes = new byte[width * 3];
            for (var y = 0; y < rows.Height; y++)
            {
                random.NextBytes(bytes);
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                    row[x] = new Rgb24(bytes[x * 3], bytes[x * 3 + 1], bytes[x * 3 + 2]);
            }
        });
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    [Fact]
    public void WithinLimit_ReturnsSameBytes()
    {
        var png = TestImages.Solid(40, 30, new Rgb24(200, 100, 50));

        Assert.Same(png, GalleryImage.FitToLimit(png, png.Length));
    }

    [Fact]
    public void AboveLimit_ReducedPngWithinLimit_SameProportions()
    {
        var png = NoisyPng(400, 200);
        var limit = png.Length / 3;

        var copy = GalleryImage.FitToLimit(png, limit);

        Assert.True(copy.Length <= limit, $"Копія {copy.Length} Б, ліміт {limit} Б.");
        var info = Image.Identify(copy);
        Assert.IsType<PngFormat>(info.Metadata.DecodedImageFormat);
        Assert.True(info.Width < 400);
        Assert.Equal(2.0, (double)info.Width / info.Height, 1);
    }

    [Fact]
    public void InvalidLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GalleryImage.FitToLimit(new byte[] { 1 }, 0));
    }
}
