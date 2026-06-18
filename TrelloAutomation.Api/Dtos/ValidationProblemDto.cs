namespace TrelloAutomation.Api.Dtos;

public sealed class ValidationProblemDto
{
    public string Title { get; init; } = "Validation failed.";
    public IReadOnlyList<string> Errors { get; init; } = [];
}
