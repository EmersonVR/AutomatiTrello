namespace TrelloAutomation.Api.Clients;

public sealed class TrelloApiException : Exception
{
    public TrelloApiException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
