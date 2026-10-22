namespace AiDesigner.Models;


public class CatalogItem
{

    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public ItemType Type { get; init; }

    /// <summary>Шлях до PNG із прозорим фоном відносно wwwroot.</summary>
    public string ImageFile { get; init; } = string.Empty;

    public string MainColor { get; init; } = string.Empty;

    public IReadOnlyList<LightingLevel> SuitableLightings { get; init; } = Array.Empty<LightingLevel>();

    public string LicenseSource { get; init; } = string.Empty;
}
