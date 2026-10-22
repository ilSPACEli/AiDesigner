using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Analysis;

// Генерує тестові фото прямо в пам'яті
internal static class TestImages
{
    public static byte[] Solid(int width, int height, Rgb24 color)
    {
        using var image = new Image<Rgb24>(width, height, color);
        return ToPng(image);
    }

    public static byte[] Stripes(int width, int height, params (Rgb24 Color, int Percent)[] stripes)
    {
        using var image = new Image<Rgb24>(width, height);
        var left = 0;
        foreach (var (color, percent) in stripes)
        {
            var stripeWidth = width * percent / 100;
            for (var x = left; x < left + stripeWidth; x++)
                for (var y = 0; y < height; y++)
                    image[x, y] = color;
            left += stripeWidth;
        }
        return ToPng(image);
    }

    private static byte[] ToPng(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
