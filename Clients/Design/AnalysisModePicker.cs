using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Style;

namespace AiDesigner.Clients.Design;

public sealed record ModeOption(AnalysisMode Mode, string Label, bool IsEnabled, string? Reason);

public enum AnalyzeStep
{
    Run,

    AskCloudConsent
}


public static class AnalysisModePicker
{
    public const AnalysisMode DefaultMode = AnalysisMode.BASIC;

    public const string CheckingReason = "Перевіряємо доступність…";

    public const string UnknownReason = "Режим недоступний.";

    public const string LocalQualityWarning = OllamaStyleAnalyzer.QualityWarning;

    public const string CloudConsentTitle = "Згода на передачу фото";

    public const string CloudConsentText =
        "Щоб визначити стиль кімнати, фото буде передано сервісу Google Gemini. " +
        "Без вашої згоди фото не надсилається. Згода діє до кінця сесії.";

    public const string ConsentAcceptLabel = "Погоджуюсь";

    public const string ConsentCancelLabel = "Скасувати";

    public const string ConsentDeclinedMessage =
        "Аналіз не запущено: без згоди фото не передається. Оберіть інший режим або погодьтеся на передачу.";


    public static IReadOnlyList<ModeOption> Options(IReadOnlyList<ModeAvailability>? modes) =>
        Enum.GetValues<AnalysisMode>().Select(mode => Option(mode, modes)).ToList();

    public static IReadOnlyList<ModeOption> OptionsAfterCheckFailed() =>
        Options(new[] { AnalysisMode.LOCAL_AI, AnalysisMode.CLOUD_AI }
            .Select(m => ModeAvailability.Unavailable(m, DesignService.ModeCheckFailedReason))
            .ToList());

    public static AnalysisMode KeepSelectable(AnalysisMode selected, IReadOnlyList<ModeOption> options) =>
        options.Any(o => o.Mode == selected && o.IsEnabled) ? selected : DefaultMode;

    public static string? Warning(AnalysisMode mode) =>
        mode == AnalysisMode.LOCAL_AI ? LocalQualityWarning : null;

    public static AnalyzeStep NextStep(AnalysisMode mode, bool hasCloudConsent) =>
        mode == AnalysisMode.CLOUD_AI && !hasCloudConsent ? AnalyzeStep.AskCloudConsent : AnalyzeStep.Run;

    private static ModeOption Option(AnalysisMode mode, IReadOnlyList<ModeAvailability>? modes)
    {
        var label = DisplayNames.Of(mode);

        if (mode == AnalysisMode.BASIC)
            return new ModeOption(mode, label, true, null);

        if (modes is null)
            return new ModeOption(mode, label, false, CheckingReason);

        var availability = modes.FirstOrDefault(m => m.Mode == mode);
        if (availability is null)
            return new ModeOption(mode, label, false, DesignService.ModeNotConfiguredReason);

        if (availability.IsAvailable)
            return new ModeOption(mode, label, true, null);

        var reason = string.IsNullOrWhiteSpace(availability.Reason) ? UnknownReason : availability.Reason;
        return new ModeOption(mode, label, false, reason);
    }
}
