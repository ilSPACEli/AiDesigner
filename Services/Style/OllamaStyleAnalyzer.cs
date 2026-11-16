using System.Net.Http.Json;
using System.Text.Json;
using AiDesigner.Models;

namespace AiDesigner.Services.Style;

public sealed class OllamaStyleAnalyzer : IStyleAnalyzer
{
    public const string NotRunningReason = "Ollama не запущена на цьому комп'ютері.";
    public const string NoVisionModelReason = "В Ollama немає моделі з підтримкою зображень (наприклад: ollama pull moondream).";
    public const string QualityWarning = "Якість визначення стилю залежить від локальної моделі.";

    public static string ModelNotInstalledReason(string model) => $"Модель «{model}» не встановлено в Ollama.";

    public static string ModelWithoutVisionReason(string model) => $"Модель «{model}» не підтримує зображення.";

    private static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaStyleAnalyzer(HttpClient http, AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options.Ollama;
    }

    public AnalysisMode Mode => AnalysisMode.LOCAL_AI;

    public async Task<ModeAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(AvailabilityTimeout);
        try
        {
            var (_, reason) = await ResolveModelAsync(timeout.Token);
            return reason is null ? ModeAvailability.Available(Mode) : ModeAvailability.Unavailable(Mode, reason);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException
                                   && !cancellationToken.IsCancellationRequested)
        {
            return ModeAvailability.Unavailable(Mode, NotRunningReason);
        }
    }

    public async Task<string> AnalyzeStyleAsync(byte[] image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        var (model, reason) = await ResolveModelAsync(cancellationToken);
        if (model is null)
            throw new InvalidOperationException(reason);

        var body = new
        {
            model,
            prompt = StylePrompt.Text,
            images = new[] { Convert.ToBase64String(image) },
            format = StylePrompt.JsonSchema,
            stream = false,
            options = new { temperature = 0 }
        };

        using var response = await _http.PostAsJsonAsync("api/generate", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return json.RootElement.TryGetProperty("response", out var text) && text.ValueKind == JsonValueKind.String
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }

    private async Task<(string? Model, string? Reason)> ResolveModelAsync(CancellationToken cancellationToken)
    {
        var installed = await GetInstalledModelsAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Model))
        {
            var configured = installed.FirstOrDefault(name => SameModel(name, _options.Model));
            if (configured is null)
                return (null, ModelNotInstalledReason(_options.Model));
            return await SupportsImagesAsync(configured, cancellationToken)
                ? (configured, null)
                : (null, ModelWithoutVisionReason(_options.Model));
        }

        foreach (var name in installed)
        {
            if (await SupportsImagesAsync(name, cancellationToken))
                return (name, null);
        }
        return (null, NoVisionModelReason);
    }

    private async Task<IReadOnlyList<string>> GetInstalledModelsAsync(CancellationToken cancellationToken)
    {
        using var json = await GetJsonAsync(_http.GetAsync("api/tags", cancellationToken), cancellationToken);
        if (!json.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return models.EnumerateArray()
            .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
            .OfType<string>()
            .ToList();
    }

    private async Task<bool> SupportsImagesAsync(string model, CancellationToken cancellationToken)
    {
        using var json = await GetJsonAsync(_http.PostAsJsonAsync("api/show", new { model }, cancellationToken), cancellationToken);
        var root = json.RootElement;

        if (root.TryGetProperty("capabilities", out var capabilities) && capabilities.ValueKind == JsonValueKind.Array)
            return capabilities.EnumerateArray().Any(c => c.GetString() == "vision");

        return root.TryGetProperty("details", out var details)
            && details.TryGetProperty("families", out var families) && families.ValueKind == JsonValueKind.Array
            && families.EnumerateArray().Any(f => f.GetString() is "clip" or "mllama");
    }

    private static async Task<JsonDocument> GetJsonAsync(Task<HttpResponseMessage> request, CancellationToken cancellationToken)
    {
        using var response = await request;
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
    }

    // "moondream" і "moondream:latest" — та сама модель.
    private static bool SameModel(string installed, string configured) =>
        string.Equals(installed, configured, StringComparison.OrdinalIgnoreCase)
        || string.Equals(installed, configured + ":latest", StringComparison.OrdinalIgnoreCase);
}
