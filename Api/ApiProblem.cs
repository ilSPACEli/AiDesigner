using Microsoft.AspNetCore.Mvc;

namespace AiDesigner.Api;

public static class ApiProblem
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };

    public static IResult Result(int status, string title, string detail) =>
        Results.Json(Create(status, title, detail), contentType: ContentType, statusCode: status);
}