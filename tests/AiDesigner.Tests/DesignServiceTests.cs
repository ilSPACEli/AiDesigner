using AiDesigner.Api.Dto;
using AiDesigner.Clients;
using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Stubs;
using AiDesigner.Tests.Api;
using AiDesigner.Tests.Clients;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

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
        ISuggestionClient? suggestions = null,
        IGalleryClient? gallery = null) =>
        new(analyzer ?? new StubPhotoAnalyzer(),
            suggestions ?? new StubSuggestionClient(),
            gallery ?? new RecordingGalleryClient());

    // Сервіс у стані ANALYZED (аналіз на заглушці).
    private static async Task<DesignService> CreateAnalyzedAsync(ISuggestionClient suggestions)
    {
        var service = CreateService(suggestions: suggestions);
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();
        return service;
    }

    // Сервіс у стані ITEM_PLACED: фото → аналіз → пропозиції → предмет розміщено.
    private static async Task<DesignService> CreatePlacedAsync(IGalleryClient gallery)
    {
        var service = CreateService(gallery: gallery);
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();
        await service.SuggestAsync();
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
        var suggest = await service.SuggestAsync();
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
            new StubSuggestionClient(),
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

    // ---------- UC-03: добір через API ----------

    [Fact]
    public async Task Suggest_SendsAnalysisWithoutPhoto_AndShowsFullItems()
    {
        var item = StubSuggestionClient.Items[0];
        var client = new FakeSuggestionClient(_ => SuggestionClientResult.Success(new[]
        {
            SuggestionDto.From(new Suggestion { Item = item, Rank = 1, Score = 2, Reason = "Аналогова гармонія" })
        }));
        var service = await CreateAnalyzedAsync(client);

        var result = await service.SuggestAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionState.SUGGESTIONS_SHOWN, service.Session.State);

        // На сервер іде результат аналізу (NFR-04: без фото).
        var request = Assert.Single(client.Requests);
        var analysis = service.Session.Analysis!;
        Assert.Equal(analysis.Lighting.ToString(), request.Lighting);
        Assert.Equal(analysis.Colors.Select(c => c.Hex), request.Colors!.Select(c => c!.Hex));

        // Предмет повний: з картинкою, щоб поставити його на полотно.
        var shown = Assert.Single(service.Session.Suggestions);
        Assert.Equal(item.Id, shown.Item.Id);
        Assert.Equal(item.Name, shown.Item.Name);
        Assert.Equal(item.ImageFile, shown.Item.ImageFile);
        Assert.Equal(item.SuitableLightings, shown.Item.SuitableLightings);
        Assert.Equal("Аналогова гармонія", shown.Reason);
    }

    [Fact]
    public async Task Suggest_NoSuitableItems_Fails_StaysAnalyzed()
    {
        var service = await CreateAnalyzedAsync(new FakeSuggestionClient(_ => SuggestionClientResult.NoSuitableItems()));

        var result = await service.SuggestAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(DesignService.NoSuitableItemsMessage, result.Message);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
    }

    [Fact]
    public async Task Suggest_CatalogUnavailable_Fails_StaysAnalyzed()
    {
        var service = await CreateAnalyzedAsync(new FakeSuggestionClient(_ => SuggestionClientResult.Unavailable()));

        var result = await service.SuggestAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(DesignService.CatalogUnavailableMessage, result.Message);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
    }

    [Fact]
    public async Task Suggest_ClientThrows_ReturnsCatalogUnavailable_StaysAnalyzed()
    {
        var service = await CreateAnalyzedAsync(new FakeSuggestionClient(_ => throw new HttpRequestException("API недоступний")));

        var result = await service.SuggestAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(DesignService.CatalogUnavailableMessage, result.Message);
        Assert.Equal(SessionState.ANALYZED, service.Session.State);
    }

    [Fact]
    public async Task Suggest_CallerCancels_PropagatesCancellation_StaysAnalyzed()
    {
        using var cts = new CancellationTokenSource();
        var service = await CreateAnalyzedAsync(new FakeSuggestionClient(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SuggestAsync(cts.Token));

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
    public async Task AC32_Save_SendsSessionAnalysis()
    {
        var gallery = new RecordingGalleryClient();
        var service = await CreatePlacedAsync(gallery);

        await service.SaveAsync(new byte[] { 1 });

        var sent = Assert.Single(gallery.Saves).Analysis;
        var analysis = service.Session.Analysis!;
        Assert.Equal("BASIC", sent.Mode);
        Assert.Equal(analysis.Lighting.ToString(), sent.Lighting);
        Assert.Equal(analysis.Colors.Select(c => c.Hex), sent.Colors!.Select(c => c.Hex));
        Assert.Null(sent.Style);
        Assert.Null(sent.Confidence);
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

    // ---------- #53: велике фото не зберігається в галерею (FR-14, NFR-06) ----------

    // PNG із випадковим шумом: шум майже не стискається, тож 2500×2000 дає файл приблизно 15 МБ.
    private static byte[] CreateNoisyPng(int width, int height)
    {
        var random = new Random(53);   // фіксоване зерно: тест відтворюваний
        var row = new byte[width * 3];
        using var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                random.NextBytes(row);
                var pixels = rows.GetRowSpan(y);
                for (var x = 0; x < pixels.Length; x++)
                    pixels[x] = new Rgb24(row[x * 3], row[x * 3 + 1], row[x * 3 + 2]);
            }
        });
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    [Fact]
    public async Task Bug53_Save_LargePhotoResult_IsSavedToGallery()
    {
        const int galleryLimit = 5 * 1024 * 1024;
        var png = CreateNoisyPng(2500, 2000);
        Assert.True(png.Length > galleryLimit, $"Тестове зображення має бути більшим за 5 МБ, а воно {png.Length} Б.");

        // Справжня галерея: клієнт з'єднано з API через ApiFactory (як у GalleryClientTests).
        using var factory = new ApiFactory();
        // CreatePlacedAsync ставить stub-01, якого немає в каталозі галереї. Тому пропозицію з реальним
        // предметом sofa-01 віддаємо через підробку добору, а решту кроків робимо як у CreatePlacedAsync.
        var item = new CatalogItem
        {
            Id = "sofa-01", Name = "Сірий диван", Type = ItemType.SOFA,
            ImageFile = "images/catalog/sofa-01.png", MainColor = "#9E9E9E",
            SuitableLightings = new[] { LightingLevel.DARK, LightingLevel.MODERATE, LightingLevel.BRIGHT }
        };
        var suggestions = new FakeSuggestionClient(_ => SuggestionClientResult.Success(new[]
        {
            SuggestionDto.From(new Suggestion { Item = item, Rank = 1, Score = 2, Reason = "Аналогова гармонія" })
        }));
        var service = CreateService(suggestions: suggestions, gallery: new GalleryClient(factory.CreateClient()));
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();
        await service.SuggestAsync();
        service.PlaceItem(service.Session.Suggestions[0].Item, 0, 0);

        var result = await service.SaveAsync(png);

        Assert.True(result.IsSuccess, $"Збереження відхилено: \"{result.Message}\"");
        Assert.Equal(1, await factory.Results.CountAsync());
        var saved = Assert.Single(await factory.Results.GetAllAsync());
        Assert.True(saved.ImageData.Length <= galleryLimit,
            $"Зображення в галереї {saved.ImageData.Length} Б, ліміт {galleryLimit} Б.");
    }
}
