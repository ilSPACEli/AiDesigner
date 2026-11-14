using AiDesigner.Models;

namespace AiDesigner.Api.Dto;

public sealed record CatalogItemDto(
    string Id,
    string Name,
    string Type,
    string ImageUrl,
    string MainColor,
    IReadOnlyList<string> SuitableLightings,
    string LicenseSource)
{
    public static CatalogItemDto From(CatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new CatalogItemDto(
            item.Id,
            item.Name,
            item.Type.ToString(),
            "/" + item.ImageFile.TrimStart('/'),
            item.MainColor,
            item.SuitableLightings.Select(level => level.ToString()).ToList(),
            item.LicenseSource);
    }

    public CatalogItem ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Type = Enum.Parse<ItemType>(Type),
        ImageFile = ImageUrl.TrimStart('/'),
        MainColor = MainColor,
        SuitableLightings = SuitableLightings.Select(level => Enum.Parse<LightingLevel>(level)).ToArray(),
        LicenseSource = LicenseSource
    };
}
