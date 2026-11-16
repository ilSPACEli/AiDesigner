using AiDesigner.Models;
using AiDesigner.Services.Style;

namespace AiDesigner.Services.Modes;

// Один пункт вибору режиму: що написати і чи можна його обрати (FR-18).
public sealed record ModeOption(AnalysisMode Mode, string Label, bool IsEnabled, string? Reason);

// Що робити після натискання «Аналіз».
public enum AnalyzeStep
{
    Run,

    // «Хмарний ШІ» без згоди: спершу питаємо (NFR-04, AC-31).
    AskCloudConsent
}

// Логіка вибору режиму аналізу для сторінки «Дизайн». Доступність режимів дає DesignService.GetModesAsync,
// тут — лише що показати і в якому стані кнопки. Сторінка нічого не вирішує сама.
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

    // Пункти завжди в порядку BASIC → LOCAL_AI → CLOUD_AI.
    // modes = null — перевірка ще йде: режими ШІ вимкнені з поясненням, «Аналіз без ШІ» доступний.
    public static IReadOnlyList<ModeOption> Options(IReadOnlyList<ModeAvailability>? modes) =>
        Enum.GetValues<AnalysisMode>().Select(mode => Option(mode, modes)).ToList();

    // Перевірка доступності впала цілком — режими ШІ вимкнені, «Аналіз без ШІ» лишається.
    public static IReadOnlyList<ModeOption> OptionsAfterCheckFailed() =>
        Options(new[] { AnalysisMode.LOCAL_AI, AnalysisMode.CLOUD_AI }
            .Select(m => ModeAvailability.Unavailable(m, DesignService.ModeCheckFailedReason))
            .ToList());

    // Обраний режим став недоступним (напр., Ollama зупинили) — повертаємось до «Аналіз без ШІ».
    public static AnalysisMode KeepSelectable(AnalysisMode selected, IReadOnlyList<ModeOption> options) =>
        options.Any(o => o.Mode == selected && o.IsEnabled) ? selected : DefaultMode;

    // Попередження під вибором режиму.
    public static string? Warning(AnalysisMode mode) =>
        mode == AnalysisMode.LOCAL_AI ? LocalQualityWarning : null;

    public static AnalyzeStep NextStep(AnalysisMode mode, bool hasCloudConsent) =>
        mode == AnalysisMode.CLOUD_AI && !hasCloudConsent ? AnalyzeStep.AskCloudConsent : AnalyzeStep.Run;

    private static ModeOption Option(AnalysisMode mode, IReadOnlyList<ModeAvailability>? modes)
    {
        var label = DisplayNames.Of(mode);

        // «Аналіз без ШІ» доступний завжди, що б не повернула перевірка (FR-18).
        if (mode == AnalysisMode.BASIC)
            return new ModeOption(mode, label, true, null);

        if (modes is null)
            return new ModeOption(mode, label, false, CheckingReason);

        var availability = modes.FirstOrDefault(m => m.Mode == mode);
        if (availability is null)
            return new ModeOption(mode, label, false, DesignService.ModeNotConfiguredReason);

        if (availability.IsAvailable)
            return new ModeOption(mode, label, true, null);

        // Причину показуємо так, як її дав сервіс (напр., «Ollama не запущена на цьому комп'ютері.»).
        var reason = string.IsNullOrWhiteSpace(availability.Reason) ? UnknownReason : availability.Reason;
        return new ModeOption(mode, label, false, reason);
    }
}
