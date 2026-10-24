using AiDesigner.Models;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Services.Analysis;

public static class LightingClassifier
{
    // Піксель «майже чорний», якщо яскравість менша за це значення
    public const int NearBlackLimit = 15;

    // Піксель «майже білий», якщо яскравість більша за це значення
    public const int NearWhiteLimit = 240;

    // A1: даних недостатньо, якщо таких пікселів ПОНАД 95 %
    public const double InsufficientShare = 0.95;

    // Середня яскравість нижче цього значення — «темне»
    public const int DarkBelow = 85;

    // Середня яскравість вище цього значення — «яскраве»
    public const int BrightAbove = 170;

    public static bool IsInsufficientData(IReadOnlyList<Rgb24> pixels)
    {
        EnsureNotEmpty(pixels);

        var extreme = 0;
        foreach (var pixel in pixels)
        {
            var luminance = ColorMath.LuminanceX1000(pixel);
            if (luminance < NearBlackLimit * 1000 || luminance > NearWhiteLimit * 1000)
                extreme++;
        }

        return (double)extreme / pixels.Count > InsufficientShare;
    }

    public static LightingLevel Classify(IReadOnlyList<Rgb24> pixels)
    {
        EnsureNotEmpty(pixels);

        // Сума яскравостей ×1000 у long — без округлень і похибок.
        // «середнє < 85» те саме, що «сума < 85 · 1000 · кількість»
        long sum = 0;
        foreach (var pixel in pixels)
            sum += ColorMath.LuminanceX1000(pixel);

        long count = pixels.Count;
        if (sum < DarkBelow * 1000L * count)
            return LightingLevel.DARK;
        if (sum > BrightAbove * 1000L * count)
            return LightingLevel.BRIGHT;
        return LightingLevel.MODERATE;
    }

    private static void EnsureNotEmpty(IReadOnlyList<Rgb24> pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Count == 0)
            throw new ArgumentException("Немає пікселів для аналізу.", nameof(pixels));
    }
}
