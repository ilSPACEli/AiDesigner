using AiDesigner.Models;

namespace AiDesigner.Services;

public interface IResultStorage
{

    Task<ResultImage> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default);
}
