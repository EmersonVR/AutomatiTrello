namespace TrelloAutomation.Api.Options;

public sealed class TrelloOptions
{
    public const string SectionName = "Trello";

    public string ApiKey { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string BoardId { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.trello.com/1";
}
