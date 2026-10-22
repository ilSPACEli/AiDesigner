using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AiDesigner.Services.Analysis;


public static class PixelSampler
{
    // Ширина, до якої зменшуємо фото
    public const int TargetWidth = 200;

    public static Rgb24[] Sample(byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var image = Image.Load<Rgb24>(content);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Width > TargetWidth)
        {
            // Висоту рахуємо пропорційно, щоб не спотворити співвідношення сторін.
            var height = Math.Max(1, (int)Math.Round(image.Height * (double)TargetWidth / image.Width));

            // Box — усереднення пікселів, що потрапили в одну клітинку.
            image.Mutate(x => x.Resize(TargetWidth, height, KnownResamplers.Box));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var pixels = new Rgb24[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);
        return pixels;
    }
}
