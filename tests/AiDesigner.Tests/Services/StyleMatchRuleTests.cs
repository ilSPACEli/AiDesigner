using AiDesigner.Models;
using AiDesigner.Services.Suggestions;

namespace AiDesigner.Tests.Services;

// Контрольні приклади R6 — картка US-2 (#20).
public class StyleMatchRuleTests
{
    [Theory]
    [InlineData(RoomStyle.LOFT, RoomStyle.LOFT, 1)]
    [InlineData(RoomStyle.LOFT, RoomStyle.CLASSIC, 0)]
    [InlineData(null, RoomStyle.LOFT, 0)]
    [InlineData(RoomStyle.LOFT, null, 0)]
    [InlineData(null, null, 0)]
    public void R6_Bonus(RoomStyle? room, RoomStyle? item, int bonus)
    {
        Assert.Equal(bonus, StyleMatchRule.Apply(room, item));
    }
}
