using AiDesigner.Models;

namespace AiDesigner.Services.Modes;

// Що показує блок «Аналіз» після завершення: освітленість, режим, стиль і повідомлення про відкат (AC-27, AC-28, AC-30).
public sealed record AnalysisResultView(string LightingText, string ModeText, StyleLabel? Style, string? Notice)
{
    public const string CompletedMessage = "Аналіз завершено.";

    // notice — AnalysisOutcome.Notice: режим ШІ не відповів, показано аналіз без ШІ (AC-30).
    public static AnalysisResultView From(AnalysisResult result, string? notice = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new AnalysisResultView(
            Lighting(result.Lighting),
            "Режим: " + DisplayNames.Of(result.Mode),
            StyleLabel.For(result.Mode, result.Style),
            string.IsNullOrWhiteSpace(notice) ? null : notice);
    }

    // Повідомлення вгорі сторінки після успішного аналізу: якщо ШІ не відповів — саме про це.
    public static string Completed(string? notice) =>
        string.IsNullOrWhiteSpace(notice) ? CompletedMessage : notice;

    public static string Lighting(LightingLevel level) => level switch
    {
        LightingLevel.DARK => "темне",
        LightingLevel.MODERATE => "помірне",
        LightingLevel.BRIGHT => "яскраве",
        _ => level.ToString()
    };
}
