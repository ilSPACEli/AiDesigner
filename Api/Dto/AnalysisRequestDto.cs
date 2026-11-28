using AiDesigner.Models;

namespace AiDesigner.Api.Dto;

public sealed record ColorDto(string? Hex, int Hue, decimal Share);

public sealed record AnalysisRequestDto(IReadOnlyList<ColorDto>? Colors, string? Lighting, string? Style = null)
{
    public const int MinColors = 3;
    public const int MaxColors = 5;

    public static AnalysisRequestDto From(AnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        return new AnalysisRequestDto(
            analysis.Colors.Select(c => new ColorDto(c.Hex, c.Hue, c.Share)).ToList(),
            analysis.Lighting.ToString(),
            analysis.Style?.ToString());
    }

    public AnalysisResult? ToModel(out string? error)
    {
        if (Lighting is null || !Enum.GetNames<LightingLevel>().Contains(Lighting))
        {
            error = "lighting: очікується DARK, MODERATE або BRIGHT.";
            return null;
        }

        if (Colors is null || Colors.Count is < MinColors or > MaxColors)
        {
            error = $"colors: очікується від {MinColors} до {MaxColors} кольорів.";
            return null;
        }

        for (var i = 0; i < Colors.Count; i++)
        {
            var color = Colors[i];
            if (color is null || !HexColor.IsValid(color.Hex))
            {
                error = $"colors[{i}].hex: очікується формат #RRGGBB.";
                return null;
            }
            if (color.Hue is < 0 or > 359)
            {
                error = $"colors[{i}].hue: очікується число від 0 до 359.";
                return null;
            }
            if (color.Share is < 0 or > 100)
            {
                error = $"colors[{i}].share: очікується відсоток від 0 до 100.";
                return null;
            }
        }

        if (Style is not null && !Enum.GetNames<RoomStyle>().Contains(Style))
        {
            error = "style: невідомий стиль.";
            return null;
        }

        error = null;
        var lighting = Enum.Parse<LightingLevel>(Lighting);
        return new AnalysisResult
        {
            Colors = Colors.Select(c => new DominantColor { Hex = c.Hex!, Hue = c.Hue, Share = c.Share }).ToList(),
            Lighting = lighting,
            AnalyzedAt = DateTime.UtcNow,
            Style = Style is null ? null : Enum.Parse<RoomStyle>(Style)
        };
    }
}
