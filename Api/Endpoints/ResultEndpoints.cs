using System.Text.Json;
using AiDesigner.Api.Dto;
using AiDesigner.Models;
using AiDesigner.Services.Results;
using Microsoft.AspNetCore.RateLimiting;

namespace AiDesigner.Api.Endpoints;

public static class ResultEndpoints
{
    public const string BadRequestTitle = "Некоректний запит";
    public const string UnknownStyleMessage = "style: невідомий стиль.";
    public const string TooLargeTitle = "Зображення завелике";
    public const string NotFoundTitle = "Роботу не знайдено";
    public const string SaveFailedTitle = "Помилка збереження";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapResultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/results");

        group.MapPost("/", SaveAsync)
            .RequireRateLimiting(GalleryRateLimiting.SaveResultPolicy)
            .DisableAntiforgery();

        group.MapGet("/", GetAllAsync);
        group.MapGet("/{id:int}/image", GetImageAsync);
        group.MapGet("/{id:int}/thumbnail", GetThumbnailAsync);
        group.MapDelete("/{id:int}", DeleteAsync);

        return app;
    }

    // POST /api/results  (multipart/form-data: image, catalogItemId)
    private static async Task<IResult> SaveAsync(
        HttpRequest request, ResultService service, GalleryOptions options, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
            return ApiProblem.Result(StatusCodes.Status400BadRequest, BadRequestTitle,
                "Очікується multipart/form-data з полями image і catalogItemId.");

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return ApiProblem.Result(StatusCodes.Status400BadRequest, BadRequestTitle,
                "Тіло запиту не є коректним multipart/form-data.");
        }

        var image = form.Files.GetFile("image");
        if (image is null)
            return ApiProblem.Result(StatusCodes.Status400BadRequest, BadRequestTitle,
                "Не передано файл у полі image.");

        var catalogItemId = form["catalogItemId"].ToString();

        var analysis = ReadAnalysis(form["analysis"].ToString(), out var analysisError);
        if (analysisError is not null)
            return ApiProblem.Result(StatusCodes.Status400BadRequest, BadRequestTitle, analysisError);

        var length = (int)Math.Min(image.Length, (long)options.MaxImageBytes + 1);
        var bytes = new byte[length];
        await using (var stream = image.OpenReadStream())
            await stream.ReadExactlyAsync(bytes, cancellationToken);

        var outcome = await service.SaveAsync(bytes, catalogItemId, analysis, cancellationToken);

        return outcome.Status switch
        {
            SaveStatus.Saved => Created(ResultDto.From(outcome.Result!)),
            SaveStatus.BadRequest => ApiProblem.Result(StatusCodes.Status400BadRequest, BadRequestTitle, outcome.Message),
            SaveStatus.TooLarge => ApiProblem.Result(StatusCodes.Status413PayloadTooLarge, TooLargeTitle, outcome.Message),
            SaveStatus.Failed => ApiProblem.Result(StatusCodes.Status500InternalServerError, SaveFailedTitle, outcome.Message),
            _ => throw new InvalidOperationException($"Невідомий статус збереження: {outcome.Status}.")
        };
    }

    // GET /api/results?style=LOFT: нові спершу; без style — уся галерея
    private static async Task<IResult> GetAllAsync(string? style, ResultService service, CancellationToken cancellationToken)
    {
        if (style is not null && !Enum.GetNames<RoomStyle>().Contains(style))
            return ApiProblem.Result(StatusCodes.Status400BadRequest, BadRequestTitle, UnknownStyleMessage);

        var filter = style is null ? (RoomStyle?)null : Enum.Parse<RoomStyle>(style);
        var results = await service.GetAllAsync(filter, cancellationToken);
        return Results.Ok(results.Select(ResultDto.From).ToList());
    }

    // GET /api/results/{id}/image: PNG. З бази читається лише стовпець ImageData
    private static async Task<IResult> GetImageAsync(int id, ResultService service, CancellationToken cancellationToken)
    {
        var image = await service.GetImageAsync(id, cancellationToken);
        return image is null ? NotFound(id) : Results.File(image, "image/png");
    }

    // GET /api/results/{id}/thumbnail: JPEG. З бази читається лише стовпець ThumbnailData.
    private static async Task<IResult> GetThumbnailAsync(int id, ResultService service, CancellationToken cancellationToken)
    {
        var thumbnail = await service.GetThumbnailAsync(id, cancellationToken);
        return thumbnail is null ? NotFound(id) : Results.File(thumbnail, "image/jpeg");
    }

    // DELETE /api/results/{id}: 204; повторне видалення - 404
    private static async Task<IResult> DeleteAsync(int id, ResultService service, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted ? Results.NoContent() : NotFound(id);
    }

    private static ResultAnalysis? ReadAnalysis(string json, out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            error = ResultService.NoAnalysisMessage;
            return null;
        }

        ResultAnalysisDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ResultAnalysisDto>(json, Json);
        }
        catch (JsonException)
        {
            dto = null;
        }

        if (dto is null)
        {
            error = "analysis: очікується JSON-об'єкт ResultAnalysisDto.";
            return null;
        }

        return dto.ToModel(out error);
    }

    private static IResult Created(ResultDto dto) => Results.Created(dto.ImageUrl, dto);

    private static IResult NotFound(int id) =>
        ApiProblem.Result(StatusCodes.Status404NotFound, NotFoundTitle, $"Роботи з id {id} немає.");
}
