namespace AiDesigner.Models;


public class ResultImage
{
    public string FileName { get; init; } = string.Empty;

    public string Format { get; init; } = "png";

    public DateTime CreatedAt { get; init; }
}
