namespace AiDesigner.Services.Results;

public sealed class GalleryOptions
{
    public const string SectionName = "Gallery";

    public int MaxItems { get; set; } = 100;

    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    public int SavesPerMinute { get; set; } = 5;
}