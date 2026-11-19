namespace AiDesigner.Clients.Gallery;

public enum GalleryDeleteStatus
{
    Deleted,
    NotFound,
    Failed
}

public sealed record GalleryDeleteResult(GalleryDeleteStatus Status, string? Error)
{
    public static readonly GalleryDeleteResult Deleted = new(GalleryDeleteStatus.Deleted, null);

    public static readonly GalleryDeleteResult NotFound =
        new(GalleryDeleteStatus.NotFound, GalleryErrorMessages.WorkNotFoundMessage);

    public bool RemoveCard => Status != GalleryDeleteStatus.Failed;

    public static GalleryDeleteResult Failed(string error) => new(GalleryDeleteStatus.Failed, error);
}
