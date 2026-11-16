namespace AiDesigner.Models;

public static class DisplayNames
{
    public const string StyleNotDetermined = "Стиль не визначено";

    public static string Of(RoomStyle style) => style switch
    {
        RoomStyle.SCANDINAVIAN => "Скандинавський",
        RoomStyle.MINIMALISM => "Мінімалізм",
        RoomStyle.MODERN => "Сучасний",
        RoomStyle.CLASSIC => "Класичний",
        RoomStyle.LOFT => "Лофт",
        RoomStyle.BOHO => "Бохо",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Невідомий стиль.")
    };

    public static string Of(RoomStyle? style) => style is { } value ? Of(value) : StyleNotDetermined;

    public static string Of(AnalysisMode mode) => mode switch
    {
        AnalysisMode.BASIC => "Аналіз без ШІ",
        AnalysisMode.LOCAL_AI => "Локальний ШІ",
        AnalysisMode.CLOUD_AI => "Хмарний ШІ",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Невідомий режим.")
    };
}
