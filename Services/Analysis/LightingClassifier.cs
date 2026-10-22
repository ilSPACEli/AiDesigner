using AiDesigner.Models;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Services.Analysis;

public static class LightingClassifier
{
    // Піксель «майже чорний», якщо яскравість менша за це значення
    public const double NearBlackLimit = 15;

    // Піксель «майже білий», якщо яскравість більша за це значення
    public const double NearWhiteLimit = 240;

    // A1: даних недостатньо, якщо таких пікселів ПОНАД 95 %
    public const double InsufficientShare = 0.95;

    // Середня яскравість нижче цього значення — «темне»
    public const double DarkBelow = 85;

    // Середня яскравість вище цього значення — «яскраве»
    public const double BrightAbove = 170;

    public static bool IsInsufficientData(IReadOnlyList<Rgb24> pixels)
    {
        EnsureNotEmpty(pixels);

        var extreme = 0;
        foreach (var pixel in pixels)
        {
            var luminance = ColorMath.Luminance(pixel);
            if (luminance < NearBlackLimit || luminance > NearWhiteLimit)
                extreme++;
        }

        return (double)extreme / pixels.Count > InsufficientShare;
    }

    public static LightingLevel Classify(IReadOnlyList<Rgb24> pixels)
    {
        EnsureNotEmpty(pixels);

        var sum = 0.0;
        foreach (var pixel in pixels)
            sum += ColorMath.Luminance(pixel);

        var mean = Math.Round(sum / pixels.Count, 6);

        if (mean < DarkBelow)
            return LightingLevel.DARK;
        if (mean > BrightAbove)
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
