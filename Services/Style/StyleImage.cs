using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AiDesigner.Services.Style;

public static class StyleImage
{
    public const int MaxSide = 1024;
    public const string MimeType = "image/jpeg";

    public static byte[] Prepare(byte[] photo)
    {
        ArgumentNullException.ThrowIfNull(photo);

        using var image = Image.Load(photo);
        if (image.Width > MaxSide || image.Height > MaxSide)
            image.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(MaxSide, MaxSide) }));

        using var output = new MemoryStream();
        image.SaveAsJpeg(output, new JpegEncoder { Quality = 85 });
        return output.ToArray();
    }
}
