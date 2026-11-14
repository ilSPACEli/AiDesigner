namespace AiDesigner.Services.Results;

public sealed class GalleryRetentionPolicy
{
    public GalleryRetentionPolicy(int maxItems)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItems);
        MaxItems = maxItems;
    }

    public int MaxItems { get; }

    public int CountToEvict(int currentCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentCount);

        return Math.Max(0, currentCount - MaxItems + 1);
    }
}