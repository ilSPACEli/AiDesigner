using AiDesigner.Models;

namespace AiDesigner.Tests;

/// <summary>
/// Тести перевірки фото за NFR-06 на рівні моделі (Photo.Validate, RejectionReason).
/// </summary>
public class PhotoValidationTests
{
    private static Photo PhotoWith(string format = "jpeg", int width = 800, int height = 600,
        int sizeBytes = 1000) => new()
    {
        FileName = "room.jpg",
        Format = format,
        Width = width,
        Height = height,
        SizeBytes = sizeBytes
    };

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    public void AC01_JpegOrPng_WithinLimits_IsValid(string format)
    {
        var photo = PhotoWith(format: format);

        Assert.True(photo.Validate());
        Assert.Null(photo.RejectionReason);
    }

    [Fact]
    public void AC01_ExactLimits_AreValid()
    {
        // Межові значення: рівно 10 МБ і рівно 480 px — ще дозволено.
        var photo = PhotoWith(width: 480, height: 480, sizeBytes: Photo.MaxSizeBytes);

        Assert.True(photo.Validate());
    }

    [Theory]
    [InlineData("webp")]
    [InlineData("gif")]
    [InlineData("unknown")]
    [InlineData("")]
    public void AC02_OtherFormat_IsRejected_WithFormatReason(string format)
    {
        var photo = PhotoWith(format: format);

        Assert.False(photo.Validate());
        Assert.Equal(Photo.WrongFormatMessage, photo.RejectionReason);
    }

    [Fact]
    public void AC02_FileOver10Mb_IsRejected_WithSizeReason()
    {
        var photo = PhotoWith(sizeBytes: Photo.MaxSizeBytes + 1);

        Assert.False(photo.Validate());
        Assert.Equal(Photo.TooLargeMessage, photo.RejectionReason);
    }

    [Theory]
    [InlineData(479, 800)]
    [InlineData(800, 479)]
    [InlineData(100, 100)]
    public void AC02_SmallerSideUnder480_IsRejected_WithSmallReason(int width, int height)
    {
        var photo = PhotoWith(width: width, height: height);

        Assert.False(photo.Validate());
        Assert.Equal(Photo.TooSmallMessage, photo.RejectionReason);
    }
}
