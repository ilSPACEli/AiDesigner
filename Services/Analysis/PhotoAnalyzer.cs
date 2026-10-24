using AiDesigner.Models;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Services.Analysis;


public class PhotoAnalyzer : IPhotoAnalyzer
{
    // Межа пікселів; у тестах можна зменшити
    public long MaxPixels { get; init; } = PixelSampler.MaxPixels;

    public Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photo);

        // Аналіз — це чисті обчислення процесора. Task.Run виносить їх в окремий потік,
        // щоб сторінка в цей час залишалась живою й показувала «Аналіз…».
        return Task.Run(() => Analyze(photo, cancellationToken, MaxPixels), cancellationToken);
    }


    public static AnalysisOutcome Analyze(
        Photo photo, CancellationToken cancellationToken = default, long maxPixels = PixelSampler.MaxPixels)
    {
        // 1. Пікселі зменшеного фото (~200 px по ширині).
        Rgb24[] pixels;
        try
        {
            pixels = PixelSampler.Sample(photo.Content, cancellationToken, maxPixels);
        }
        catch (ImageTooLargeException)
        {
            // E1 із зрозумілою причиною замість загального «сталася помилка»
            return AnalysisOutcome.Error(
                $"Фото завелике для аналізу (понад {maxPixels / 1_000_000} Мп). Надайте фото меншої роздільності.");
        }

        // 2. A1: майже все чорне або біле — далі рахувати немає сенсу.
        //    На Activity Diagram «Достатньо даних?» стоїть після обчислень; ми перевіряємо
        //    раніше, бо це дешевше, а результат той самий: результат не зберігається.
        if (LightingClassifier.IsInsufficientData(pixels))
            return AnalysisOutcome.InsufficientData();

        // 3. FR-04: рівень освітленості.
        var lighting = LightingClassifier.Classify(pixels);

        // 4. FR-03: кластери кольорів і палітра 3–5 кольорів.
        var clusters = KMeans.Cluster(pixels, KMeans.DefaultK, cancellationToken);
        if (!PaletteBuilder.TryBuild(clusters, out var palette))
            return AnalysisOutcome.InsufficientData();   // розширення A1: однотонне фото

        return AnalysisOutcome.Success(new AnalysisResult
        {
            Colors = palette,
            Lighting = lighting,
            AnalyzedAt = DateTime.Now
        });
    }
}
