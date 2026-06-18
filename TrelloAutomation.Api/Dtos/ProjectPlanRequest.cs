namespace TrelloAutomation.Api.Dtos;

public sealed class ProjectPlanRequest
{
    public string? BoardId { get; init; }
    public string Mode { get; init; } = "append";
    public bool DryRun { get; init; }
    public IReadOnlyList<PlanListDto> Lists { get; init; } = [];
}
