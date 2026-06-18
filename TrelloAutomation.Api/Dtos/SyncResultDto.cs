namespace TrelloAutomation.Api.Dtos;

public sealed class SyncResultDto
{
    public bool Success { get; set; }
    public string BoardId { get; init; } = string.Empty;
    public int CreatedLists { get; set; }
    public int ReusedLists { get; set; }
    public int CreatedCards { get; set; }
    public int UpdatedCards { get; set; }
    public int CreatedLabels { get; set; }
    public int ReusedLabels { get; set; }
    public int CreatedChecklists { get; set; }
    public int ReusedChecklists { get; set; }
    public int CreatedCheckItems { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = [];
    public IReadOnlyList<string> Errors { get; set; } = [];
}
