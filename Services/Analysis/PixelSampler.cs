using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AiDesigner.Services.Analysis;


public static class PixelSampler
{
    // Ширина, до якої зменшуємо фото
    public const int TargetWidth = 200;

    // Найбільше фото, яке розпаковуємо (50 Мп ≈ 150 МБ пам'яті).
    // Захист від «декомпресійної бомби»: файл до 10 МБ, що розпаковується в гігантську картинку
    public const long MaxPixels = 50_000_000;

    public static Rgb24[] Sample(
        byte[] content, CancellationToken cancellationToken = default, long maxPixels = MaxPixels)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Спершу читаємо лише заголовок файлу: розміри відомі без розпаковування пікселів
        var info = Image.Identify(content);
        if ((long)info.Width * info.Height > maxPixels)
            throw new ImageTooLargeException(info.Width, info.Height, maxPixels);

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

// Фото має забагато пікселів для безпечного аналізу
public sealed class ImageTooLargeException : Exception
{
    public ImageTooLargeException(int width, int height, long maxPixels)
        : base($"Зображення {width}×{height} перевищує межу {maxPixels} пікселів.")
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }
}
