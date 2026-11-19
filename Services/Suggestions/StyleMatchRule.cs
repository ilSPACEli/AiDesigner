using AiDesigner.Models;

namespace AiDesigner.Services.Suggestions;

public static class StyleMatchRule
{
    public const int Bonus = 1;

    public const string Reason = "Стиль збігається зі стилем кімнати.";

    public static int Apply(RoomStyle? roomStyle, RoomStyle? itemStyle) =>
        roomStyle is { } room && itemStyle == room ? Bonus : 0;
}
