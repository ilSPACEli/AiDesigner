using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiDesigner.Api;
using AiDesigner.Api.Endpoints;
using AiDesigner.Services.Results;
using AiDesigner.Tests.Analysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Api;

// HTTP API галереї через справжній Program (WebApplicationFactory), але зі сховищем у пам'яті, без PostgreSQL.
// AC-19 (201), AC-22 (204/404), AC-23 (R7 через HTTP), AC-24 (413, 429); відмови 400.
public class ResultEndpointsTests
{
    // Реальний предмет каталогу (wwwroot/data/catalog.json)
    private const string ItemId = "sofa-01";
    private const string ItemName = "Сірий диван";

    private static byte[] Png(int width = 40, int height = 30) => TestImages.Solid(width, height, new Rgb24(200, 100, 50));

    private static MultipartFormDataContent Form(byte[]? image, string? catalogItemId = ItemId)
    {
        var content = new MultipartFormDataContent();
        if (image is not null)
        {
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(file, "image", "room.png");
        }
        if (catalogItemId is not null)
            content.Add(new StringContent(catalogItemId), "catalogItemId");
        return content;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    // Зберігає роботу й повертає її id (очікуючи 201)
    private static async Task<int> PostOkAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/results", Form(Png()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(ApiProblem.ContentType, response.Content.Headers.ContentType?.MediaType);
        using var json = await ReadJsonAsync(response);
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(title, json.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
    }

    // ---------- AC-19: POST → 201 ----------

    [Fact]
    public async Task AC19_Post_ValidPng_Returns201WithResultDtoAndLocation()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/results", Form(Png()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        var id = root.GetProperty("id").GetInt32();
        Assert.True(id > 0);
        Assert.Equal($"/api/results/{id}/image", response.Headers.Location?.OriginalString);
        Assert.Equal(ItemId, root.GetProperty("catalogItemId").GetString());
        Assert.Equal(ItemName, root.GetProperty("itemName").GetString());
        Assert.Equal($"/api/results/{id}/image", root.GetProperty("imageUrl").GetString());
        Assert.Equal($"/api/results/{id}/thumbnail", root.GetProperty("thumbnailUrl").GetString());
        Assert.EndsWith("Z", root.GetProperty("createdAt").GetString());   // ISO 8601, UTC
        Assert.Equal(1, await factory.Results.CountAsync());
    }

    [Fact]
    public async Task AC19_Get_ReturnsWorksNewestFirst()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var first = await PostOkAsync(client);
        var second = await PostOkAsync(client);

        var response = await client.GetAsync("/api/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var items = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(new[] { second, first }, items.Select(i => i.GetProperty("id").GetInt32()));
        Assert.All(items, i => Assert.Equal(ItemName, i.GetProperty("itemName").GetString()));
    }

    [Fact]
    public async Task AC19_Get_EmptyGallery_ReturnsEmptyArray()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(0, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task AC19_GetImage_ReturnsStoredPng()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var id = await PostOkAsync(client);

        var response = await client.GetAsync($"/api/results/{id}/image");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var info = Image.Identify(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.IsType<PngFormat>(info.Metadata.DecodedImageFormat);
        Assert.Equal(40, info.Width);
        Assert.Equal(30, info.Height);
    }

    [Fact]
    public async Task AC19_GetThumbnail_ReturnsJpeg()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var id = await PostOkAsync(client);

        var response = await client.GetAsync($"/api/results/{id}/thumbnail");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        var info = Image.Identify(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.IsType<JpegFormat>(info.Metadata.DecodedImageFormat);
    }

    // ---------- 404 ----------

    [Theory]
    [InlineData("image")]
    [InlineData("thumbnail")]
    public async Task Get_UnknownId_Returns404Problem(string kind)
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync($"/api/results/999/{kind}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, ResultEndpoints.NotFoundTitle);
    }

    // ---------- AC-22: видалення ----------

    [Fact]
    public async Task AC22_Delete_Returns204_ThenWorkIsGone_AndSecondDeleteIs404()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var id = await PostOkAsync(client);

        var deleted = await client.DeleteAsync($"/api/results/{id}");
        var imageAfter = await client.GetAsync($"/api/results/{id}/image");
        var deletedAgain = await client.DeleteAsync($"/api/results/{id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, imageAfter.StatusCode);
        await AssertProblemAsync(deletedAgain, HttpStatusCode.NotFound, ResultEndpoints.NotFoundTitle);
        Assert.Equal(0, await factory.Results.CountAsync());
    }

    // ---------- AC-23: ліміт галереї (R7) через HTTP ----------

    [Fact]
    public async Task AC23_Post_OverLimit_EvictsOldest_AndListKeepsLimit()
    {
        using var factory = new ApiFactory(new GalleryOptions { MaxItems = 3, SavesPerMinute = 1000 });
        var client = factory.CreateClient();
        var ids = new List<int>();
        for (var i = 0; i < 4; i++)
            ids.Add(await PostOkAsync(client));

        var response = await client.GetAsync("/api/results");

        using var json = await ReadJsonAsync(response);
        var listed = json.RootElement.EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
        Assert.Equal(new[] { ids[3], ids[2], ids[1] }, listed);       // найстарша (ids[0]) витіснена, нова є
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/results/{ids[0]}/image")).StatusCode);
    }

    // ---------- AC-24: розмір → 413 ----------

    [Fact]
    public async Task AC24_Post_OverFiveMegabytes_Returns413_AndStoresNothing()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var tooBig = new byte[5 * 1024 * 1024 + 1];

        var response = await client.PostAsync("/api/results", Form(tooBig));

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, ResultEndpoints.TooLargeTitle);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("Максимальний розмір — 5 МБ.", json.RootElement.GetProperty("detail").GetString());   // текст із docs/api.md
        Assert.Equal(0, await factory.Results.CountAsync());
    }

    // ---------- AC-24: частота → 429 ----------

    [Fact]
    public async Task AC24_Post_SixthWithinMinute_Returns429_ButOtherEndpointsStillWork()
    {
        using var factory = new ApiFactory(new GalleryOptions { SavesPerMinute = 5 });
        var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
            await PostOkAsync(client);

        var sixth = await client.PostAsync("/api/results", Form(Png()));
        var list = await client.GetAsync("/api/results");

        await AssertProblemAsync(sixth, HttpStatusCode.TooManyRequests, GalleryRateLimiting.TooManyRequestsTitle);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);                  // обмежується лише збереження
        Assert.Equal(5, await factory.Results.CountAsync());               // шоста не збережена
    }

    [Fact]
    public async Task AC19_Post_LocationLeadsToTheStoredImage()
    {
        // Location має вказувати на існуючу адресу: за нею одразу можна отримати PNG збереженої роботи.
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var created = await client.PostAsync("/api/results", Form(Png()));

        var followed = await client.GetAsync(created.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
        Assert.Equal("image/png", followed.Content.Headers.ContentType?.MediaType);
    }

    // ---------- AC-24: роздільність → 413 ----------

    [Fact]
    public async Task AC24_Post_TooManyPixels_Returns413_AndStoresNothing()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsync("/api/results", Form(HugePng.Create()));

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, ResultEndpoints.TooLargeTitle);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(ResultService.TooManyPixelsMessage, json.RootElement.GetProperty("detail").GetString());
        Assert.Equal(0, await factory.Results.CountAsync());
    }

    // ---------- 400 ----------

    [Fact]
    public async Task Post_Jpeg_Returns400_AndStoresNothing()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsync("/api/results", Form(TestImages.RoomJpeg(64, 48)));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ResultEndpoints.BadRequestTitle);
        Assert.Equal(0, await factory.Results.CountAsync());
    }

    [Fact]
    public async Task Post_UnknownItem_Returns400()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsync("/api/results", Form(Png(), catalogItemId: "no-such-item"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ResultEndpoints.BadRequestTitle);
        Assert.Equal(0, await factory.Results.CountAsync());
    }

    [Fact]
    public async Task Post_MissingCatalogItemId_Returns400()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsync("/api/results", Form(Png(), catalogItemId: null));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ResultEndpoints.BadRequestTitle);
    }

    [Fact]
    public async Task Post_MissingImage_Returns400()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsync("/api/results", Form(image: null));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ResultEndpoints.BadRequestTitle);
    }

    [Fact]
    public async Task Post_NotMultipart_Returns400()
    {
        using var factory = new ApiFactory();
        var body = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await factory.CreateClient().PostAsync("/api/results", body);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ResultEndpoints.BadRequestTitle);
    }
}
