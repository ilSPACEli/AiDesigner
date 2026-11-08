using AiDesigner.Api.Dto;
using AiDesigner.Clients;

namespace AiDesigner.Tests.Clients;

    
public sealed class RecordingGalleryClient : IGalleryClient
{
    private int _nextId = 1;

    public List<(byte[] Png, string CatalogItemId)> Saves { get; } = new();

    public string? SaveFailureMessage { get; set; }

    public Exception? SaveException { get; set; }

    public Task<GallerySaveResult> SaveAsync(byte[] pngBytes, string catalogItemId, CancellationToken cancellationToken = default)
    {
        Saves.Add((pngBytes, catalogItemId));

        if (SaveException is not null)
            return Task.FromException<GallerySaveResult>(SaveException);

        if (SaveFailureMessage is not null)
            return Task.FromResult(GallerySaveResult.Fail(SaveFailureMessage));

        var id = _nextId++;
        var dto = new ResultDto(id, DateTime.UtcNow, catalogItemId, catalogItemId,
            $"/api/results/{id}/image", $"/api/results/{id}/thumbnail");
        return Task.FromResult(GallerySaveResult.Success(dto));
    }

    public Task<IReadOnlyList<ResultDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ResultDto>>(Array.Empty<ResultDto>());

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}