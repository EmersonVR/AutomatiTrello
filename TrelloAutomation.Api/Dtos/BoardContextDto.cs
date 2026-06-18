namespace TrelloAutomation.Api.Dtos;

public sealed class BoardContextDto
{
    public string BoardId { get; init; } = string.Empty;
    public IReadOnlyList<TrelloListDto> Lists { get; init; } = [];
    public IReadOnlyList<TrelloLabelDto> Labels { get; init; } = [];
}
