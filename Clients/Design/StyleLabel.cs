using AiDesigner.Models;

namespace AiDesigner.Clients.Design;

public sealed record StyleLabel(string Text, bool IsDetermined)
{
    public const string Prefix = "Стиль: ";

    public static StyleLabel? For(AnalysisMode mode, RoomStyle? style)
    {
        if (mode == AnalysisMode.BASIC)
            return null;

        return style is { } value
            ? new StyleLabel(Prefix + DisplayNames.Of(value), true)
            : new StyleLabel(DisplayNames.StyleNotDetermined, false);
    }

    public static StyleLabel? For(string? mode, string? style)
    {
        if (!TryParse<AnalysisMode>(mode, out var parsedMode))
            return null;

        RoomStyle? parsedStyle = TryParse<RoomStyle>(style, out var value) ? value : null;
        return For(parsedMode, parsedStyle);
    }

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
