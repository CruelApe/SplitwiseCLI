using System.Net;

namespace SplitwiseCLI.Api;

public sealed class SplitwiseApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public SplitwiseApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
