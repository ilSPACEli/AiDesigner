using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiDesigner.Models;

namespace AiDesigner.Services.Style;

public sealed class GeminiStyleAnalyzer : IStyleAnalyzer
{
    public const string BaseUrl = "https://generativelanguage.googleapis.com/";
    public const string NoKeyReason = "Не налаштовано ключ Google Gemini.";
    public const string InvalidKeyReason = "Ключ Google Gemini недійсний.";
    public const string UnreachableReason = "Google Gemini недоступний: немає інтернету або сервіс не відповідає.";

    private static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly GeminiOptions _options;

    public GeminiStyleAnalyzer(HttpClient http, AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options.Gemini;
    }

    public AnalysisMode Mode => AnalysisMode.CLOUD_AI;

    private bool HasKey => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<ModeAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (!HasKey)
            return ModeAvailability.Unavailable(Mode, NoKeyReason);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(AvailabilityTimeout);
        try
        {
            using var request = Request(HttpMethod.Get, $"v1beta/models/{_options.Model}");
            using var response = await _http.SendAsync(request, timeout.Token);

            if (response.IsSuccessStatusCode)
                return ModeAvailability.Available(Mode);

            return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? ModeAvailability.Unavailable(Mode, InvalidKeyReason)
                : ModeAvailability.Unavailable(Mode, UnreachableReason);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ModeAvailability.Unavailable(Mode, UnreachableReason);
        }
    }

    public async Task<string> AnalyzeStyleAsync(byte[] image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!HasKey)
            throw new InvalidOperationException(NoKeyReason);

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = StylePrompt.Text },
                        new { inlineData = new { mimeType = StyleImage.MimeType, data = Convert.ToBase64String(image) } }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = StylePrompt.GeminiSchema,
                temperature = 0
            }
        };

        using var request = Request(HttpMethod.Post, $"v1beta/models/{_options.Model}:generateContent");
        request.Content = JsonContent.Create(body);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Gemini {(int)response.StatusCode}: {await ReadErrorAsync(response, cancellationToken)}", null, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return ReadText(json.RootElement);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("x-goog-api-key", _options.ApiKey);
        return request;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                return message.GetString() ?? response.ReasonPhrase ?? string.Empty;
        }
        catch (JsonException)
        {
        }
        return response.ReasonPhrase ?? string.Empty;
    }

    private static string ReadText(JsonElement root)
    {
        if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array
            && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            return text.GetString() ?? string.Empty;

        return string.Empty;
    }
}
