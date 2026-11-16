using System.Reflection;
using AiDesigner.Models;

namespace AiDesigner.Services.Style;

public static class StylePrompt
{
    private const string ResourceName = "AiDesigner.Services.Style.style-prompt.txt";

    public static string Text { get; } = Load();

    public static IReadOnlyList<string> StyleValues { get; } = Enum.GetNames<RoomStyle>();

    // Ollama: format = JSON Schema.
    public static object JsonSchema { get; } = new
    {
        type = "object",
        properties = new
        {
            style = new { type = "string", @enum = StyleValues },
            confidence = new { type = "number" }
        },
        required = new[] { "style", "confidence" }
    };

    // Gemini: responseSchema (підмножина OpenAPI).
    public static object GeminiSchema { get; } = new
    {
        type = "OBJECT",
        properties = new
        {
            style = new { type = "STRING", @enum = StyleValues },
            confidence = new { type = "NUMBER" }
        },
        required = new[] { "style", "confidence" }
    };

    private static string Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Не знайдено ресурс {ResourceName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
