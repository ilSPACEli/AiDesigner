using AiDesigner.Services.Analysis;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Analysis;

public class KMeansTests
{
    private static readonly Rgb24 Red = new(255, 0, 0);
    private static readonly Rgb24 Green = new(0, 255, 0);
    private static readonly Rgb24 Blue = new(0, 0, 255);

    /// <summary>Набір пікселів: кожен колір повторено заданою кількістю разів.</summary>
    private static Rgb24[] Pixels(params (Rgb24 Color, int Count)[] parts) =>
        parts.SelectMany(p => Enumerable.Repeat(p.Color, p.Count)).ToArray();

    private static List<ColorCluster> NonEmpty(IReadOnlyList<ColorCluster> clusters) =>
        clusters.Where(c => c.Count > 0).ToList();

    [Fact]
    public void ThreeColors_AreFoundExactly_WithTheirPixelCounts()
    {
        var pixels = Pixels((Red, 500), (Green, 300), (Blue, 200));

        var clusters = NonEmpty(KMeans.Cluster(pixels));

        Assert.Equal(3, clusters.Count);
        Assert.Contains(new ColorCluster(Red, 500), clusters);
        Assert.Contains(new ColorCluster(Green, 300), clusters);
        Assert.Contains(new ColorCluster(Blue, 200), clusters);
    }

    [Fact]
    public void SolidColor_GivesSingleCluster()
    {
        var pixels = Pixels((new Rgb24(120, 130, 140), 1000));

        var clusters = NonEmpty(KMeans.Cluster(pixels));

        Assert.Single(clusters);
        Assert.Equal(1000, clusters[0].Count);
    }

    [Fact]
    public void ShadesOfOneColor_AreGroupedTogether()
    {
        // Дві групи відтінків: червонуваті й синюваті, по 6 відтінків у кожній.
        var reddish = Enumerable.Range(0, 6).Select(i => new Rgb24((byte)(200 + i * 5), (byte)(40 + i), 40));
        var bluish = Enumerable.Range(0, 6).Select(i => new Rgb24(40, (byte)(40 + i), (byte)(200 + i * 5)));
        var pixels = reddish.SelectMany(c => Enumerable.Repeat(c, 50))
            .Concat(bluish.SelectMany(c => Enumerable.Repeat(c, 30)))
            .ToArray();

        var clusters = NonEmpty(KMeans.Cluster(pixels, k: 2));

        Assert.Equal(2, clusters.Count);
        var red = clusters.Single(c => c.Center.R > c.Center.B);
        var blue = clusters.Single(c => c.Center.B > c.Center.R);
        Assert.Equal(300, red.Count);
        Assert.Equal(180, blue.Count);
    }

    [Fact]
    public void SameInput_GivesSameResult()
    {
        // Без випадковості результат має бути однаковим щоразу — інакше палітра «стрибала» б.
        var pixels = Pixels((Red, 400), (new Rgb24(250, 240, 220), 350), (new Rgb24(60, 60, 70), 250));

        var first = KMeans.Cluster(pixels);
        var second = KMeans.Cluster(pixels);

        Assert.Equal(first, second);
    }

    [Fact]
    public void CountsAddUpToAllPixels()
    {
        var pixels = Pixels((Red, 123), (Green, 456), (Blue, 78), (new Rgb24(128, 128, 128), 90));

        var clusters = KMeans.Cluster(pixels);

        Assert.Equal(pixels.Length, clusters.Sum(c => c.Count));
        Assert.True(clusters.Count <= KMeans.DefaultK);
    }

    [Fact]
    public void CancelledToken_Throws()
    {
        var pixels = Pixels((Red, 100), (Blue, 100));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => KMeans.Cluster(pixels, cancellationToken: cts.Token));
    }

    [Fact]
    public void EmptyPixels_Throws()
    {
        Assert.Throws<ArgumentException>(() => KMeans.Cluster(Array.Empty<Rgb24>()));
    }
}
