using AiDesigner.Models;

namespace AiDesigner.Services.Stubs;

/// ЗАГЛУШКА
public class StubResultStorage : IResultStorage
{
    public Task<ResultImage> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var image = new ResultImage
        {
            FileName = $"result-{now:yyyyMMdd-HHmmss}.png",
            Format = "png",
            CreatedAt = now
        };
        return Task.FromResult(image);
    }
}
