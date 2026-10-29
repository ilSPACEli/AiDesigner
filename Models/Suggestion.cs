namespace AiDesigner.Models;


public class Suggestion
{
    public CatalogItem Item { get; init; } = new();

    public int Rank { get; init; }

    public int Score { get; init; }

    public string Reason { get; init; } = string.Empty;
}
