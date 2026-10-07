using System.Net;

namespace GDriveLight.Desktop.Services.Api;

public sealed class DriveApiException : Exception
{
    public DriveApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
