using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Stubs;

namespace AiDesigner.Tests;

/// <summary>
/// Тести оркестратора DesignService: наскрізний сценарій на заглушках
/// і невдалі гілки UC-02 (A1, E1), UC-03 (R5, E1), UC-04 (E1).
/// </summary>
public class DesignServiceTests
{
    // ---------- Підробки (fakes): поводяться так, як потрібно тесту ----------

    private sealed class FakeAnalyzer(Func<CancellationToken, Task<AnalysisOutcome>> run) : IPhotoAnalyzer
    {
        public Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default) =>
            run(cancellationToken);
    }

    private sealed class FakeCatalog(Func<IReadOnlyList<Suggestion>> select) : ICatalog
    {
        public IReadOnlyList<CatalogItem> Items => Array.Empty<CatalogItem>();
        public IReadOnlyList<Suggestion> Select(AnalysisResult analysis, int count) => select();
    }

    private sealed class FailingStorage : IResultStorage
    {
        public Task<ResultImage> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default) =>
            throw new IOException("Диск недоступний");
    }

    private static Photo CreatePhoto() => new()
    {
        FileName = "room.jpg", Format = "jpeg", Width = 800, Height = 600, SizeBytes = 1000,
        Content = new byte[] { 1, 2, 3 }
    };

    private static DesignService CreateService(
        IPhotoAnalyzer? analyzer = null, ICatalog? catalog = null, IResultStorage? storage = null) =>
        new(analyzer ?? new StubPhotoAnalyzer(),
            catalog ?? new StubCatalog(),
            storage ?? new StubResultStorage());

    // ---------- Наскрізний сценарій ----------

    [Fact]
    public async Task FullScenario_WithStubs_EndsInSaved()
    {
        var service = CreateService();

        service.LoadPhoto(CreatePhoto());
        var analysis = await service.AnalyzeAsync();
        var suggest = service.Suggest();
        service.PlaceItem(service.Session.Suggestions[0].Item, 10, 10);
        var save = await service.SaveAsync(new byte[] { 1 });

        Assert.Equal(AnalysisStatus.Success, analysis.Status);
        Assert.True(suggest.IsSuccess);
        Assert.True(save.IsSuccess);
        Assert.Equal(SessionState.SAVED, service.Session.State);
        Assert.Single(service.Session.SavedResults);
    }

    // ---------- UC-02: невдалий аналіз ----------

    [Fact]
    public async Task Analyze_InsufficientData_KeepsPhotoLoaded()
    {
        var service = CreateService(analyzer: new FakeAnalyzer(_ => Task.FromResult(AnalysisOutcome.InsufficientData())));
        service.LoadPhoto(CreatePhoto());

        var outcome = await service.AnalyzeAsync();

        Assert.Equal(AnalysisStatus.InsufficientData, outcome.Status);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
        Assert.Null(service.Session.Analysis);
        Assert.False(service.Session.IsAnalyzing);
    }

    [Fact]
    public async Task Analyze_AnalyzerThrows_ReturnsError_KeepsPhotoLoaded()
    {
        var service = CreateService(analyzer: new FakeAnalyzer(_ => throw new InvalidDataException("зіпсований файл")));
        service.LoadPhoto(CreatePhoto());

        var outcome = await service.AnalyzeAsync();

        Assert.Equal(AnalysisStatus.Error, outcome.Status);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
        Assert.False(service.Session.IsAnalyzing);
    }

    [Fact]
    public async Task Analyze_Timeout_ReturnsError_KeepsPhotoLoaded()
    {
        // Аналізатор «зависає» назавжди; тайм-аут зменшено до 200 мс, щоб тест був швидким.
        var service = new DesignService(
            new FakeAnalyzer(async ct => { await Task.Delay(Timeout.Infinite, ct); return AnalysisOutcome.InsufficientData(); }),
            new StubCatalog(),
            new StubResultStorage())
        {
            AnalysisTimeout = TimeSpan.FromMilliseconds(200)
        };
        service.LoadPhoto(CreatePhoto());

        var outcome = await service.AnalyzeAsync();

        Assert.Equal(AnalysisStatus.Error, outcome.Status);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
        Assert.False(service.Session.IsAnalyzing);
    }

    [Fact]
    public async Task Analyze_Failed_PhotoCanBeReplacedAgain()
    {
        var service = CreateService(analyzer: new FakeAnalyzer(_ => Task.FromResult(AnalysisOutcome.Error("помилка"))));
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();

        var newPhoto = CreatePhoto();
        service.LoadPhoto(newPhoto);

        Assert.Same(newPhoto, service.Session.Photo);
    }

    // ---------- UC-03: немає пропозицій ----------

    [Fact]
    public async Task Suggest_NoItems_Fails_StaysAnalyzed()
    {
        var service = CreateService(catalog: new FakeCatalog(() => Array.Empty<Suggestion>()));
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();

        var result = service.Suggest();

        Assert.False(result.IsSuccess);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
    }

    [Fact]
    public async Task Suggest_CatalogUnavailable_Fails_StaysAnalyzed()
    {
        var service = CreateService(catalog: new FakeCatalog(() => throw new FileNotFoundException("catalog.json")));
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();

        var result = service.Suggest();

        Assert.False(result.IsSuccess);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
    }

    // ---------- UC-04: помилка збереження ----------

    [Fact]
    public async Task Save_StorageFails_StaysItemPlaced()
    {
        var service = CreateService(storage: new FailingStorage());
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();
        service.Suggest();
        service.PlaceItem(service.Session.Suggestions[0].Item, 0, 0);

        var result = await service.SaveAsync(new byte[] { 1 });

        Assert.False(result.IsSuccess);
        Assert.Equal(SessionState.ITEM_PLACED, service.Session.State);
        Assert.Empty(service.Session.SavedResults);
    }
}
