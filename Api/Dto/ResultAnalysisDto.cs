using AiDesigner.Models;

namespace AiDesigner.Api.Dto;

public sealed record ResultAnalysisDto(
    string? Mode,
    string? Lighting,
    IReadOnlyList<ColorDto>? Colors,
    string? Style,
    decimal? Confidence)
{
    public const decimal MinConfidence = 0.6m;

    public ResultAnalysis? ToModel(out string? error)
    {
        if (Mode is null || !Enum.GetNames<AnalysisMode>().Contains(Mode))
        {
            error = "analysis.mode: очікується BASIC, LOCAL_AI або CLOUD_AI.";
            return null;
        }

        var palette = new AnalysisRequestDto(Colors, Lighting).ToModel(out var paletteError);
        if (palette is null)
        {
            error = "analysis." + paletteError;
            return null;
        }

        if (Style is not null && !Enum.GetNames<RoomStyle>().Contains(Style))
        {
            error = "analysis.style: невідомий стиль.";
            return null;
        }

        var mode = Enum.Parse<AnalysisMode>(Mode);
        if (mode == AnalysisMode.BASIC && (Style is not null || Confidence is not null))
        {
            error = "analysis: для BASIC стиль і впевненість не передаються.";
            return null;
        }

        if (Confidence is < 0 or > 1)
        {
            error = "analysis.confidence: очікується число від 0 до 1.";
            return null;
        }

        if (Style is not null && (Confidence is null || Confidence < MinConfidence))
        {
            error = "analysis.confidence: стиль приймається лише з упевненістю не менше 0,6 (R8).";
            return null;
        }

        error = null;
        return new ResultAnalysis
        {
            Mode = mode,
            Lighting = palette.Lighting,
            Colors = palette.Colors,
            Style = Style is null ? null : Enum.Parse<RoomStyle>(Style),
            Confidence = Confidence
        };
    }
}
