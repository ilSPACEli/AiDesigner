using System.Net;
using AiDesigner.Api.Dto;
using AiDesigner.Clients;
using AiDesigner.Models;
using AiDesigner.Services.Results;
using AiDesigner.Tests.Analysis;
using AiDesigner.Tests.Api;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Clients;

public class GalleryScenarioTests
{
    private const string SofaGrey = "sofa-01";     // «Сірий диван»
    private const string SofaBeige = "sofa-02";    // «Бежевий диван з подушками»

    private static readonly ResultAnalysisDto LoftAnalysis = new(
        "CLOUD_AI", "MODERATE",
        new[] { new ColorDto("#D8C3A5", 36, 61.2m), new ColorDto("#322D28", 30, 30.6m), new ColorDto("#9A8F80", 35, 8.2m) },
        "LOFT", 0.82m);

    // ШІ-режим, але стиль не визначено (немає стилю й упевненості).
    private static readonly ResultAnalysisDto AiNoStyleAnalysis = LoftAnalysis with { Style = null, Confidence = null };

    // Режим «Аналіз без ШІ»: стиль і впевненість не передаються.
    private static readonly ResultAnalysisDto BasicAnalysis = LoftAnalysis with { Mode = "BASIC", Style = null, Confidence = null };

    private static byte[] Png() => TestImages.Solid(40, 30, new Rgb24(200, 100, 50));

    private static GalleryClient ClientFor(ApiFactory factory) => new(factory.CreateClient());

    private static async Task<ResultDto> SaveAsync(GalleryClient client, string itemId, ResultAnalysisDto analysis)
    {
        var saved = await client.SaveAsync(Png(), itemId, analysis);
        Assert.True(saved.IsSuccess, saved.Message);
        return saved.Result!;
    }

    // Дзеркало стану й дій Gallery.razor: ті самі кроки й ті самі присвоєння.
    private sealed class PageSteps(IGalleryClient client)
    {
        public List<GalleryItemView> Items { get; private set; } = new();
        public bool LoadFailed { get; private set; }
        public string? Error { get; private set; }

        // Gallery.razor -> LoadAsync (пояс UTC, щоб дата не залежала від машини).
        public async Task LoadAsync()
        {
            LoadFailed = false;
            Error = null;

            try
            {
                var results = await client.GetAllAsync();
                Items = results.Select(r => GalleryItemView.From(r, TimeZoneInfo.Utc)).ToList();
            }
            catch (Exception ex)
            {
                LoadFailed = true;
                Error = GalleryErrorMessages.FromException(ex);
            }
        }

        // Gallery.razor -> DeleteAsync. Повертає те, що повернув клієнт (null — клієнт кинув виняток).
        public async Task<bool?> DeleteAsync(int id)
        {
            Error = null;

            try
            {
                var deleted = await client.DeleteAsync(id);
                if (!deleted)
                    Error = GalleryErrorMessages.WorkNotFoundMessage;

                // І після успіху, і після 404 картку прибираємо зі списку.
                Items.RemoveAll(i => i.Id == id);
                return deleted;
            }
            catch (Exception ex)
            {
                Error = GalleryErrorMessages.FromException(ex);   // робота лишається в списку
                return null;
            }
        }
    }

    // ---------- 1. Завантаження списку (AC-19, AC-21) ----------

    [Fact]
    public async Task AC19_Gallery_Load_TwoSavedWorks_ShowsTwoCardsNewestFirst()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        var first = await SaveAsync(client, SofaGrey, LoftAnalysis);
        var second = await SaveAsync(client, SofaBeige, LoftAnalysis);
        var page = new PageSteps(client);

        await page.LoadAsync();

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(new[] { second.Id, first.Id }, page.Items.Select(i => i.Id));
        Assert.False(page.LoadFailed);
        Assert.Null(page.Error);
    }

    [Fact]
    public async Task AC19_Gallery_Load_Card_HasItemNameImageUrlAndDownloadFileName()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        var first = await SaveAsync(client, SofaGrey, LoftAnalysis);
        var second = await SaveAsync(client, SofaBeige, LoftAnalysis);
        var page = new PageSteps(client);

        await page.LoadAsync();

        var newest = page.Items[0];
        Assert.Equal("Бежевий диван з подушками", newest.ItemName);
        Assert.Equal($"/api/results/{second.Id}/image", newest.ImageUrl);
        Assert.Equal($"/api/results/{second.Id}/thumbnail", newest.ThumbnailUrl);
        Assert.Equal($"result-{second.Id}.png", newest.DownloadFileName);

        var oldest = page.Items[1];
        Assert.Equal("Сірий диван", oldest.ItemName);
        Assert.Equal($"/api/results/{first.Id}/image", oldest.ImageUrl);
        Assert.Equal($"result-{first.Id}.png", oldest.DownloadFileName);
    }

    [Fact]
    public async Task AC19_Gallery_Load_EmptyGallery_ShowsNoCardsAndNoError()
    {
        using var factory = new ApiFactory();
        var page = new PageSteps(ClientFor(factory));

        await page.LoadAsync();

        Assert.Empty(page.Items);
        Assert.False(page.LoadFailed);
        Assert.Null(page.Error);
    }

    // ---------- 2. Стиль на картці (AC-32) ----------

    [Fact]
    public async Task AC32_Gallery_Card_WorkWithLoftStyle_ShowsLoft()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        await SaveAsync(client, SofaGrey, LoftAnalysis);
        var page = new PageSteps(client);

        await page.LoadAsync();

        var style = page.Items.Single().Style;
        Assert.NotNull(style);
        Assert.Equal("Стиль: Лофт", style!.Text);
        Assert.True(style.IsDetermined);
    }

    [Fact]
    public async Task AC32_Gallery_Card_AiWorkWithoutStyle_ShowsStyleNotDetermined()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        await SaveAsync(client, SofaGrey, AiNoStyleAnalysis);
        var page = new PageSteps(client);

        await page.LoadAsync();

        var style = page.Items.Single().Style;
        Assert.NotNull(style);
        Assert.Equal(DisplayNames.StyleNotDetermined, style!.Text);   // «Стиль не визначено»
        Assert.False(style.IsDetermined);
    }

    [Fact]
    public async Task AC32_Gallery_Card_BasicWork_HasNoStyleLine()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        await SaveAsync(client, SofaGrey, BasicAnalysis);
        var page = new PageSteps(client);

        await page.LoadAsync();

        // Без ШІ стиль не визначається: рядка на картці немає (не «не визначено»).
        Assert.Null(page.Items.Single().Style);
    }

    // ---------- 3. Видалення (AC-22) ----------

    [Fact]
    public async Task AC22_Gallery_Delete_ExistingWork_ReturnsTrueAndRemovesCard()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        var keep = await SaveAsync(client, SofaGrey, LoftAnalysis);
        var remove = await SaveAsync(client, SofaBeige, LoftAnalysis);
        var page = new PageSteps(client);
        await page.LoadAsync();

        var deleted = await page.DeleteAsync(remove.Id);

        Assert.True(deleted);
        Assert.Null(page.Error);
        Assert.Equal(new[] { keep.Id }, page.Items.Select(i => i.Id));

        // Після нового завантаження результат той самий: на сервері роботи теж немає.
        await page.LoadAsync();
        Assert.Equal(new[] { keep.Id }, page.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task AC22_Gallery_Delete_AlreadyDeleted_ReturnsFalseWithNotFoundMessage()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        var saved = await SaveAsync(client, SofaGrey, LoftAnalysis);
        var page = new PageSteps(client);
        await page.LoadAsync();
        await page.DeleteAsync(saved.Id);

        var second = await page.DeleteAsync(saved.Id);

        Assert.False(second);
        Assert.Equal(GalleryErrorMessages.WorkNotFoundMessage, page.Error);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task AC22_Gallery_Delete_WorkDeletedElsewhere_NotFoundMessageAndCardRemoved()
    {
        // Картка ще на екрані, а робота вже видалена (інша вкладка): 404 -> повідомлення, картка зникає.
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        var saved = await SaveAsync(client, SofaGrey, LoftAnalysis);
        var page = new PageSteps(client);
        await page.LoadAsync();
        await client.DeleteAsync(saved.Id);

        var deleted = await page.DeleteAsync(saved.Id);

        Assert.False(deleted);
        Assert.Equal(GalleryErrorMessages.WorkNotFoundMessage, page.Error);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task AC22_Gallery_Delete_ServerError_ShowsServerMessageAndKeepsCard()
    {
        // Список завантажуємо зі справжнього API, а видалення відповідає 500.
        using var factory = new ApiFactory();
        var real = ClientFor(factory);
        var saved = await SaveAsync(real, SofaGrey, LoftAnalysis);
        var page = new PageSteps(new DeleteFailsClient(real, HttpStatusCode.InternalServerError));
        await page.LoadAsync();

        var deleted = await page.DeleteAsync(saved.Id);

        Assert.Null(deleted);   // клієнт кинув виняток
        Assert.Equal(GalleryErrorMessages.ServerErrorMessage, page.Error);
        Assert.Equal(new[] { saved.Id }, page.Items.Select(i => i.Id));   // робота лишається в списку
    }

    // ---------- 4. Помилки завантаження ----------

    [Fact]
    public async Task Gallery_Load_Server500_ShowsServerErrorMessage()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var page = new PageSteps(new GalleryClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));

        await page.LoadAsync();

        Assert.True(page.LoadFailed);
        Assert.Equal(GalleryErrorMessages.ServerErrorMessage, page.Error);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Gallery_Load_NoConnection_ShowsNoConnectionMessage()
    {
        var page = new PageSteps(new GalleryClient(
            new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://localhost/") }));

        await page.LoadAsync();

        Assert.True(page.LoadFailed);
        Assert.Equal(GalleryErrorMessages.NoConnectionMessage, page.Error);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Gallery_Load_AfterFailure_RetrySucceedsAndClearsError()
    {
        // Кнопка «Спробувати ще раз» викликає той самий LoadAsync: стан помилки скидається.
        using var factory = new ApiFactory();
        var real = ClientFor(factory);
        await SaveAsync(real, SofaGrey, LoftAnalysis);
        var flaky = new FlakyListClient(real);
        var page = new PageSteps(flaky);

        await page.LoadAsync();
        Assert.True(page.LoadFailed);
        Assert.Equal(GalleryErrorMessages.NoConnectionMessage, page.Error);

        await page.LoadAsync();

        Assert.False(page.LoadFailed);
        Assert.Null(page.Error);
        Assert.Single(page.Items);
    }

    // ---------- Допоміжні клієнти й обробники ----------

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Немає з'єднання");
    }

    // Список і збереження — справжні; видалення відповідає помилкою сервера.
    private sealed class DeleteFailsClient(IGalleryClient inner, HttpStatusCode status) : IGalleryClient
    {
        public Task<GallerySaveResult> SaveAsync(byte[] pngBytes, string catalogItemId, ResultAnalysisDto analysis, CancellationToken cancellationToken = default) =>
            inner.SaveAsync(pngBytes, catalogItemId, analysis, cancellationToken);

        public Task<IReadOnlyList<ResultDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            inner.GetAllAsync(cancellationToken);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Сервер відповів помилкою", null, status);
    }

    private sealed class FlakyListClient(IGalleryClient inner) : IGalleryClient
    {
        private bool _failed;

        public Task<GallerySaveResult> SaveAsync(byte[] pngBytes, string catalogItemId, ResultAnalysisDto analysis, CancellationToken cancellationToken = default) =>
            inner.SaveAsync(pngBytes, catalogItemId, analysis, cancellationToken);

        public Task<IReadOnlyList<ResultDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            if (_failed)
                return inner.GetAllAsync(cancellationToken);

            _failed = true;
            throw new HttpRequestException("Немає з'єднання");
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(id, cancellationToken);
    }
}