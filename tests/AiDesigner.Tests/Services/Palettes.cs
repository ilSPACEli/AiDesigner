using AiDesigner.Models;
using AiDesigner.Services.Analysis;

namespace AiDesigner.Tests.Services;

// Палітри для тестів добору: відтінок рахується з hex, як в аналізаторі.
public static class Palettes
{
    public static AnalysisResult Of(LightingLevel lighting, params (string Hex, decimal Share)[] colors) => new()
    {
        Lighting = lighting,
        AnalyzedAt = DateTime.UtcNow,
        Colors = colors
            .Select(c => new DominantColor { Hex = c.Hex, Hue = (int)Math.Round(ColorHelper.GetHue(c.Hex)) % 360, Share = c.Share })
            .ToList()
    };

    // Типові палітри кімнат із 3–5 кольорами (як від аналізатора)
    public static IReadOnlyDictionary<string, (string Hex, decimal Share)[]> Rooms { get; } =
        new Dictionary<string, (string, decimal)[]>
        {
            ["теракотова"] = new[] { ("#D7D2C7", 49.9m), ("#954A28", 17.1m), ("#C7775E", 16.8m), ("#814A27", 16.2m) },
            ["сіра спальня"] = new[] { ("#A89E9D", 27.9m), ("#DBD4CF", 27.9m), ("#7C787E", 19.3m), ("#57515D", 13.5m), ("#2A272F", 11.4m) },
            ["синя"] = new[] { ("#3B5BA5", 45m), ("#A7B8D8", 35m), ("#F0F0F0", 20m) },
            ["зелена"] = new[] { ("#4F7942", 50m), ("#C9D6A3", 30m), ("#2E2E2E", 20m) }
        };

    public static IEnumerable<object[]> RoomsAtAllLevels() =>
        from name in Rooms.Keys
        from level in Enum.GetValues<LightingLevel>()
        select new object[] { name, level };
}
