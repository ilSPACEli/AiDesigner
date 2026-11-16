using System.Net;
using System.Text.Json;
using AiDesigner.Models;
using AiDesigner.Services.Style;

namespace AiDesigner.Tests.Style;

public class GeminiStyleAnalyzerTests
{
    private const string Key = "test-key";

    private static (GeminiStyleAnalyzer Analyzer, StubHttpHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond, string? key = Key)
    {
        var handler = new StubHttpHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri(GeminiStyleAnalyzer.BaseUrl) };
        var options = new AiOptions { Gemini = new GeminiOptions { ApiKey = key, Model = "gemini-2.5-flash" } };
        return (new GeminiStyleAnalyzer(http, options), handler);
    }

    private static string Answer(string text) =>
        JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text } } } } } });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AC26_NoKey_Unavailable_WithoutRequest(string? key)
    {
        var (analyzer, handler) = Create(_ => throw new InvalidOperationException("не має викликатися"), key);

        var availability = await analyzer.CheckAvailabilityAsync();

        Assert.False(availability.IsAvailable);
        Assert.Equal(AnalysisMode.CLOUD_AI, availability.Mode);
        Assert.Equal(GeminiStyleAnalyzer.NoKeyReason, availability.Reason);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task KeyAndServiceAnswer_Available_KeyInHeaderNotInUrl()
    {
        var (analyzer, handler) = Create(_ => StubHttpHandler.Json("{}"));

        var availability = await analyzer.CheckAvailabilityAsync();

        Assert.True(availability.IsAvailable);
        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal("/v1beta/models/gemini-2.5-flash", request.RequestUri!.AbsolutePath);
        Assert.Equal(Key, request.Headers.GetValues("x-goog-api-key").Single());
        Assert.DoesNotContain(Key, request.RequestUri.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, GeminiStyleAnalyzer.InvalidKeyReason)]
    [InlineData(HttpStatusCode.BadRequest, GeminiStyleAnalyzer.InvalidKeyReason)]
    [InlineData(HttpStatusCode.ServiceUnavailable, GeminiStyleAnalyzer.UnreachableReason)]
    public async Task ServiceRefuses_Unavailable_WithReason(HttpStatusCode status, string reason)
    {
        var (analyzer, _) = Create(_ => StubHttpHandler.Json("{}", status));

        var availability = await analyzer.CheckAvailabilityAsync();

        Assert.False(availability.IsAvailable);
        Assert.Equal(reason, availability.Reason);
    }

    [Fact]
    public async Task NoInternet_Unavailable()
    {
        var (analyzer, _) = Create(_ => throw new HttpRequestException("немає мережі"));

        var availability = await analyzer.CheckAvailabilityAsync();

        Assert.False(availability.IsAvailable);
        Assert.Equal(GeminiStyleAnalyzer.UnreachableReason, availability.Reason);
    }

    [Fact]
    public async Task Analyze_SendsPromptImageAndSchema_ReturnsModelText()
    {
        var (analyzer, handler) = Create(_ => StubHttpHandler.Json(Answer("""{"style":"LOFT","confidence":0.82}""")));
        var image = new byte[] { 1, 2, 3 };

        var text = await analyzer.AnalyzeStyleAsync(image);

        Assert.Equal("""{"style":"LOFT","confidence":0.82}""", text);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1beta/models/gemini-2.5-flash:generateContent", request.RequestUri!.AbsolutePath);
        Assert.Equal(Key, request.Headers.GetValues("x-goog-api-key").Single());

        using var json = JsonDocument.Parse(body!);
        var parts = json.RootElement.GetProperty("contents")[0].GetProperty("parts");
        Assert.Equal(StylePrompt.Text, parts[0].GetProperty("text").GetString());
        Assert.Equal("image/jpeg", parts[1].GetProperty("inlineData").GetProperty("mimeType").GetString());
        Assert.Equal(Convert.ToBase64String(image), parts[1].GetProperty("inlineData").GetProperty("data").GetString());
        var config = json.RootElement.GetProperty("generationConfig");
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        var styles = config.GetProperty("responseSchema").GetProperty("properties").GetProperty("style").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString());
        Assert.Equal(Enum.GetNames<RoomStyle>(), styles);
    }

    [Fact]
    public async Task Analyze_NoCandidates_ReturnsEmptyText()
    {
        var (analyzer, _) = Create(_ => StubHttpHandler.Json("""{"promptFeedback":{"blockReason":"SAFETY"}}"""));

        Assert.Equal(string.Empty, await analyzer.AnalyzeStyleAsync(new byte[] { 1 }));
    }

    [Fact]
    public async Task Analyze_ServiceError_Throws()
    {
        var (analyzer, _) = Create(_ => StubHttpHandler.Json("{}", HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => analyzer.AnalyzeStyleAsync(new byte[] { 1 }));
    }

    [Fact]
    public async Task Analyze_NoKey_Throws_WithoutRequest()
    {
        var (analyzer, handler) = Create(_ => StubHttpHandler.Json("{}"), key: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => analyzer.AnalyzeStyleAsync(new byte[] { 1 }));
        Assert.Empty(handler.Requests);
    }
}
