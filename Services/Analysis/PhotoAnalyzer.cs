using AiDesigner.Models;

namespace AiDesigner.Services.Analysis;


public class PhotoAnalyzer : IPhotoAnalyzer
{
    public Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photo);

        // Аналіз — це чисті обчислення процесора. Task.Run виносить їх в окремий потік,
        // щоб сторінка в цей час залишалась живою й показувала «Аналіз…».
        return Task.Run(() => Analyze(photo, cancellationToken), cancellationToken);
    }


    public static AnalysisOutcome Analyze(Photo photo, CancellationToken cancellationToken = default)
    {
        // 1. Пікселі зменшеного фото (~200 px по ширині).
        var pixels = PixelSampler.Sample(photo.Content, cancellationToken);

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
