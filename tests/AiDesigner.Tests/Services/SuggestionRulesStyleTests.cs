using AiDesigner.Models;
using AiDesigner.Services.Suggestions;

namespace AiDesigner.Tests.Services;

// R6 у доборі (AC-29): предмет зі стилем кімнати — вище за предмет з іншим стилем і тим самим балом R4.
public class SuggestionRulesStyleTests
{
    private static CatalogItem Item(string id, ItemType type, string hex, RoomStyle? style) => new()
    {
        Id = id,
        Name = id,
        Type = type,
        ImageFile = $"images/catalog/{id}.png",
        MainColor = hex,
        SuitableLightings = new[] { LightingLevel.MODERATE },
        LicenseSource = "test",
        Style = style
    };

    private static AnalysisResult Room(RoomStyle? style) => new()
    {
        Lighting = LightingLevel.MODERATE,
        Colors = new[]
        {
            new DominantColor { Hex = "#3B5BA5", Hue = 222, Share = 45m },
            new DominantColor { Hex = "#A7B8D8", Hue = 219, Share = 35m },
            new DominantColor { Hex = "#F0F0F0", Hue = 0, Share = 20m }
        },
        Style = style
    };

    // Обидва нейтральні — бал R4 = 1. За id «a-classic» іде першим.
    private static readonly CatalogItem[] SameScore =
    {
        Item("a-classic", ItemType.ARMCHAIR, "#9E9E9E", RoomStyle.CLASSIC),
        Item("b-loft", ItemType.SOFA, "#808080", RoomStyle.LOFT)
    };

    [Fact]
    public void AC29_LoftRoom_LoftItemAboveOtherStyleWithSameScore()
    {
        var result = new SuggestionRules(SameScore).Select(Room(RoomStyle.LOFT), 4);

        Assert.Equal(new[] { "b-loft", "a-classic" }, result.Select(s => s.Item.Id));
        Assert.Equal(new[] { 2, 1 }, result.Select(s => s.Score));
        Assert.EndsWith(StyleMatchRule.Reason, result[0].Reason);
        Assert.DoesNotContain(StyleMatchRule.Reason, result[1].Reason);
    }

    [Fact]
    public void AC29_StyleNotDetermined_SameAsWithoutStyle()
    {
        var result = new SuggestionRules(SameScore).Select(Room(null), 4);

        Assert.Equal(new[] { "a-classic", "b-loft" }, result.Select(s => s.Item.Id));
        Assert.Equal(new[] { 1, 1 }, result.Select(s => s.Score));
        Assert.All(result, s => Assert.DoesNotContain(StyleMatchRule.Reason, s.Reason));
    }

    [Fact]
    public void R6_ItemWithoutStyle_ScoreUnchanged()
    {
        var items = new[] { Item("a-none", ItemType.SOFA, "#9E9E9E", null) };

        var result = new SuggestionRules(items).Select(Room(RoomStyle.LOFT), 4);

        Assert.Equal(1, Assert.Single(result).Score);
    }

    [Fact]
    public void R6_DoesNotApplyToPartialMatch()
    {
        // Червоний не гармонує із синьою кімнатою — потрапляє лише як часткова відповідність (R5).
        var items = new[] { Item("a-red-loft", ItemType.SOFA, "#CC0000", RoomStyle.LOFT) };

        var result = new SuggestionRules(items).Select(Room(RoomStyle.LOFT), 4);

        var suggestion = Assert.Single(result);
        Assert.Equal(0, suggestion.Score);
        Assert.Equal(SuggestionRules.PartialMatchReason, suggestion.Reason);
    }
}
