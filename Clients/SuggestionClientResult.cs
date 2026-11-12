using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public enum SuggestionClientStatus
{
    // 200
    Success,

    // 422 
    NoSuitableItems,

    // 503
    Unavailable
}

public sealed record SuggestionClientResult(SuggestionClientStatus Status, IReadOnlyList<SuggestionDto> Suggestions)
{
    public static SuggestionClientResult Success(IReadOnlyList<SuggestionDto> suggestions) =>
        new(SuggestionClientStatus.Success, suggestions);

    public static SuggestionClientResult NoSuitableItems() =>
        new(SuggestionClientStatus.NoSuitableItems, Array.Empty<SuggestionDto>());

    public static SuggestionClientResult Unavailable() =>
        new(SuggestionClientStatus.Unavailable, Array.Empty<SuggestionDto>());
}
