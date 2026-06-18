namespace TrelloAutomation.Api.Dtos;

public sealed class PlanListDto
{
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = "bottom";
    public IReadOnlyList<PlanCardDto> Cards { get; init; } = [];
}
