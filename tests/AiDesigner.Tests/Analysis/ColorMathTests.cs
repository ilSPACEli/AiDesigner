using AiDesigner.Services.Analysis;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Analysis;

public class ColorMathTests
{
    // ---------- Яскравість ----------

    [Theory]
    [InlineData(0, 0, 0, 0)]          // чорний
    [InlineData(255, 255, 255, 255)]  // білий
    [InlineData(128, 128, 128, 128)]  // сірий: яскравість = значенню складових
    [InlineData(0, 255, 0, 149.685)]  // чистий зелений: 0.587 · 255
    [InlineData(0, 0, 255, 29.07)]    // чистий синій: 0.114 · 255 — темний для ока
    public void Luminance_UsesWeightedFormula(byte r, byte g, byte b, double expected)
    {
        Assert.Equal(expected, ColorMath.Luminance(new Rgb24(r, g, b)), precision: 3);
    }

    // ---------- Відтінок ----------

    [Theory]
    [InlineData(255, 0, 0, 0)]      // червоний
    [InlineData(255, 255, 0, 60)]   // жовтий
    [InlineData(0, 255, 0, 120)]    // зелений
    [InlineData(0, 255, 255, 180)]  // блакитний
    [InlineData(0, 0, 255, 240)]    // синій
    [InlineData(255, 0, 255, 300)]  // пурпуровий
    [InlineData(255, 128, 0, 30)]   // помаранчевий
    public void Hue_PrimaryAndSecondaryColors(byte r, byte g, byte b, int expected)
    {
        Assert.Equal(expected, ColorMath.Hue(new Rgb24(r, g, b)));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(128, 128, 128)]
    [InlineData(255, 255, 255)]
    public void Hue_OfGray_IsZero(byte r, byte g, byte b)
    {
        Assert.Equal(0, ColorMath.Hue(new Rgb24(r, g, b)));
    }

    [Fact]
    public void Hue_JustBelow360_WrapsToZero()
    {
        // Майже червоний з краплею синього: ~359.8° → 0°, а не 360°.
        Assert.Equal(0, ColorMath.Hue(new Rgb24(255, 0, 1)));
    }

    [Fact]
    public void Hue_IsAlwaysInRange0To359()
    {
        for (var r = 0; r <= 255; r += 15)
            for (var g = 0; g <= 255; g += 15)
                for (var b = 0; b <= 255; b += 15)
                {
                    var hue = ColorMath.Hue(new Rgb24((byte)r, (byte)g, (byte)b));
                    Assert.InRange(hue, 0, 359);
                }
    }

    // ---------- Hex ----------

    [Theory]
    [InlineData(0, 0, 0, "#000000")]
    [InlineData(255, 255, 255, "#FFFFFF")]
    [InlineData(255, 0, 128, "#FF0080")]
    [InlineData(10, 171, 205, "#0AABCD")]
    public void ToHex_FormatsUppercaseRrggbb(byte r, byte g, byte b, string expected)
    {
        Assert.Equal(expected, ColorMath.ToHex(new Rgb24(r, g, b)));
    }

    [Theory]
    [InlineData(85, 85, 85, 85_000)]
    [InlineData(170, 170, 170, 170_000)]
    [InlineData(0, 255, 0, 149_685)]
    public void LuminanceX1000_IsExactInteger(byte r, byte g, byte b, int expected)
    {
        Assert.Equal(expected, ColorMath.LuminanceX1000(new Rgb24(r, g, b)));
    }
}
