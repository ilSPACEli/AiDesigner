using AiDesigner.Models;

namespace AiDesigner.Clients.Gallery;

public sealed class GalleryFacade
{
    private readonly IGalleryClient _client;

    public GalleryFacade(IGalleryClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public static string NoWorksOfStyleMessage(RoomStyle style) =>
        $"Робіт зі стилем «{DisplayNames.Of(style)}» ще немає.";

    public async Task<GalleryLoadResult> LoadAsync(
        string? timeZoneId, RoomStyle? style = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var zone = BrowserTimeZone.Resolve(timeZoneId);
            var results = await _client.GetAllAsync(style, cancellationToken);
            var items = results.Select(r => GalleryItemView.From(r, zone)).ToList();
            var notice = items.Count == 0 && style is { } value ? NoWorksOfStyleMessage(value) : null;
            return GalleryLoadResult.Success(items, notice);
        }
        catch (Exception ex)
        {
            return GalleryLoadResult.Fail(GalleryErrorMessages.FromException(ex));
        }
    }

    public async Task<GalleryDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.DeleteAsync(id, cancellationToken)
                ? GalleryDeleteResult.Deleted
                : GalleryDeleteResult.NotFound;
        }
        catch (Exception ex)
        {
            return GalleryDeleteResult.Failed(GalleryErrorMessages.FromException(ex));
        }
    }
}
