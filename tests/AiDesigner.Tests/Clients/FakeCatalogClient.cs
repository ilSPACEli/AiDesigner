using AiDesigner.Clients;

namespace AiDesigner.Tests.Clients;
internal sealed class FakeCatalogClient : ICatalogClient
{
    private sealed class FakeCatalogClient(Func<IEnumerable<CatalogItemDto>> getCatalog) : ICatalogClient
    {
        public Task<IEnumerable<CatalogItemDto>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(getCatalog());
    }
}