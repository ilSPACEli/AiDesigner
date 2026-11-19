namespace AiDesigner.Clients.Gallery;

public sealed record GalleryLoadResult(IReadOnlyList<GalleryItemView> Items, string? Error)
{
    public bool IsSuccess => Error is null;

    public static GalleryLoadResult Success(IReadOnlyList<GalleryItemView> items) => new(items, null);

    public static GalleryLoadResult Fail(string error) => new(Array.Empty<GalleryItemView>(), error);
}
