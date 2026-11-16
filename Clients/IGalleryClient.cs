using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public interface IGalleryClient
{
    Task<GallerySaveResult> SaveAsync(
        byte[] pngBytes, string catalogItemId, ResultAnalysisDto analysis, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResultDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}