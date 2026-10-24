using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Services.Analysis;

public readonly record struct ColorCluster(Rgb24 Center, int Count);

public static class KMeans
{
    public const int DefaultK = 5;

    public const int MaxIterations = 20;

    public static IReadOnlyList<ColorCluster> Cluster(
        IReadOnlyList<Rgb24> pixels, int k = DefaultK, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Count == 0)
            throw new ArgumentException("Немає пікселів для аналізу.", nameof(pixels));
        if (k < 1)
            throw new ArgumentOutOfRangeException(nameof(k), "k має бути не меншим за 1.");

        var centers = InitialCenters(pixels, k);
        var assignment = new int[pixels.Count];
        Array.Fill(assignment, -1);
        var counts = new int[centers.Length];

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Крок «призначення»: кожен піксель — до найближчого центру.
            var changed = Assign(pixels, centers, assignment);

            // Крок «оновлення»: центр — середній колір своїх пікселів.
            Recompute(pixels, centers, assignment, counts);
            
            // Якщо жоден піксель не змінив кластер, центри більше не зрушать — готово.
            if (!changed)
                break;
        }

        return centers.Select((center, i) => new ColorCluster(center, counts[i])).ToList();
    }


    private static Rgb24[] InitialCenters(IReadOnlyList<Rgb24> pixels, int k)
    {
        var centers = new List<Rgb24> { Mean(pixels) };

        // Для кожного пікселя — квадрат відстані до найближчого вже вибраного центру.
        var nearest = new int[pixels.Count];
        for (var i = 0; i < pixels.Count; i++)
            nearest[i] = Distance2(pixels[i], centers[0]);

        while (centers.Count < k)
        {
            var farthest = -1;
            var best = 0;
            for (var i = 0; i < pixels.Count; i++)
            {
                if (nearest[i] > best)
                {
                    best = nearest[i];
                    farthest = i;
                }
            }

            // Кожен піксель уже збігається з якимсь центром — нових кольорів немає.
            if (farthest < 0)
                break;

            var center = pixels[farthest];
            centers.Add(center);
            for (var i = 0; i < pixels.Count; i++)
                nearest[i] = Math.Min(nearest[i], Distance2(pixels[i], center));
        }

        return centers.ToArray();
    }

    private static bool Assign(IReadOnlyList<Rgb24> pixels, Rgb24[] centers, int[] assignment)
    {
        var changed = false;
        for (var i = 0; i < pixels.Count; i++)
        {
            var bestIndex = 0;
            var bestDistance = int.MaxValue;
            for (var c = 0; c < centers.Length; c++)
            {
                var distance = Distance2(pixels[i], centers[c]);
                if (distance < bestDistance)   // за рівної відстані — центр з меншим номером
                {
                    bestDistance = distance;
                    bestIndex = c;
                }
            }

            if (assignment[i] != bestIndex)
            {
                assignment[i] = bestIndex;
                changed = true;
            }
        }
        return changed;
    }

    private static void Recompute(IReadOnlyList<Rgb24> pixels, Rgb24[] centers, int[] assignment, int[] counts)
    {
        var sumR = new long[centers.Length];
        var sumG = new long[centers.Length];
        var sumB = new long[centers.Length];
        Array.Clear(counts);

        for (var i = 0; i < pixels.Count; i++)
        {
            var c = assignment[i];
            sumR[c] += pixels[i].R;
            sumG[c] += pixels[i].G;
            sumB[c] += pixels[i].B;
            counts[c]++;
        }

        for (var c = 0; c < centers.Length; c++)
        {
            // Порожній кластер зберігає старий центр; потім його відкине PaletteBuilder.
            if (counts[c] == 0)
                continue;

            centers[c] = new Rgb24(
                ToByte((double)sumR[c] / counts[c]),
                ToByte((double)sumG[c] / counts[c]),
                ToByte((double)sumB[c] / counts[c]));
        }
    }

    private static Rgb24 Mean(IReadOnlyList<Rgb24> pixels)
    {
        long r = 0, g = 0, b = 0;
        foreach (var p in pixels)
        {
            r += p.R;
            g += p.G;
            b += p.B;
        }
        return new Rgb24(
            ToByte((double)r / pixels.Count),
            ToByte((double)g / pixels.Count),
            ToByte((double)b / pixels.Count));
    }

    internal static int Distance2(Rgb24 a, Rgb24 b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return dr * dr + dg * dg + db * db;
    }

    private static byte ToByte(double value) => (byte)Math.Round(value);
}
