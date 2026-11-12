using System.Net.Http.Json;
using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public sealed class CatalogClient : ICatalogClient
{
    public const string CatalogPath = "api/catalog";

    private readonly HttpClient _http;

    public CatalogClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public async Task<IReadOnlyList<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _http.GetFromJsonAsync<List<CatalogItemDto>>(CatalogPath, cancellationToken);
        return items ?? new List<CatalogItemDto>();
    }
}
