using AiDesigner.Models;
using AiDesigner.Services.Analysis;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Analysis;

public class LightingClassifierTests
{
    private static Rgb24 Gray(byte value) => new(value, value, value);

    private static Rgb24[] Mix(Rgb24 a, int count, Rgb24 b) =>
        Enumerable.Repeat(a, count).Concat(Enumerable.Repeat(b, 100 - count)).ToArray();

    // ---------- A1: недостатньо даних ----------

    [Theory]
    [InlineData(0)]    // чорне фото
    [InlineData(10)]   // майже чорне
    [InlineData(250)]  // майже біле
    [InlineData(255)]  // біле
    public void A1_NearlyBlackOrWhitePhoto_IsInsufficient(byte value)
    {
        var pixels = Enumerable.Repeat(Gray(value), 100).ToArray();

        Assert.True(LightingClassifier.IsInsufficientData(pixels));
    }

    [Fact]
    public void A1_MoreThan95PercentExtreme_IsInsufficient()
    {
        var pixels = Mix(Gray(0), 96, Gray(128));

        Assert.True(LightingClassifier.IsInsufficientData(pixels));
    }

    [Fact]
    public void A1_Exactly95PercentExtreme_IsEnough()
    {
        // У ТЗ — «понад 95 %», тож рівно 95 % ще не A1.
        var pixels = Mix(Gray(0), 95, Gray(128));

        Assert.False(LightingClassifier.IsInsufficientData(pixels));
    }

    [Fact]
    public void A1_HalfBlackHalfWhite_IsInsufficient()
    {
        // І чорні, і білі пікселі однаково «крайні».
        var pixels = Mix(Gray(0), 50, Gray(255));

        Assert.True(LightingClassifier.IsInsufficientData(pixels));
    }

    [Fact]
    public void A1_NormalRoomColors_AreEnough()
    {
        var pixels = Mix(new Rgb24(200, 180, 150), 60, new Rgb24(90, 100, 120));

        Assert.False(LightingClassifier.IsInsufficientData(pixels));
    }

    [Fact]
    public void A1_WorksOnRealPngThroughSampler()
    {
        var png = TestImages.Solid(800, 600, Gray(5));

        var pixels = PixelSampler.Sample(png);

        Assert.True(LightingClassifier.IsInsufficientData(pixels));
    }

    // ---------- AC-04: рівень освітленості ----------

    [Theory]
    [InlineData(40, LightingLevel.DARK)]
    [InlineData(84, LightingLevel.DARK)]
    [InlineData(85, LightingLevel.MODERATE)]   // межа 85 — уже «помірне»
    [InlineData(128, LightingLevel.MODERATE)]
    [InlineData(170, LightingLevel.MODERATE)]  // межа 170 — ще «помірне»
    [InlineData(171, LightingLevel.BRIGHT)]
    [InlineData(220, LightingLevel.BRIGHT)]
    public void AC04_LightingLevel_ByMeanLuminance(byte value, LightingLevel expected)
    {
        var pixels = Enumerable.Repeat(Gray(value), 100).ToArray();

        Assert.Equal(expected, LightingClassifier.Classify(pixels));
    }

    [Fact]
    public void AC04_UsesMeanOfAllPixels()
    {
        // Половина 40, половина 200 → середнє 120 → «помірне», хоча жодного «помірного» пікселя немає.
        var pixels = Mix(Gray(40), 50, Gray(200));

        Assert.Equal(LightingLevel.MODERATE, LightingClassifier.Classify(pixels));
    }

    [Fact]
    public void AC04_WorksOnRealPngThroughSampler()
    {
        var png = TestImages.Solid(1200, 800, new Rgb24(230, 225, 215));

        var pixels = PixelSampler.Sample(png);

        Assert.Equal(LightingLevel.BRIGHT, LightingClassifier.Classify(pixels));
    }

    // ---------- Некоректний вхід ----------

    [Fact]
    public void EmptyPixels_Throws()
    {
        Assert.Throws<ArgumentException>(() => LightingClassifier.Classify(Array.Empty<Rgb24>()));
        Assert.Throws<ArgumentException>(() => LightingClassifier.IsInsufficientData(Array.Empty<Rgb24>()));
    }

    // ---------- Точні межі без похибок double (рев'ю Copilot) ----------

    [Theory]
    [InlineData(14, true)]    // яскравість 14 < 15 — майже чорний
    [InlineData(15, false)]   // рівно 15 — ще ні
    [InlineData(240, false)]  // рівно 240 — ще ні
    [InlineData(241, true)]   // 241 > 240 — майже білий
    public void A1_PixelThresholds_AreExact(byte value, bool extreme)
    {
        var pixels = Enumerable.Repeat(Gray(value), 100).ToArray();

        Assert.Equal(extreme, LightingClassifier.IsInsufficientData(pixels));
    }

    [Fact]
    public void AC04_MeanJustBelow85_IsDark()
    {
        // 999 пікселів по 85 і один 84 — середнє 84.999, строго менше 85.
        var pixels = Enumerable.Repeat(Gray(85), 999).Append(Gray(84)).ToArray();

        Assert.Equal(LightingLevel.DARK, LightingClassifier.Classify(pixels));
    }

    [Fact]
    public void AC04_MeanJustAbove170_IsBright()
    {
        var pixels = Enumerable.Repeat(Gray(170), 999).Append(Gray(171)).ToArray();

        Assert.Equal(LightingLevel.BRIGHT, LightingClassifier.Classify(pixels));
    }
}
