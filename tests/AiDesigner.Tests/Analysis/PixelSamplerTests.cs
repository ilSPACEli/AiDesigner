using AiDesigner.Services.Analysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Analysis;

public class PixelSamplerTests
{
    private static readonly Rgb24 Red = new(255, 0, 0);

    [Fact]
    public void WideImage_IsDownscaledTo200px_KeepingAspectRatio()
    {
        var png = TestImages.Solid(800, 400, Red);

        var pixels = PixelSampler.Sample(png);

        Assert.Equal(200 * 100, pixels.Length);
    }

    [Fact]
    public void NarrowImage_IsNotUpscaled()
    {
        var png = TestImages.Solid(100, 50, Red);

        var pixels = PixelSampler.Sample(png);

        Assert.Equal(100 * 50, pixels.Length);
    }

    [Fact]
    public void Downscaling_KeepsSolidColorExact()
    {
        var png = TestImages.Solid(1000, 600, Red);

        var pixels = PixelSampler.Sample(png);

        Assert.All(pixels, p => Assert.Equal(Red, p));
    }

    [Fact]
    public void NotAnImage_Throws()
    {
        var garbage = new byte[] { 1, 2, 3, 4, 5 };

        Assert.ThrowsAny<ImageFormatException>(() => PixelSampler.Sample(garbage));
    }

    [Fact]
    public void CancelledToken_Throws()
    {
        var png = TestImages.Solid(400, 300, Red);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => PixelSampler.Sample(png, cts.Token));
    }

    // ---------- Захист від завеликих зображень (рев'ю Copilot) ----------

    [Fact]
    public void TooManyPixels_ThrowsBeforeFullDecoding()
    {
        var png = TestImages.Solid(400, 300, Red);   // 120 000 пікселів

        var ex = Assert.Throws<ImageTooLargeException>(() => PixelSampler.Sample(png, maxPixels: 100_000));
        Assert.Equal(400, ex.Width);
        Assert.Equal(300, ex.Height);
    }

    [Fact]
    public void ExactlyMaxPixels_IsAllowed()
    {
        var png = TestImages.Solid(400, 250, Red);   // рівно 100 000 пікселів

        var pixels = PixelSampler.Sample(png, maxPixels: 100_000);

        Assert.Equal(200 * 125, pixels.Length);
    }
}
