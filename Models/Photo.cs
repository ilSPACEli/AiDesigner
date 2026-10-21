namespace AiDesigner.Models;


public class Photo
{
    public string FileName { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }

    public int SizeBytes { get; init; }

    public byte[] Content { get; init; } = Array.Empty<byte>();

    public bool Validate() => true;
}
