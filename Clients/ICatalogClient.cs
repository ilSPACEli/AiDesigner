using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public interface ICatalogClient
{
    Task<IReadOnlyList<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
