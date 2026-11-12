using AiDesigner.Api.Dto;
using AiDesigner.Clients;
using AiDesigner.Models;
using AiDesigner.Tests.Clients;
using AiDesigner.Services;
using AiDesigner.Services.Stubs;

namespace AiDesigner.Tests;

public class DesignServiceTests
{
    // ---------- Підробки (fakes): поводяться так, як потрібно тесту ----------

    private sealed class FakeAnalyzer(Func<CancellationToken, Task<AnalysisOutcome>> run) : IPhotoAnalyzer
    {
        public Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default) =>
            run(cancellationToken);
    }

    private static Photo CreatePhoto() => new()
    {
        FileName = "room.jpg", Format = "jpeg", Width = 800, Height = 600, SizeBytes = 1000,
        Content = new byte[] { 1, 2, 3 }
    };

    private static DesignService CreateService(
        IPhotoAnalyzer? analyzer = null, 
        ICatalogClient? catalogClient = null, 
        ISuggestionClient? suggestionClient = null, 
        IGalleryClient? gallery = null)
    {
        // Створюємо дефолтні фейки, щоб тести проходили
        var defaultCatalog = new FakeCatalogClient(() => new List<CatalogItemDto> 
        { 
            new() { Id = "item1", Type = "SOFA" } 
        });
        var defaultSuggestion = new FakeSuggestionClient(() => new List<SuggestionDto> 
        { 
            new() { Id = "item1" } 
        });

        return new DesignService(
            analyzer ?? new StubPhotoAnalyzer(),
            catalogClient ?? defaultCatalog,
            suggestionClient ?? defaultSuggestion,
            gallery ?? new RecordingGalleryClient());
    }

    // Сервіс у стані ITEM_PLACED: фото → аналіз → пропозиції → предмет розміщено.
    private static async Task<DesignService> CreatePlacedAsync(IGalleryClient gallery)
    {
        var service = CreateService(gallery: gallery);
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();
        await service.SuggestAsync(); // Оновлено на асинхронний виклик
        service.PlaceItem(service.Session.Suggestions[0].Item, 0, 0);
        return service;
    }

    // ---------- Наскрізний сценарій ----------

    [Fact]
    public async Task FullScenario_WithStubs_EndsInSaved()
    {
        var service = CreateService();

        service.LoadPhoto(CreatePhoto());
        var analysis = await service.AnalyzeAsync();
        var suggest = await service.SuggestAsync(); // Оновлено
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
        var service = new DesignService(
            new FakeAnalyzer(async ct => { await Task.Delay(Timeout.Infinite, ct); return AnalysisOutcome.InsufficientData(); }),
            new FakeSuggestionClient(() => Array.Empty<SuggestionDto>()),
            new FakeCatalogClient(() => Array.Empty<CatalogItemDto>()),
            new RecordingGalleryClient())
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
        var service = CreateService(suggestionClient: new FakeSuggestionClient(() => Array.Empty<SuggestionDto>()));
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();

        var result = await service.SuggestAsync(); // Оновлено

        Assert.False(result.IsSuccess);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
    }

    [Fact]
    public async Task Suggest_CatalogUnavailable_Fails_StaysAnalyzed()
    {
        var service = CreateService(catalogClient: new FakeCatalogClient(() => throw new FileNotFoundException("API недоступне")));
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();

        var result = await service.SuggestAsync(); // Оновлено

        Assert.False(result.IsSuccess);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
    }

    // ---------- UC-04 / US-1: збереження в галерею через клієнт ----------

    [Fact]
    public async Task AC19_Save_SendsPngAndPlacedItemId_ToGallery()
    {
        var gallery = new RecordingGalleryClient();
        var service = await CreatePlacedAsync(gallery);
        var png = new byte[] { 1, 2, 3 };

        var result = await service.SaveAsync(png);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(gallery.Saves);
        Assert.Equal(png, sent.Png);
        Assert.Equal(service.Session.Placement!.Item.Id, sent.CatalogItemId);
    }

    [Fact]
    public async Task AC19_Save_Success_MovesSessionToSaved_AndRecordsWork()
    {
        var service = await CreatePlacedAsync(new RecordingGalleryClient());

        var result = await service.SaveAsync(new byte[] { 1 });

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionState.SAVED, service.Session.State);
        var saved = Assert.Single(service.Session.SavedResults);
        Assert.Equal("result-1.png", saved.FileName);
        Assert.Equal("png", saved.Format);
    }

    [Fact]
    public async Task AC24_Save_GalleryRejects_ReturnsItsMessage_StaysItemPlaced()
    {
        var gallery = new RecordingGalleryClient { SaveFailureMessage = "Максимальний розмір — 5 МБ." };
        var service = await CreatePlacedAsync(gallery);

        var result = await service.SaveAsync(new byte[] { 1 });

        Assert.False(result.IsSuccess);
        Assert.Equal("Максимальний розмір — 5 МБ.", result.Message);
        Assert.Equal(SessionState.ITEM_PLACED, service.Session.State);
        Assert.Empty(service.Session.SavedResults);
    }

    [Fact]
    public async Task Save_GalleryClientThrows_ReturnsFail_StaysItemPlaced()
    {
        var gallery = new RecordingGalleryClient { SaveException = new HttpRequestException("API недоступний") };
        var service = await CreatePlacedAsync(gallery);

        var result = await service.SaveAsync(new byte[] { 1 });

        Assert.False(result.IsSuccess);
        Assert.Equal("Не вдалося зберегти зображення. Спробуйте ще раз.", result.Message);
        Assert.Equal(SessionState.ITEM_PLACED, service.Session.State);
        Assert.Empty(service.Session.SavedResults);
    }

    [Fact]
    public async Task Save_NotInItemPlacedState_Throws_AndSendsNothing()
    {
        var gallery = new RecordingGalleryClient();
        var service = CreateService(gallery: gallery);
        service.LoadPhoto(CreatePhoto());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new byte[] { 1 }));

        Assert.Empty(gallery.Saves);
    }
}