namespace AiDesigner.Models;


public class Placement
{
    public CatalogItem Item { get; init; } = new();

    public int X { get; private set; }

    public int Y { get; private set; }

    public decimal Scale { get; private set; } = 1.0m;

    public Placement(CatalogItem item, int x, int y, decimal scale = 1.0m)
    {
        Item = item;
        X = x;
        Y = y;
        Resize(scale);   // та сама перевірка «масштаб > 0», що й при зміні розміру
    }

    public void Move(int dx, int dy)
    {
        X += dx;
        Y += dy;
    }


    public void Resize(decimal scale)
    {
        if (scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Масштаб має бути більшим за нуль.");
        Scale = scale;
    }
}
