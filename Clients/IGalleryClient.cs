using AiDesigner.Api.Dto;
using AiDesigner.Models;

namespace AiDesigner.Clients;

public interface IGalleryClient
{
    Task<GallerySaveResult> SaveAsync(
        byte[] pngBytes, string catalogItemId, ResultAnalysisDto analysis, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResultDto>> GetAllAsync(RoomStyle? style = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}