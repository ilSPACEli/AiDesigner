using System.Text.Json;
using AiDesigner.Models;

namespace AiDesigner.Services.Style;

public sealed record StyleDecision(RoomStyle? Style, decimal? Confidence);

public static class StyleAcceptanceRule
{
    public const decimal MinConfidence = 0.6m;

    public static StyleDecision Apply(string? answer)
    {
        if (!TryRead(answer, out var styleText, out var confidence))
            return new StyleDecision(null, null);

        var validConfidence = confidence is >= 0 and <= 1 ? confidence : null;
        var style = ParseStyle(styleText);

        return style is not null && validConfidence >= MinConfidence
            ? new StyleDecision(style, validConfidence)
            : new StyleDecision(null, validConfidence);
    }

    private static bool TryRead(string? answer, out string? style, out decimal? confidence)
    {
        style = null;
        confidence = null;
        if (string.IsNullOrWhiteSpace(answer))
            return false;

        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        try
        {
            using var json = JsonDocument.Parse(answer[start..(end + 1)]);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (root.TryGetProperty("style", out var s) && s.ValueKind == JsonValueKind.String)
                style = s.GetString();
            if (root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetDecimal(out var value))
                confidence = value;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static RoomStyle? ParseStyle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var value = text.Trim();
        foreach (var style in Enum.GetValues<RoomStyle>())
        {
            if (string.Equals(value, style.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, DisplayNames.Of(style), StringComparison.OrdinalIgnoreCase))
                return style;
        }
        return null;
    }
}
