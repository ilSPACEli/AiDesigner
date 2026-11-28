using AiDesigner.Models;

namespace AiDesigner.Tests;

// Спільна перевірка #RRGGBB (#111, SonarQube S6444).
public class HexColorTests
{
    [Theory]
    [InlineData("#D8C3A5")]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    public void Valid_RRGGBB_Accepted(string value)
    {
        Assert.True(HexColor.IsValid(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("D8C3A5")]
    [InlineData("#D8C3A")]
    [InlineData("#D8C3A55")]
    [InlineData("#G8C3A5")]
    [InlineData(" #D8C3A5")]
    public void Invalid_Rejected(string? value)
    {
        Assert.False(HexColor.IsValid(value));
    }

    [Fact]
    public void LongInput_RejectedQuickly()
    {
        var value = "#" + new string('A', 100_000);

        Assert.False(HexColor.IsValid(value));
    }
}
