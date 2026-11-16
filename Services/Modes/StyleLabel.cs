using AiDesigner.Models;

namespace AiDesigner.Services.Modes;

// Рядок про стиль кімнати — однаковий на сторінці «Дизайн» і на картці галереї (FR-19, FR-21).
// IsDetermined = false — «стиль не визначено»: показуємо нейтрально, не як помилку.
public sealed record StyleLabel(string Text, bool IsDetermined)
{
    public const string Prefix = "Стиль: ";

    // У режимі «Аналіз без ШІ» стиль не визначається — рядка немає (null).
    public static StyleLabel? For(AnalysisMode mode, RoomStyle? style)
    {
        if (mode == AnalysisMode.BASIC)
            return null;

        return style is { } value
            ? new StyleLabel(Prefix + DisplayNames.Of(value), true)
            : new StyleLabel(DisplayNames.StyleNotDetermined, false);
    }

    // Варіант для даних з API (ResultDto): режим і стиль приходять рядками.
    // Немає режиму (робота до S2) або невідомий режим — рядка немає; невідомий стиль — «не визначено».
    public static StyleLabel? For(string? mode, string? style)
    {
        if (!TryParse<AnalysisMode>(mode, out var parsedMode))
            return null;

        RoomStyle? parsedStyle = TryParse<RoomStyle>(style, out var value) ? value : null;
        return For(parsedMode, parsedStyle);
    }

    // Лише точні імена значень: Enum.TryParse прийняв би й числа ("1"), і "loft".
    private static bool TryParse<T>(string? text, out T value) where T : struct, Enum
    {
        if (text is not null && Enum.GetNames<T>().Contains(text))
        {
            value = Enum.Parse<T>(text);
            return true;
        }

        value = default;
        return false;
    }
}
