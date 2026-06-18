namespace TrelloAutomation.Api.Dtos;

public sealed class PlanCardDto
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Priority { get; init; }
    public string? Estimate { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public DateTimeOffset? DueDate { get; init; }
    public IReadOnlyList<string> Checklist { get; init; } = [];
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = [];
    public string? CodexNotes { get; init; }
}
