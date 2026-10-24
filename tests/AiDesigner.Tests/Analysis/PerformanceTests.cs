using System.Diagnostics;
using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Analysis;
using Xunit.Abstractions;

namespace AiDesigner.Tests.Analysis;


public class PerformanceTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Performance")]
    public async Task AC14_TwentyRuns_AtLeast19WithinThirtySeconds()
    {
        // Від невеликого фото з телефона до 12 Мп; по 4 запуски на кожен розмір.
        (int Width, int Height)[] sizes = { (640, 480), (1280, 960), (1920, 1080), (3000, 2000), (4000, 3000) };
        var analyzer = new PhotoAnalyzer();
        var withinLimit = 0;
        var run = 0;

        foreach (var (width, height) in sizes)
        {
            var jpeg = TestImages.RoomJpeg(width, height);
            var photo = new Photo
            {
                FileName = $"room-{width}x{height}.jpg",
                Format = "jpeg",
                Width = width,
                Height = height,
                SizeBytes = jpeg.Length,
                Content = jpeg
            };

            for (var i = 0; i < 4; i++)
            {
                run++;
                var stopwatch = Stopwatch.StartNew();
                var outcome = await analyzer.AnalyzeAsync(photo);
                stopwatch.Stop();

                Assert.Equal(AnalysisStatus.Success, outcome.Status);
                if (stopwatch.Elapsed <= Limit)
                    withinLimit++;

                _output.WriteLine(
                    $"Запуск {run,2}: {width}×{height}, {jpeg.Length / 1024} КБ — {stopwatch.ElapsedMilliseconds} мс");
            }
        }

        _output.WriteLine($"За ≤ 30 с: {withinLimit} з {run}");
        Assert.Equal(20, run);
        Assert.True(withinLimit >= 19, $"За ≤ 30 с завершилось лише {withinLimit} з 20 запусків.");
    }
}
