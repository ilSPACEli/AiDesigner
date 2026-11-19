namespace AiDesigner.Clients.Gallery;

public sealed class GalleryFacade
{
    private readonly IGalleryClient _client;

    public GalleryFacade(IGalleryClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<GalleryLoadResult> LoadAsync(string? timeZoneId, CancellationToken cancellationToken = default)
    {
        try
        {
            var zone = BrowserTimeZone.Resolve(timeZoneId);
            var results = await _client.GetAllAsync(cancellationToken);
            return GalleryLoadResult.Success(results.Select(r => GalleryItemView.From(r, zone)).ToList());
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
