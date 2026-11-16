namespace AiDesigner.Services.Style;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public GeminiOptions Gemini { get; set; } = new();

    public OllamaOptions Ollama { get; set; } = new();
}

public sealed class GeminiOptions
{
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-3.5-flash";
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434/";

    public string? Model { get; set; }
}
