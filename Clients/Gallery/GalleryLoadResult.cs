namespace AiDesigner.Clients.Gallery;

public sealed record GalleryLoadResult(IReadOnlyList<GalleryItemView> Items, string? Error, string? Notice = null)
{
    public bool IsSuccess => Error is null;

    public static GalleryLoadResult Success(IReadOnlyList<GalleryItemView> items, string? notice = null) =>
        new(items, null, notice);

    public static GalleryLoadResult Fail(string error) => new(Array.Empty<GalleryItemView>(), error);
}
