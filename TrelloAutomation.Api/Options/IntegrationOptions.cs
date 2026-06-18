namespace TrelloAutomation.Api.Options;

public sealed class IntegrationOptions
{
    public const string SectionName = "Integration";

    public string ApiKey { get; init; } = string.Empty;
}
