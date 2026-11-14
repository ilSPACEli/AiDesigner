using AiDesigner.Models;

namespace AiDesigner.Api.Dto;

public sealed record SuggestionDto(int Rank, int Score, string Reason, bool PartialMatch, CatalogItemDto Item)
{
    public static SuggestionDto From(Suggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        return new SuggestionDto(
            suggestion.Rank,
            suggestion.Score,
            suggestion.Reason,
            suggestion.Score == 0,
            CatalogItemDto.From(suggestion.Item));
    }

    public Suggestion ToModel() => new()
    {
        Rank = Rank,
        Score = Score,
        Reason = Reason,
        Item = Item.ToModel()
    };
}
