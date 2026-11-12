using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public interface ICatalogClient
{
    Task<IEnumerable<CatalogItemDto>> GetCatalogAsync(CancellationToken cancellationToken = default);
}