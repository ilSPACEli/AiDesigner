using AiDesigner.Api.Dto;
using AiDesigner.Repositories;

namespace AiDesigner.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog", GetAllAsync);
        return app;
    }

    private static async Task<IResult> GetAllAsync(ICatalogRepository repository, CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(items.Select(CatalogItemDto.From).ToList());
    }
}
