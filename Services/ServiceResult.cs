namespace AiDesigner.Services;


public sealed record ServiceResult(bool IsSuccess, string Message)
{
    public static ServiceResult Ok() => new(true, string.Empty);

    public static ServiceResult Fail(string message) => new(false, message);
}
