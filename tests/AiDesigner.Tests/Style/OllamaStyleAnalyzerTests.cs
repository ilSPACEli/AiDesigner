using System.Net;
using System.Text.Json;
using AiDesigner.Models;
using AiDesigner.Services.Style;

namespace AiDesigner.Tests.Style;

public class OllamaStyleAnalyzerTests
{
    private const string Tags = """{"models":[{"name":"llama3:latest"},{"name":"moondream:latest"}]}""";

    private static HttpResponseMessage Show(string model) => model switch
    {
        "moondream:latest" => StubHttpHandler.Json("""{"capabilities":["completion","vision"]}"""),
        "llava:latest" => StubHttpHandler.Json("""{"details":{"families":["llama","clip"]}}"""),
        _ => StubHttpHandler.Json("""{"capabilities":["completion"]}""")
    };

    private static HttpResponseMessage Ollama(HttpRequestMessage request, string tags, string answer = "")
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/api/tags")
            return StubHttpHandler.Json(tags);
        if (path == "/api/show")
        {
            var model = JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result).RootElement.GetProperty("model").GetString()!;
            return Show(model);
        }
        if (path == "/api/generate")
            return StubHttpHandler.Json(JsonSerializer.Serialize(new { response = answer, done = true }));
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static (OllamaStyleAnalyzer Analyzer, StubHttpHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond, string? model = null)
    {
        var handler = new StubHttpHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var options = new AiOptions { Ollama = new OllamaOptions { Model = model } };
        return (new OllamaStyleAnalyzer(http, options), handler);
    }

    // ---------- AC-26: доступність ----------

    [Fact]
    public async Task AC26_NotRunning_Unavailable()
    {
        var (analyzer, _) = Create(_ => throw new HttpRequestException("connection refused"));

        var availability = await analyzer.CheckAvailabilityAsync();

        Assert.False(availability.IsAvailable);
        Assert.Equal(AnalysisMode.LOCAL_AI, availability.Mode);
        Assert.Equal(OllamaStyleAnalyzer.NotRunningReason, availability.Reason);
    }

    [Fact]
    public async Task AC26_NoVisionModel_Unavailable()
    {
        var (analyzer, _) = Create(r => Ollama(r, """{"models":[{"name":"llama3:latest"}]}"""));

        var availability = await analyzer.CheckAvailabilityAsync();

        Assert.False(availability.IsAvailable);
        Assert.Equal(OllamaStyleAnalyzer.NoVisionModelReason, availability.Reason);
    }

    [Fact]
    public async Task AC26_NoModels_Unavailable()
    {
        var (analyzer, _) = Create(r => Ollama(r, """{"models":[]}"""));

        Assert.Equal(OllamaStyleAnalyzer.NoVisionModelReason, (await analyzer.CheckAvailabilityAsync()).Reason);
    }

    [Fact]
    public async Task VisionModelInstalled_Available()
    {
        var (analyzer, _) = Create(r => Ollama(r, Tags));

        Assert.True((await analyzer.CheckAvailabilityAsync()).IsAvailable);
    }

    [Fact]
    public async Task OlderOllama_ClipFamily_CountsAsVision()
    {
        var (analyzer, _) = Create(r => Ollama(r, """{"models":[{"name":"llava:latest"}]}"""));

        Assert.True((await analyzer.CheckAvailabilityAsync()).IsAvailable);
    }

    [Theory]
    [InlineData("moondream", true, null)]
    [InlineData("moondream:latest", true, null)]
    [InlineData("llama3", false, "Модель «llama3» не підтримує зображення.")]
    [InlineData("bakllava", false, "Модель «bakllava» не встановлено в Ollama.")]
    public async Task ConfiguredModel_CheckedByName(string model, bool available, string? reason)
    {
        var (analyzer, _) = Create(r => Ollama(r, Tags), model);

        var availability = await analyzer.CheckAvailabilityAsync();

        Assert.Equal(available, availability.IsAvailable);
        Assert.Equal(reason, availability.Reason);
    }

    // ---------- Аналіз ----------

    [Fact]
    public async Task Analyze_FirstVisionModel_SamePromptAndSchema_ReturnsAnswer()
    {
        var (analyzer, handler) = Create(r => Ollama(r, Tags, """{"style":"BOHO","confidence":0.7}"""));
        var image = new byte[] { 1, 2, 3 };

        var answer = await analyzer.AnalyzeStyleAsync(image);

        Assert.Equal("""{"style":"BOHO","confidence":0.7}""", answer);
        var body = handler.Requests.Single(r => r.Request.RequestUri!.AbsolutePath == "/api/generate").Body!;
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("moondream:latest", root.GetProperty("model").GetString());
        Assert.Equal(StylePrompt.Text, root.GetProperty("prompt").GetString());
        Assert.Equal(Convert.ToBase64String(image), root.GetProperty("images")[0].GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        var styles = root.GetProperty("format").GetProperty("properties").GetProperty("style").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString());
        Assert.Equal(Enum.GetNames<RoomStyle>(), styles);
    }

    [Fact]
    public async Task Analyze_NoVisionModel_Throws_WithoutGenerate()
    {
        var (analyzer, handler) = Create(r => Ollama(r, """{"models":[{"name":"llama3:latest"}]}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => analyzer.AnalyzeStyleAsync(new byte[] { 1 }));
        Assert.DoesNotContain(handler.Requests, r => r.Request.RequestUri!.AbsolutePath == "/api/generate");
    }

    [Fact]
    public async Task Analyze_OllamaError_Throws()
    {
        var (analyzer, _) = Create(r => r.RequestUri!.AbsolutePath == "/api/generate"
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Ollama(r, Tags));

        await Assert.ThrowsAsync<HttpRequestException>(() => analyzer.AnalyzeStyleAsync(new byte[] { 1 }));
    }

    [Fact]
    public async Task Analyze_AnswerGoesThroughR8()
    {
        var (analyzer, _) = Create(r => Ollama(r, Tags, """{"style":"LOFT","confidence":0.4}"""));

        var decision = StyleAcceptanceRule.Apply(await analyzer.AnalyzeStyleAsync(new byte[] { 1 }));

        Assert.Null(decision.Style);
        Assert.Equal(0.4m, decision.Confidence);
    }
}
