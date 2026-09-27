namespace AiDesigner.Models;

public sealed class UploadedPhoto(string fileName, string contentType, ReadOnlyMemory<byte> bytes)
{
    public string FileName { get; } = fileName;
    public string ContentType { get; } = contentType;
    public ReadOnlyMemory<byte> Bytes { get; } = bytes;

    public string ToBase64() => Convert.ToBase64String(Bytes.Span);

    // data-URL для Fabric.js.
    public string ToDataUrl() => $"data:{ContentType};base64,{ToBase64()}";
}
