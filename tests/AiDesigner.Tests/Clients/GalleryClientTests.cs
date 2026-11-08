using System.Net;
using System.Text;
using AiDesigner.Clients;
using AiDesigner.Services.Results;
using AiDesigner.Tests.Analysis;
using AiDesigner.Tests.Api;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Clients;

// GalleryClient "по-справжньому": клієнт з'єднано з API через ApiFactory (TestServer), без мережі й без PostgreSQL.
// Так перевіряється все разом: запит клієнта (multipart), ендпоінт, сервіс і розбір відповіді.
public class GalleryClientTests
{
    private const string ItemId = "sofa-01";

    private static byte[] Png() => TestImages.Solid(40, 30, new Rgb24(200, 100, 50));

    private static GalleryClient ClientFor(ApiFactory factory) => new(factory.CreateClient());

    // ---------- AC-19: збереження ----------

    [Fact]
    public async Task AC19_Save_Valid_ReturnsSuccessWithWork()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);

        var result = await client.SaveAsync(Png(), ItemId);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Message);
        Assert.True(result.Result!.Id > 0);
        Assert.Equal(ItemId, result.Result.CatalogItemId);
        Assert.Equal("Сірий диван", result.Result.ItemName);
        Assert.Equal($"/api/results/{result.Result.Id}/image", result.Result.ImageUrl);
        Assert.Equal(1, await factory.Results.CountAsync());
    }

    [Fact]
    public async Task AC19_GetAll_ReturnsSavedWorksNewestFirst()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        var first = (await client.SaveAsync(Png(), ItemId)).Result!;
        var second = (await client.SaveAsync(Png(), ItemId)).Result!;

        var all = await client.GetAllAsync();

        Assert.Equal(new[] { second.Id, first.Id }, all.Select(r => r.Id));
    }

    // ---------- Відмови API перетворюються на повідомлення, а не на виняток ----------

    [Fact]
    public async Task AC24_Save_TooLarge_ReturnsServerMessage()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);

        var result = await client.SaveAsync(new byte[5 * 1024 * 1024 + 1], ItemId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Максимальний розмір — 5 МБ.", result.Message);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task AC24_Save_RateLimited_ReturnsServerMessage()
    {
        using var factory = new ApiFactory(new GalleryOptions { SavesPerMinute = 1 });
        var client = ClientFor(factory);
        await client.SaveAsync(Png(), ItemId);

        var result = await client.SaveAsync(Png(), ItemId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Ліміт збережень — 1 за хвилину. Спробуйте пізніше.", result.Message);
    }

    [Fact]
    public async Task Save_NotPng_ReturnsServerMessage()
    {
        using var factory = new ApiFactory();

        var result = await ClientFor(factory).SaveAsync(TestImages.RoomJpeg(64, 48), ItemId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultService.NotPngMessage, result.Message);
    }

    [Fact]
    public async Task Save_UnknownItem_ReturnsServerMessage()
    {
        using var factory = new ApiFactory();

        var result = await ClientFor(factory).SaveAsync(Png(), "no-such-item");

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultService.UnknownItemMessage, result.Message);
    }

    [Fact]
    public async Task Save_ErrorWithoutProblemBody_ReturnsGenericMessageWithStatusCode()
    {
        // Наприклад, проксі відповів власною сторінкою помилки: тіло не ProblemDetails.
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad gateway</html>", Encoding.UTF8, "text/html")
        });
        var client = new GalleryClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var result = await client.SaveAsync(Png(), ItemId);

        Assert.False(result.IsSuccess);
        Assert.Equal($"{GalleryClient.GenericFailureMessage} (код 502)", result.Message);
    }

    // ---------- AC-22: видалення ----------

    [Fact]
    public async Task AC22_Delete_ReturnsTrue_ThenFalseForSecondDelete()
    {
        using var factory = new ApiFactory();
        var client = ClientFor(factory);
        var saved = (await client.SaveAsync(Png(), ItemId)).Result!;

        var first = await client.DeleteAsync(saved.Id);
        var second = await client.DeleteAsync(saved.Id);

        Assert.True(first);
        Assert.False(second);
        Assert.Empty(await client.GetAllAsync());
    }

    // ---------- Збій зв'язку — виняток (його ловить DesignService) ----------

    [Fact]
    public async Task Save_ConnectionFailure_Throws()
    {
        var client = new GalleryClient(new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://localhost/") });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SaveAsync(Png(), ItemId));
    }

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
}