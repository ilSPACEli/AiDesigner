using AiDesigner.Models;

namespace AiDesigner.Clients.Design;

public sealed record AnalysisResultView(string LightingText, string ModeText, StyleLabel? Style, string? Notice)
{
    public const string CompletedMessage = "Аналіз завершено.";

    public static AnalysisResultView From(AnalysisResult result, string? notice = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new AnalysisResultView(
            Lighting(result.Lighting),
            "Режим: " + DisplayNames.Of(result.Mode),
            StyleLabel.For(result.Mode, result.Style),
            string.IsNullOrWhiteSpace(notice) ? null : notice);
    }

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
