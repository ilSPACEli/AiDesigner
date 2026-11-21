using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Stubs;
using AiDesigner.Services.Style;
using AiDesigner.Tests.Analysis;
using AiDesigner.Tests.Clients;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Style;

public class DesignServiceStyleTests
{
    private sealed class FakeStyleAnalyzer(
        AnalysisMode mode,
        Func<byte[], CancellationToken, Task<string>> answer,
        ModeAvailability? availability = null) : IStyleAnalyzer
    {
        public int Calls { get; private set; }

        public byte[]? LastImage { get; private set; }

        public AnalysisMode Mode => mode;

        public Task<ModeAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(availability ?? ModeAvailability.Available(mode));

        public Task<string> AnalyzeStyleAsync(byte[] image, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastImage = image;
            return answer(image, cancellationToken);
        }
    }

    private static FakeStyleAnalyzer Answering(AnalysisMode mode, string text) =>
        new(mode, (_, _) => Task.FromResult(text));

    private static Photo CreatePhoto() => new()
    {
        FileName = "room.png", Format = "png", Width = 640, Height = 480, SizeBytes = 1000,
        Content = TestImages.Solid(640, 480, new Rgb24(180, 120, 90))
    };

    private static DesignService CreateService(RecordingGalleryClient? gallery = null, params IStyleAnalyzer[] analyzers)
    {
        var service = new DesignService(new StubPhotoAnalyzer(), new StubSuggestionClient(),
            gallery ?? new RecordingGalleryClient(), analyzers);
        service.LoadPhoto(CreatePhoto());
        return service;
    }

    // ---------- AC-26: доступність режимів ----------

    [Fact]
    public async Task AC26_NoAiConfigured_OnlyBasicAvailable()
    {
        var service = CreateService();

        var modes = await service.GetModesAsync();

        Assert.Equal(new[] { AnalysisMode.BASIC, AnalysisMode.LOCAL_AI, AnalysisMode.CLOUD_AI }, modes.Select(m => m.Mode));
        Assert.True(modes[0].IsAvailable);
        Assert.All(modes.Skip(1), m =>
        {
            Assert.False(m.IsAvailable);
            Assert.False(string.IsNullOrWhiteSpace(m.Reason));
        });
    }

    [Fact]
    public async Task AC26_UnavailableProvider_ReasonPassedThrough()
    {
        var cloud = new FakeStyleAnalyzer(AnalysisMode.CLOUD_AI, (_, _) => Task.FromResult(""),
            ModeAvailability.Unavailable(AnalysisMode.CLOUD_AI, "Немає ключа."));
        var service = CreateService(null, cloud);

        var modes = await service.GetModesAsync();

        var cloudMode = Assert.Single(modes, m => m.Mode == AnalysisMode.CLOUD_AI);
        Assert.False(cloudMode.IsAvailable);
        Assert.Equal("Немає ключа.", cloudMode.Reason);
    }

    [Fact]
    public async Task AvailableProvider_ModeAvailable()
    {
        var service = CreateService(null, Answering(AnalysisMode.CLOUD_AI, ""));

        var modes = await service.GetModesAsync();

        Assert.True(Assert.Single(modes, m => m.Mode == AnalysisMode.CLOUD_AI).IsAvailable);
    }

    // ---------- AC-27, AC-28: стиль і R8 ----------

    [Fact]
    public async Task AC27_CloudAi_StyleAccepted()
    {
        var cloud = Answering(AnalysisMode.CLOUD_AI, """{"style":"LOFT","confidence":0.82}""");
        var service = CreateService(null, cloud);
        service.GiveCloudConsent();

        var outcome = await service.AnalyzeAsync(AnalysisMode.CLOUD_AI);

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        Assert.Null(outcome.Notice);
        var analysis = service.Session.Analysis!;
        Assert.Equal(AnalysisMode.CLOUD_AI, analysis.Mode);
        Assert.Equal(RoomStyle.LOFT, analysis.Style);
        Assert.Equal(0.82m, analysis.Confidence);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
        Assert.Equal(1, cloud.Calls);
        Assert.True(cloud.LastImage!.Length > 0);
    }

    [Theory]
    [InlineData("""{"style":"LOFT","confidence":0.59}""", 0.59)]
    [InlineData("""{"style":"Футуризм","confidence":0.9}""", 0.9)]
    [InlineData("not json", null)]
    [InlineData("", null)]
    public async Task AC28_NotAccepted_StyleNotDetermined_PaletteKept(string answer, double? confidence)
    {
        var service = CreateService(null, Answering(AnalysisMode.LOCAL_AI, answer));

        var outcome = await service.AnalyzeAsync(AnalysisMode.LOCAL_AI);

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        Assert.Null(outcome.Notice);
        var analysis = service.Session.Analysis!;
        Assert.Equal(AnalysisMode.LOCAL_AI, analysis.Mode);
        Assert.Null(analysis.Style);
        Assert.Equal((decimal?)confidence, analysis.Confidence);
        Assert.InRange(analysis.Colors.Count, 3, 5);
    }

    // ---------- AC-30: ШІ не відповів ----------

    [Fact]
    public async Task AC30_ProviderFails_BasicResultWithNotice()
    {
        var cloud = new FakeStyleAnalyzer(AnalysisMode.CLOUD_AI, (_, _) => throw new HttpRequestException("немає мережі"));
        var service = CreateService(null, cloud);
        service.GiveCloudConsent();

        var outcome = await service.AnalyzeAsync(AnalysisMode.CLOUD_AI);

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        Assert.Equal(DesignService.StyleFailedNotice(AnalysisMode.CLOUD_AI), outcome.Notice);
        Assert.Equal(AnalysisMode.BASIC, service.Session.Analysis!.Mode);
        Assert.Null(service.Session.Analysis.Style);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
    }

    [Fact]
    public async Task AC30_ProviderTimeout_BasicResultWithNotice()
    {
        var local = new FakeStyleAnalyzer(AnalysisMode.LOCAL_AI, async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return "";
        });
        var service = new DesignService(new StubPhotoAnalyzer(), new StubSuggestionClient(), new RecordingGalleryClient(), new[] { local })
        {
            StyleTimeout = TimeSpan.FromMilliseconds(200)
        };
        service.LoadPhoto(CreatePhoto());

        var outcome = await service.AnalyzeAsync(AnalysisMode.LOCAL_AI);

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        Assert.Equal(service.StyleTimeoutNotice(AnalysisMode.LOCAL_AI), outcome.Notice);
        Assert.Equal(AnalysisMode.BASIC, service.Session.Analysis!.Mode);
        Assert.False(service.Session.IsAnalyzing);
    }

    [Fact]
    public async Task ModeWithoutProvider_BasicResultWithNotice()
    {
        var service = CreateService();

        var outcome = await service.AnalyzeAsync(AnalysisMode.LOCAL_AI);

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        Assert.Equal(DesignService.StyleFailedNotice(AnalysisMode.LOCAL_AI), outcome.Notice);
        Assert.Equal(AnalysisMode.BASIC, service.Session.Analysis!.Mode);
    }

    [Fact]
    public async Task AC30_ProviderFails_ReasonLogged()
    {
        var logger = new ListLogger();
        var cloud = new FakeStyleAnalyzer(AnalysisMode.CLOUD_AI, (_, _) => throw new HttpRequestException("Gemini 404: model not found"));
        var service = new DesignService(new StubPhotoAnalyzer(), new StubSuggestionClient(), new RecordingGalleryClient(),
            new[] { cloud }, logger);
        service.LoadPhoto(CreatePhoto());
        service.GiveCloudConsent();

        await service.AnalyzeAsync(AnalysisMode.CLOUD_AI);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("CLOUD_AI", entry.Message);
        Assert.Contains("Gemini 404", entry.Exception!.Message);
    }

    private sealed class ListLogger : ILogger<DesignService>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    // ---------- #100: повторний аналіз в іншому режимі без повторного завантаження фото ----------

    [Fact]
    public async Task Bug100_Reanalyze_InOtherMode_AfterAiFailed_GivesStyle()
    {
        var local = new FakeStyleAnalyzer(AnalysisMode.LOCAL_AI, (_, _) => throw new HttpRequestException("тайм-аут"));
        var cloud = Answering(AnalysisMode.CLOUD_AI, """{"style":"SCANDINAVIAN","confidence":0.9}""");
        var service = CreateService(null, local, cloud);
        service.GiveCloudConsent();
        await service.AnalyzeAsync(AnalysisMode.LOCAL_AI);

        var outcome = await service.AnalyzeAsync(AnalysisMode.CLOUD_AI);

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        Assert.Equal(AnalysisMode.CLOUD_AI, service.Session.Analysis!.Mode);
        Assert.Equal(RoomStyle.SCANDINAVIAN, service.Session.Analysis.Style);
    }

    [Fact]
    public async Task Bug100_Reanalyze_AfterSuggestions_ClearsSuggestions()
    {
        var service = CreateService();
        await service.AnalyzeAsync(AnalysisMode.BASIC);
        await service.SuggestAsync();

        var outcome = await service.AnalyzeAsync(AnalysisMode.BASIC);

        Assert.Equal(AnalysisStatus.Success, outcome.Status);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
        Assert.Empty(service.Session.Suggestions);
    }

    // ---------- AC-31: згода на хмарний ШІ ----------

    [Fact]
    public async Task AC31_CloudWithoutConsent_PhotoNotSent_SessionUnchanged()
    {
        var cloud = Answering(AnalysisMode.CLOUD_AI, """{"style":"LOFT","confidence":0.9}""");
        var service = CreateService(null, cloud);

        var outcome = await service.AnalyzeAsync(AnalysisMode.CLOUD_AI);

        Assert.Equal(AnalysisStatus.Error, outcome.Status);
        Assert.Equal(DesignService.CloudConsentRequiredMessage, outcome.Message);
        Assert.Equal(0, cloud.Calls);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
        Assert.False(service.Session.IsAnalyzing);
    }

    [Fact]
    public async Task Basic_DoesNotCallAi()
    {
        var cloud = Answering(AnalysisMode.CLOUD_AI, "");
        var service = CreateService(null, cloud);
        service.GiveCloudConsent();

        await service.AnalyzeAsync(AnalysisMode.BASIC);

        Assert.Equal(0, cloud.Calls);
        Assert.Equal(AnalysisMode.BASIC, service.Session.Analysis!.Mode);
    }

    // ---------- AC-32: стиль іде в галерею разом із роботою ----------

    [Fact]
    public async Task AC32_Save_SendsAiModeAndStyle()
    {
        var gallery = new RecordingGalleryClient();
        var service = CreateService(gallery, Answering(AnalysisMode.CLOUD_AI, """{"style":"BOHO","confidence":0.75}"""));
        service.GiveCloudConsent();
        await service.AnalyzeAsync(AnalysisMode.CLOUD_AI);
        await service.SuggestAsync();
        service.PlaceItem(service.Session.Suggestions[0].Item, 0, 0);

        await service.SaveAsync(new byte[] { 1 });

        var sent = Assert.Single(gallery.Saves).Analysis;
        Assert.Equal("CLOUD_AI", sent.Mode);
        Assert.Equal("BOHO", sent.Style);
        Assert.Equal(0.75m, sent.Confidence);
        Assert.NotNull(sent.ToModel(out var error));
        Assert.Null(error);
    }
}
