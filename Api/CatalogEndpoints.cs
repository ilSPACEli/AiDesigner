using AiDesigner.Api.Dto;
using AiDesigner.Repositories;
using AiDesigner.Services.Suggestions;
using Microsoft.AspNetCore.Mvc;

namespace AiDesigner.Api;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog", async (ICatalogRepository repository, CancellationToken ct) =>
        {
            try
            {
                var items = await repository.GetAllAsync(ct);
                
                var dtos = items.Select(i => new CatalogItemDto 
                { 
                    Id = i.Id, 
                    Type = i.Type.ToString(), 
                    SuitableLightings = string.Join(",", i.SuitableLightings)
                }).ToArray();
                
                return Results.Ok(dtos);
            }
            catch
            {
                return Results.Problem(statusCode: 503, detail: "Каталог недоступний");
            }
        });

        app.MapPost("/api/suggestions", async ([FromBody] AnalysisRequestDto request, SuggestionService service, CancellationToken ct) =>
        {
            if (request == null)
            {
                return Results.Problem(statusCode: 400, detail: "Некоректний запит");
            }

            try
            {
                var suggestions = await service.GetSuggestionsAsync(request, ct);
                var suggestedList = suggestions.ToList();

                if (suggestedList.Count == 0)
                {
                    return Results.Problem(statusCode: 422, detail: "Придатних предметів не знайдено");
                }

                var dtos = suggestedList.Select(s => new SuggestionDto { Id = s.Id }).ToArray();
                
                return Results.Ok(dtos);
            }
            catch
            {
                return Results.Problem(statusCode: 503, detail: "Каталог недоступний");
            }
        });
    }
}