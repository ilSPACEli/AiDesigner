using AiDesigner.Models;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Services.Analysis;


public static class PaletteBuilder
{
    public const int MinColors = 3;
    public const int MaxColors = 5;

    public const double MinShare = 0.05;

    public const int MergeDistance = 20;


    public static bool TryBuild(IReadOnlyList<ColorCluster> clusters, out IReadOnlyList<DominantColor> palette)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        palette = Array.Empty<DominantColor>();

        // 1. Порожні кластери не потрібні; великі — першими.
        //    OrderByDescending стабільний: за рівної кількості порядок не змінюється.
        var sorted = clusters.Where(c => c.Count > 0).OrderByDescending(c => c.Count);

        // 2. Склеїти майже однакові кольори.
        var groups = Merge(sorted);
        if (groups.Count < MinColors)
            return false;

        // 3. Відкинути дрібні (< 5 % усіх пікселів), але лишити щонайменше 3 і не більше 5.
        var total = groups.Sum(g => g.Count);
        var selected = groups.Where(g => (double)g.Count / total >= MinShare).ToList();
        if (selected.Count < MinColors)
            selected = groups.Take(MinColors).ToList();
        selected = selected.Take(MaxColors).ToList();

        // 4. Частки серед обраних кольорів, сума рівно 100 %.
        var shares = SharesInTenths(selected.Select(g => g.Count).ToList());

        palette = selected
            .Select((g, i) =>
            {
                var color = g.Center;
                return new DominantColor
                {
                    Hex = ColorMath.ToHex(color),
                    Hue = ColorMath.Hue(color),
                    Share = shares[i] / 10m
                };
            })
            .ToList();
        return true;
    }

    private static List<Group> Merge(IEnumerable<ColorCluster> sorted)
    {
        var groups = new List<Group>();
        foreach (var cluster in sorted)
        {
            var target = groups.FirstOrDefault(g =>
                KMeans.Distance2(g.Center, cluster.Center) < MergeDistance * MergeDistance);

            if (target is null)
                groups.Add(new Group(cluster));
            else
                target.Add(cluster);
        }

        // Після склеювання порядок за розміром міг змінитися.
        return groups.OrderByDescending(g => g.Count).ToList();
    }

    internal static int[] SharesInTenths(IReadOnlyList<int> counts)
    {
        const int Total = 1000;
        var sum = counts.Sum();
        var exact = counts.Select(c => (double)c * Total / sum).ToArray();
        var result = exact.Select(e => (int)Math.Floor(e)).ToArray();

        var missing = Total - result.Sum();
        var byRemainder = Enumerable.Range(0, counts.Count)
            .OrderByDescending(i => exact[i] - result[i])
            .ThenBy(i => i)
            .Take(missing);
        foreach (var i in byRemainder)
            result[i]++;

        return result;
    }

    private sealed class Group
    {
        private double _r, _g, _b;

        public int Count { get; private set; }

        public Group(ColorCluster cluster) => Add(cluster);

        public Rgb24 Center => new(
            (byte)Math.Round(_r / Count),
            (byte)Math.Round(_g / Count),
            (byte)Math.Round(_b / Count));

        public void Add(ColorCluster cluster)
        {
            _r += cluster.Center.R * (double)cluster.Count;
            _g += cluster.Center.G * (double)cluster.Count;
            _b += cluster.Center.B * (double)cluster.Count;
            Count += cluster.Count;
        }
    }
}
