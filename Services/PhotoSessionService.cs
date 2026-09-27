using AiDesigner.Models;

namespace AiDesigner.Services;

public sealed class PhotoSessionService
{
    public UploadedPhoto? Current { get; private set; }

    public event Action? Changed;

    public void SetPhoto(UploadedPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        Current = photo;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Current = null;
        Changed?.Invoke();
    }
}
