using System.Text.Json;
using AiDesigner.Api.Dto;
using AiDesigner.Services.Suggestions;

namespace AiDesigner.Api.Endpoints;

public static class SuggestionEndpoints
{
    public const string BadRequestTitle = "Некоректний запит";
    public const string NoSuitableItemsTitle = "Придатних предметів немає";
    public const string CatalogUnavailableTitle = "Каталог недоступний";

    public const string NotJsonMessage = "Очікується JSON із полями colors і lighting.";
    public const string NoSuitableItemsMessage = "Для цього освітлення в каталозі немає придатних предметів.";
    public const string CatalogUnavailableMessage = "Каталог предметів недоступний. Спробуйте пізніше.";

    public static IEndpointRouteBuilder MapSuggestionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/suggestions", SuggestAsync)
            .DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> SuggestAsync(
        HttpRequest request, SuggestionService service, CancellationToken cancellationToken)
    {
        AnalysisRequestDto? dto;
        try
        {
            dto = request.HasJsonContentType()
                ? await request.ReadFromJsonAsync<AnalysisRequestDto>(cancellationToken)
                : null;
        }
        catch (JsonException)
        {
            dto = null;
        }

        if (dto is null)
            return ApiProblem.Result(StatusCodes.Status400BadRequest, BadRequestTitle, NotJsonMessage);

        var analysis = dto.ToModel(out var error);
        if (analysis is null)
            return ApiProblem.Result(StatusCodes.Status400BadRequest, BadRequestTitle, error!);

        var outcome = await service.SuggestAsync(analysis, cancellationToken);

        return outcome.Status switch
        {
            SuggestionStatus.Ok => Results.Ok(outcome.Suggestions.Select(SuggestionDto.From).ToList()),
            SuggestionStatus.NoSuitableItems => ApiProblem.Result(
                StatusCodes.Status422UnprocessableEntity, NoSuitableItemsTitle, NoSuitableItemsMessage),
            _ => ApiProblem.Result(
                StatusCodes.Status503ServiceUnavailable, CatalogUnavailableTitle, CatalogUnavailableMessage)
        };
    }
}
