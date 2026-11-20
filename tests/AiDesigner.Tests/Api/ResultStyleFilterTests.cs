using System.Net;
using System.Text.Json;
using AiDesigner.Api;
using AiDesigner.Api.Dto;
using AiDesigner.Api.Endpoints;
using AiDesigner.Clients;
using AiDesigner.Tests.Analysis;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Api;

// GET /api/results?style= (S3, FR-22) через справжній Program зі сховищем у пам'яті.
public class ResultStyleFilterTests
{
    private static readonly ResultAnalysisDto Loft = new(
        "CLOUD_AI", "MODERATE",
        new[] { new ColorDto("#D8C3A5", 36, 61.2m), new ColorDto("#322D28", 30, 30.6m), new ColorDto("#9A8F80", 35, 8.2m) },
        "LOFT", 0.82m);

    private static readonly ResultAnalysisDto Minimalism = Loft with { Style = "MINIMALISM" };

    private static readonly ResultAnalysisDto Basic = Loft with { Mode = "BASIC", Style = null, Confidence = null };

    private static async Task<int> SaveAsync(ApiFactory factory, ResultAnalysisDto analysis)
    {
        var png = TestImages.Solid(40, 30, new Rgb24(200, 100, 50));
        var saved = await new GalleryClient(factory.CreateClient()).SaveAsync(png, "sofa-01", analysis);
        Assert.True(saved.IsSuccess, saved.Message);
        return saved.Result!.Id;
    }

    private static async Task<(int Id, string? Style)[]> GetOkAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray()
            .Select(e => (e.GetProperty("id").GetInt32(),
                e.TryGetProperty("style", out var style) ? style.GetString() : null))
            .ToArray();
    }

    [Fact]
    public async Task AC36_GetByStyle_Loft_ReturnsOnlyLoftWorks_NewestFirst()
    {
        using var factory = new ApiFactory();
        var loftOld = await SaveAsync(factory, Loft);
        await SaveAsync(factory, Minimalism);
        await SaveAsync(factory, Basic);
        var loftNew = await SaveAsync(factory, Loft);

        var works = await GetOkAsync(factory.CreateClient(), "/api/results?style=LOFT");

        Assert.Equal(new[] { loftNew, loftOld }, works.Select(w => w.Id));
        Assert.All(works, w => Assert.Equal("LOFT", w.Style));
    }

    [Fact]
    public async Task AC38_GetWithoutStyle_ReturnsWholeGalleryAsBefore()
    {
        using var factory = new ApiFactory();
        var loft = await SaveAsync(factory, Loft);
        var minimalism = await SaveAsync(factory, Minimalism);
        var basic = await SaveAsync(factory, Basic);

        var works = await GetOkAsync(factory.CreateClient(), "/api/results");

        Assert.Equal(new[] { basic, minimalism, loft }, works.Select(w => w.Id));
    }

    [Fact]
    public async Task AC37_GetByStyle_NoWorksOfStyle_Returns200AndEmptyList()
    {
        using var factory = new ApiFactory();
        await SaveAsync(factory, Loft);

        var works = await GetOkAsync(factory.CreateClient(), "/api/results?style=BOHO");

        Assert.Empty(works);
    }

    [Theory]
    [InlineData("FUTURISM")]
    [InlineData("loft")]
    [InlineData("1")]
    public async Task AC38_GetByStyle_Unknown_Returns400Problem(string style)
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync($"/api/results?style={style}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ApiProblem.ContentType, response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(ResultEndpoints.BadRequestTitle, json.RootElement.GetProperty("title").GetString());
        Assert.Equal(ResultEndpoints.UnknownStyleMessage, json.RootElement.GetProperty("detail").GetString());
    }
}
