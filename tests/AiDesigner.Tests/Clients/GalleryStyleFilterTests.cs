using System.Net;
using System.Text;
using AiDesigner.Api.Dto;
using AiDesigner.Clients;
using AiDesigner.Clients.Gallery;
using AiDesigner.Models;
using AiDesigner.Tests.Analysis;
using AiDesigner.Tests.Api;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Clients;

// Фільтр за стилем у клієнті й фасаді галереї (S3, FR-22).
public class GalleryStyleFilterTests
{
    private static readonly ResultAnalysisDto Loft = new(
        "CLOUD_AI", "MODERATE",
        new[] { new ColorDto("#D8C3A5", 36, 61.2m), new ColorDto("#322D28", 30, 30.6m), new ColorDto("#9A8F80", 35, 8.2m) },
        "LOFT", 0.82m);

    private static async Task SaveAsync(GalleryClient client, ResultAnalysisDto analysis)
    {
        var saved = await client.SaveAsync(TestImages.Solid(40, 30, new Rgb24(200, 100, 50)), "sofa-01", analysis);
        Assert.True(saved.IsSuccess, saved.Message);
    }

    [Fact]
    public async Task AC38_Client_WithStyle_SendsGetWithStyleQuery()
    {
        var handler = new CapturingHandler();
        var client = new GalleryClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        await client.GetAllAsync(RoomStyle.LOFT);

        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/api/results?style=LOFT", handler.Request.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task AC38_Client_WithoutStyle_SendsGetWithoutQuery()
    {
        var handler = new CapturingHandler();
        var client = new GalleryClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        await client.GetAllAsync();

        Assert.Equal("/api/results", handler.Request!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task AC36_Facade_ByStyle_ShowsOnlyThatStyleCards()
    {
        using var factory = new ApiFactory();
        var client = new GalleryClient(factory.CreateClient());
        await SaveAsync(client, Loft);
        await SaveAsync(client, Loft with { Style = "MINIMALISM" });

        var loaded = await new GalleryFacade(client).LoadAsync("UTC", RoomStyle.LOFT);

        Assert.True(loaded.IsSuccess);
        Assert.Null(loaded.Notice);
        Assert.Equal("Стиль: Лофт", Assert.Single(loaded.Items).Style!.Text);
    }

    [Fact]
    public async Task AC37_Facade_StyleWithoutWorks_EmptyListAndNoticeWithStyleName()
    {
        using var factory = new ApiFactory();
        var client = new GalleryClient(factory.CreateClient());
        await SaveAsync(client, Loft);

        var loaded = await new GalleryFacade(client).LoadAsync("UTC", RoomStyle.BOHO);

        Assert.True(loaded.IsSuccess);
        Assert.Null(loaded.Error);
        Assert.Empty(loaded.Items);
        Assert.Equal(GalleryFacade.NoWorksOfStyleMessage(RoomStyle.BOHO), loaded.Notice);
        Assert.Contains(DisplayNames.Of(RoomStyle.BOHO), loaded.Notice);
    }

    [Fact]
    public async Task Facade_WithoutStyle_EmptyGallery_NoNotice()
    {
        using var factory = new ApiFactory();

        var loaded = await new GalleryFacade(new GalleryClient(factory.CreateClient())).LoadAsync("UTC");

        Assert.True(loaded.IsSuccess);
        Assert.Empty(loaded.Items);
        Assert.Null(loaded.Notice);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
