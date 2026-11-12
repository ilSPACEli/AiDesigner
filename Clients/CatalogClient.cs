using System.Net.Http.Json;
using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public class CatalogClient
{
    private readonly HttpClient _httpClient;

    public CatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<CatalogItemDto>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/catalog", cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<CatalogItemDto>();
        }

        var items = await response.Content.ReadFromJsonAsync<IEnumerable<CatalogItemDto>>(cancellationToken: cancellationToken);
        return items ?? Array.Empty<CatalogItemDto>();
    }
}