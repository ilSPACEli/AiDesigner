using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Analysis;
using AiDesigner.Services.Stubs;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Analysis;

/// <summary>
/// Тести UC-02 цілком: PhotoAnalyzer окремо й разом із DesignService (стани сесії).
/// </summary>
public class PhotoAnalyzerTests
{
    private static Photo PhotoOf(byte[] content) => new()
    {
        FileName = "room.png",
        Format = "png",
        Width = 1000,
        Height = 600,
        SizeBytes = content.Length,
        Content = content
    };

    // Три «кімнати» з різною освітленістю: кольори не крайні (не A1) і помітно різні.
    private static byte[] DarkRoom() => TestImages.Stripes(1000, 600,
        (new Rgb24(60, 40, 30), 50), (new Rgb24(30, 40, 70), 30), (new Rgb24(70, 60, 50), 20));

    private static byte[] ModerateRoom() => TestImages.Stripes(1000, 600,
        (new Rgb24(180, 120, 90), 50), (new Rgb24(90, 110, 140), 30), (new Rgb24(60, 70, 80), 20));

    private static byte[] BrightRoom() => TestImages.Stripes(1000, 600,
        (new Rgb24(240, 230, 215), 50), (new Rgb24(220, 225, 235), 30), (new Rgb24(200, 210, 190), 20));

    private static DesignService ServiceWithRealAnalyzer() =>
        new(new PhotoAnalyzer(), new StubCatalog(), new StubResultStorage());

    // ---------- AC-03: гама ----------

    [Fact]
    public async Task AC03_ValidPhoto_Gives3To5Colors_SharesSumTo100()
    {
        var outcome = await new PhotoAnalyzer().AnalyzeAsync(PhotoOf(ModerateRoom()));

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        var colors = outcome.Result!.Colors;
        Assert.InRange(colors.Count, 3, 5);
        Assert.Equal(100m, colors.Sum(c => c.Share));
        Assert.All(colors, c => Assert.Matches("^#[0-9A-F]{6}$", c.Hex));
    }

    [Fact]
    public async Task AC03_DominantColor_IsTheLargestArea()
    {
        var outcome = await new PhotoAnalyzer().AnalyzeAsync(PhotoOf(ModerateRoom()));

        // Найбільша смуга (50 %) — теракотова (180, 120, 90) = #B4785A.
        Assert.Equal("#B4785A", outcome.Result!.Colors[0].Hex);
    }

    // ---------- AC-04: освітленість ----------

    [Fact]
    public async Task AC04_DarkRoom_IsDark()
    {
        var outcome = await new PhotoAnalyzer().AnalyzeAsync(PhotoOf(DarkRoom()));

        Assert.Equal(LightingLevel.DARK, outcome.Result!.Lighting);
    }

    [Fact]
    public async Task AC04_ModerateRoom_IsModerate()
    {
        var outcome = await new PhotoAnalyzer().AnalyzeAsync(PhotoOf(ModerateRoom()));

        Assert.Equal(LightingLevel.MODERATE, outcome.Result!.Lighting);
    }

    [Fact]
    public async Task AC04_BrightRoom_IsBright()
    {
        var outcome = await new PhotoAnalyzer().AnalyzeAsync(PhotoOf(BrightRoom()));

        Assert.Equal(LightingLevel.BRIGHT, outcome.Result!.Lighting);
    }

    // ---------- A1 і розширення A1 ----------

    [Fact]
    public async Task A1_NearlyBlackPhoto_IsInsufficientData()
    {
        var png = TestImages.Solid(800, 600, new Rgb24(5, 5, 8));

        var outcome = await new PhotoAnalyzer().AnalyzeAsync(PhotoOf(png));

        Assert.Equal(AnalysisStatus.InsufficientData, outcome.Status);
        Assert.Null(outcome.Result);
        Assert.False(string.IsNullOrEmpty(outcome.Message));
    }

    [Fact]
    public async Task A1Ext_SolidGrayWall_IsInsufficientData()
    {
        var png = TestImages.Solid(800, 600, new Rgb24(150, 150, 150));

        var outcome = await new PhotoAnalyzer().AnalyzeAsync(PhotoOf(png));

        Assert.Equal(AnalysisStatus.InsufficientData, outcome.Status);
    }

    // ---------- Відтворюваність (потрібна для AC-06 у UC-03) ----------

    [Fact]
    public async Task SamePhoto_GivesSameResult()
    {
        var analyzer = new PhotoAnalyzer();
        var photo = PhotoOf(ModerateRoom());

        var first = (await analyzer.AnalyzeAsync(photo)).Result!;
        var second = (await analyzer.AnalyzeAsync(photo)).Result!;

        Assert.Equal(first.Lighting, second.Lighting);
        Assert.Equal(first.Colors.Select(c => (c.Hex, c.Hue, c.Share)), second.Colors.Select(c => (c.Hex, c.Hue, c.Share)));
    }

    // ---------- Разом із DesignService: стани сесії ----------

    [Fact]
    public async Task AC05_SuccessfulAnalysis_SessionIsAnalyzed_WithPaletteAndLighting()
    {
        var service = ServiceWithRealAnalyzer();
        service.LoadPhoto(PhotoOf(BrightRoom()));

        var outcome = await service.AnalyzeAsync();

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
        Assert.InRange(service.Session.Analysis!.Colors.Count, 3, 5);
        Assert.Equal(LightingLevel.BRIGHT, service.Session.Analysis.Lighting);
        Assert.False(service.Session.IsAnalyzing);
    }

    [Fact]
    public async Task A1_InsufficientData_SessionStaysPhotoLoaded_WithoutResult()
    {
        var service = ServiceWithRealAnalyzer();
        service.LoadPhoto(PhotoOf(TestImages.Solid(800, 600, new Rgb24(255, 255, 255))));

        var outcome = await service.AnalyzeAsync();

        Assert.Equal(AnalysisStatus.InsufficientData, outcome.Status);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
        Assert.Null(service.Session.Analysis);
        Assert.False(service.Session.IsAnalyzing);
    }

    [Fact]
    public async Task E1_CorruptFile_ReturnsError_SessionStaysPhotoLoaded()
    {
        var service = ServiceWithRealAnalyzer();
        service.LoadPhoto(PhotoOf(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 }));   // «PNG», але битий

        var outcome = await service.AnalyzeAsync();

        Assert.Equal(AnalysisStatus.Error, outcome.Status);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
        Assert.Null(service.Session.Analysis);
        Assert.False(service.Session.IsAnalyzing);
    }

    [Fact]
    public async Task CancelledToken_StopsAnalysis()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new PhotoAnalyzer().AnalyzeAsync(PhotoOf(ModerateRoom()), cts.Token));
    }

    // ---------- E1: завелике фото (рев'ю Copilot) ----------

    [Fact]
    public async Task E1_TooLargePhoto_ReturnsErrorWithReason()
    {
        var analyzer = new PhotoAnalyzer { MaxPixels = 100_000 };   // 1000×600 = 600 000 пікселів

        var outcome = await analyzer.AnalyzeAsync(PhotoOf(ModerateRoom()));

        Assert.Equal(AnalysisStatus.Error, outcome.Status);
        Assert.Contains("завелике", outcome.Message);
    }
}
