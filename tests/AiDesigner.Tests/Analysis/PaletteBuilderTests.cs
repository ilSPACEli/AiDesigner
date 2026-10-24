using AiDesigner.Models;
using AiDesigner.Services.Analysis;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Analysis;

public class PaletteBuilderTests
{
    private static readonly Rgb24 Red = new(255, 0, 0);
    private static readonly Rgb24 Green = new(0, 255, 0);
    private static readonly Rgb24 Blue = new(0, 0, 255);
    private static readonly Rgb24 Beige = new(216, 195, 165);
    private static readonly Rgb24 Dark = new(50, 45, 40);

    private static ColorCluster C(Rgb24 color, int count) => new(color, count);

    private static IReadOnlyList<DominantColor> Build(params ColorCluster[] clusters)
    {
        Assert.True(PaletteBuilder.TryBuild(clusters, out var palette));
        return palette;
    }

    // ---------- AC-03: 3–5 кольорів, сума 100 % ----------

    [Fact]
    public void AC03_FiveColors_SharesSumToExactly100()
    {
        var palette = Build(C(Red, 333), C(Green, 271), C(Blue, 197), C(Beige, 123), C(Dark, 76));

        Assert.Equal(5, palette.Count);
        Assert.Equal(100m, palette.Sum(c => c.Share));
    }

    [Fact]
    public void AC03_EqualThirds_StillSumTo100()
    {
        // 33.3 × 3 = 99.9 — тому одна частка отримує зайву десяту: 33.4 + 33.3 + 33.3.
        var palette = Build(C(Red, 100), C(Green, 100), C(Blue, 100));

        Assert.Equal(new[] { 33.4m, 33.3m, 33.3m }, palette.Select(c => c.Share));
    }

    [Fact]
    public void AC03_ClustersBelow5Percent_AreDropped_AndSharesRecalculated()
    {
        // 15 і 5 пікселів із 1000 — 1.5 % і 0.5 %, відкидаються; частки рахуються серед 980.
        var palette = Build(C(Beige, 600), C(Dark, 300), C(Blue, 80), C(Red, 15), C(Green, 5));

        Assert.Equal(new[] { "#D8C3A5", "#322D28", "#0000FF" }, palette.Select(c => c.Hex));
        Assert.Equal(new[] { 61.2m, 30.6m, 8.2m }, palette.Select(c => c.Share));
    }

    [Fact]
    public void AC03_KeepsAtLeastThreeColors_EvenIfSmall()
    {
        // 4 % і 1 % — менше порогу, але без них лишився б один колір.
        var palette = Build(C(Beige, 950), C(Dark, 40), C(Blue, 10));

        Assert.Equal(3, palette.Count);
        Assert.Equal(100m, palette.Sum(c => c.Share));
    }

    [Fact]
    public void AC03_NeverMoreThanFiveColors()
    {
        var palette = Build(
            C(Red, 200), C(Green, 180), C(Blue, 160), C(Beige, 140),
            C(Dark, 120), C(new Rgb24(255, 255, 0), 110), C(new Rgb24(0, 255, 255), 90));

        Assert.Equal(5, palette.Count);
        Assert.Equal(100m, palette.Sum(c => c.Share));
    }

    [Fact]
    public void AC03_ColorsAreSortedByShare_WithHexAndHue()
    {
        var palette = Build(C(Blue, 200), C(Red, 500), C(Green, 300));

        Assert.Equal(new[] { "#FF0000", "#00FF00", "#0000FF" }, palette.Select(c => c.Hex));
        Assert.Equal(new[] { 0, 120, 240 }, palette.Select(c => c.Hue));
        Assert.Equal(new[] { 50m, 30m, 20m }, palette.Select(c => c.Share));
    }

    [Fact]
    public void EmptyClusters_AreIgnored()
    {
        var palette = Build(C(Red, 500), C(Dark, 0), C(Green, 300), C(Blue, 200), C(Beige, 0));

        Assert.Equal(3, palette.Count);
    }

    // ---------- Склеювання майже однакових кольорів ----------

    [Fact]
    public void NearlyIdenticalColors_AreMerged()
    {
        // (200,100,50) і (205,102,52) — відстань ~5.7, це один колір.
        var palette = Build(
            C(new Rgb24(200, 100, 50), 500), C(new Rgb24(205, 102, 52), 300), C(Blue, 200), C(Green, 100));

        Assert.Equal(3, palette.Count);
        // Центр зважений: (200·500 + 205·300) / 800 ≈ 202.
        Assert.Equal("#CA6533", palette[0].Hex);
        Assert.Equal(72.7m, palette[0].Share);
    }

    // ---------- Розширення A1: кольорів менше трьох ----------

    [Fact]
    public void A1Ext_SolidColor_IsNotEnough()
    {
        Assert.False(PaletteBuilder.TryBuild(new[] { C(Beige, 1000) }, out var palette));
        Assert.Empty(palette);
    }

    [Fact]
    public void A1Ext_TwoColors_AreNotEnough()
    {
        Assert.False(PaletteBuilder.TryBuild(new[] { C(Beige, 600), C(Dark, 400) }, out _));
    }

    [Fact]
    public void A1Ext_OnlyNearDuplicates_AreNotEnough()
    {
        // П'ять кластерів, але всі — один і той самий сірий із шумом.
        var clusters = new[]
        {
            C(new Rgb24(128, 128, 128), 300), C(new Rgb24(131, 129, 127), 250),
            C(new Rgb24(125, 127, 130), 200), C(new Rgb24(130, 126, 128), 150),
            C(new Rgb24(127, 131, 129), 100)
        };

        Assert.False(PaletteBuilder.TryBuild(clusters, out _));
    }

    // ---------- Наскрізно: PNG → пікселі → k-means → палітра ----------

    [Fact]
    public void AC03_StripesPhoto_GivesTheirColorsAndShares()
    {
        var png = TestImages.Stripes(1000, 300, (Red, 50), (Green, 30), (Blue, 20));

        var pixels = PixelSampler.Sample(png);
        var clusters = KMeans.Cluster(pixels);
        Assert.True(PaletteBuilder.TryBuild(clusters, out var palette));

        Assert.Equal(3, palette.Count);
        Assert.Equal(100m, palette.Sum(c => c.Share));
        Assert.Equal(new[] { 0, 120, 240 }, palette.Select(c => c.Hue));
        Assert.InRange(palette[0].Share, 48m, 52m);
        Assert.InRange(palette[1].Share, 28m, 32m);
        Assert.InRange(palette[2].Share, 18m, 22m);
    }
}
