namespace AiDesigner.Models;

// Фото, яке користувач явно зберіг у галерею
public class SavedResult
{
    public int Id { get; init; }


    public DateTime CreatedAt { get; init; }

    public string CatalogItemId { get; init; } = string.Empty;

    public CatalogItem? CatalogItem { get; init; }

    public byte[] ImageData { get; init; } = Array.Empty<byte>();
    
    public byte[] ThumbnailData { get; init; } = Array.Empty<byte>();
}
