using AiDesigner.Models;

namespace AiDesigner.Api.Dto;


public sealed record ResultDto(
    int Id,
    DateTime CreatedAt,
    string CatalogItemId,
    string ItemName,
    string ImageUrl,
    string ThumbnailUrl)
{
    public static ResultDto From(SavedResult result) => new(
        result.Id,
        result.CreatedAt,
        result.CatalogItemId,
        result.CatalogItem?.Name ?? result.CatalogItemId,
        $"/api/results/{result.Id}/image",
        $"/api/results/{result.Id}/thumbnail");
}