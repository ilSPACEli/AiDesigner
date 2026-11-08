using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;


public sealed record GallerySaveResult(bool IsSuccess, string Message, ResultDto? Result = null)
{
    public static GallerySaveResult Success(ResultDto result) => new(true, string.Empty, result);

    public static GallerySaveResult Fail(string message) => new(false, message);
}