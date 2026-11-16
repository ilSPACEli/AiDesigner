using AiDesigner.Models;
using AiDesigner.Services.Style;
using AiDesigner.Tests.Analysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Style;

// Контрольні приклади R8 — картка US-2 (#20).
public class StyleAcceptanceRuleTests
{
    [Theory]
    [InlineData("""{"style":"Скандинавський","confidence":0.85}""", RoomStyle.SCANDINAVIAN, 0.85)]
    [InlineData("""{"style":"SCANDINAVIAN","confidence":0.85}""", RoomStyle.SCANDINAVIAN, 0.85)]
    [InlineData("""{"style":"loft","confidence":0.7}""", RoomStyle.LOFT, 0.7)]
    [InlineData("""{"style":"BOHO","confidence":0.60}""", RoomStyle.BOHO, 0.6)]
    [InlineData("""{"style":"MODERN","confidence":1}""", RoomStyle.MODERN, 1.0)]
    [InlineData("```json\n{\"style\":\"CLASSIC\",\"confidence\":0.9}\n```", RoomStyle.CLASSIC, 0.9)]
    public void R8_Accepted(string answer, RoomStyle style, double confidence)
    {
        var decision = StyleAcceptanceRule.Apply(answer);

        Assert.Equal(style, decision.Style);
        Assert.Equal((decimal)confidence, decision.Confidence);
    }

    [Theory]
    [InlineData("""{"style":"LOFT","confidence":0.59}""", 0.59)]
    [InlineData("""{"style":"Футуризм","confidence":0.9}""", 0.9)]
    [InlineData("""{"style":"","confidence":0.9}""", 0.9)]
    [InlineData("""{"confidence":0.9}""", 0.9)]
    [InlineData("""{"style":"LOFT","confidence":0}""", 0.0)]
    public void R8_NotDetermined_KeepsValidConfidence(string answer, double confidence)
    {
        var decision = StyleAcceptanceRule.Apply(answer);

        Assert.Null(decision.Style);
        Assert.Equal((decimal)confidence, decision.Confidence);
    }

    [Theory]
    [InlineData("""{"style":"LOFT","confidence":1.2}""")]
    [InlineData("""{"style":"LOFT","confidence":-0.1}""")]
    [InlineData("""{"style":"LOFT","confidence":"0.9"}""")]
    [InlineData("""{"style":"LOFT"}""")]
    [InlineData("""{"style":"LOFT","confidence":0.9""")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void R8_InvalidOrBrokenAnswer_NotDetermined_NoConfidence(string? answer)
    {
        var decision = StyleAcceptanceRule.Apply(answer);

        Assert.Null(decision.Style);
        Assert.Null(decision.Confidence);
    }

    [Fact]
    public void Prompt_ListsEveryStyle()
    {
        Assert.All(Enum.GetNames<RoomStyle>(), name => Assert.Contains(name, StylePrompt.Text));
    }

    [Fact]
    public void Image_LargePhoto_IsScaledDownToJpeg()
    {
        var photo = TestImages.Solid(3000, 2000, new Rgb24(200, 100, 50));

        var prepared = StyleImage.Prepare(photo);

        var info = Image.Identify(prepared);
        Assert.Equal(StyleImage.MaxSide, info.Width);
        Assert.InRange(info.Height, 682, 683);
        Assert.Equal("JPEG", info.Metadata.DecodedImageFormat!.Name);
    }

    [Fact]
    public void Image_SmallPhoto_KeepsSize()
    {
        var prepared = StyleImage.Prepare(TestImages.Solid(640, 480, new Rgb24(10, 20, 30)));

        var info = Image.Identify(prepared);
        Assert.Equal((640, 480), (info.Width, info.Height));
    }
}
