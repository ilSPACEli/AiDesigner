using System.Net;
using AiDesigner.Services.Results;

namespace AiDesigner.Tests.Services;

// Сторінка «Галерея»: помилка клієнта → зрозумілий текст.
public class GalleryErrorMessagesTests
{
    private static HttpRequestException Http(HttpStatusCode? status) => new("помилка", null, status);

    [Fact]
    public void AC22_NotFound_ReturnsWorkNotFoundMessage()
    {
        var message = GalleryErrorMessages.FromException(Http(HttpStatusCode.NotFound));

        Assert.Equal(GalleryErrorMessages.WorkNotFoundMessage, message);
    }

    [Fact]
    public void AC24_TooManyRequests_ReturnsTooManyRequestsMessage()
    {
        var message = GalleryErrorMessages.FromException(Http(HttpStatusCode.TooManyRequests));

        Assert.Equal(GalleryErrorMessages.TooManyRequestsMessage, message);
    }

    [Fact]
    public void NoStatus_ReturnsNoConnectionMessage()
    {
        // StatusCode == null: відповіді від сервера не було.
        var message = GalleryErrorMessages.FromException(Http(null));

        Assert.Equal(GalleryErrorMessages.NoConnectionMessage, message);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public void OtherStatus_ReturnsServerErrorMessage(HttpStatusCode status)
    {
        var message = GalleryErrorMessages.FromException(Http(status));

        Assert.Equal(GalleryErrorMessages.ServerErrorMessage, message);
    }

    [Fact]
    public void NotHttpException_ReturnsUnexpectedMessage()
    {
        var message = GalleryErrorMessages.FromException(new InvalidOperationException("збій"));

        Assert.Equal(GalleryErrorMessages.UnexpectedMessage, message);
    }

    [Fact]
    public void NullException_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GalleryErrorMessages.FromException(null!));
    }
}
