using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
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

    // «Кімната» JPEG для AC-14: теракотова стіна, світла стіна з тінню, коричнева підлога
    public static byte[] RoomJpeg(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (y > height * 2 / 3)
                        row[x] = new Rgb24((byte)(120 + x * 40 / width), 75, 40);              // підлога
                    else if (x < width / 4)
                        row[x] = new Rgb24(200, (byte)(110 + y * 20 / height), 95);            // ліва стіна
                    else
                    {
                        var shade = y * 30 / height;
                        row[x] = new Rgb24((byte)(225 - shade), (byte)(220 - shade), (byte)(210 - shade)); // задня стіна
                    }
                }
            }
        });

        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 90 });
        return stream.ToArray();
    }

    private static byte[] ToPng(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}