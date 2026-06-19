namespace TrelloAutomation.Api.Dtos;

public sealed class BoardContextFullDto
{
    public string BoardId { get; init; } = string.Empty;
    public IReadOnlyList<BoardContextFullListDto> Lists { get; init; } = [];
    public IReadOnlyList<TrelloLabelDto> Labels { get; init; } = [];
}

public sealed class BoardContextFullListDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal? Pos { get; init; }
    public bool Closed { get; init; }
    public IReadOnlyList<BoardContextFullCardDto> Cards { get; init; } = [];
}

public sealed class BoardContextFullCardDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Title => Name;
    public string? Desc { get; init; }
    public string IdList { get; init; } = string.Empty;
    public IReadOnlyList<TrelloLabelDto> Labels { get; init; } = [];
    public IReadOnlyList<TrelloChecklistDto> Checklists { get; init; } = [];
}

public sealed class RenameListRequest
{
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateCardRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public IReadOnlyList<string>? Labels { get; init; }
}

public sealed class ActionResultDto
{
    public bool Success { get; init; }
    public string? ListId { get; init; }
    public string? CardId { get; init; }
    public string? OldName { get; init; }
    public string? NewName { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class TextReplacementDto
{
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
}

public sealed class ReplaceTextTargetDto
{
    public IReadOnlyList<string> ListIds { get; init; } = [];
    public IReadOnlyList<string> CardIds { get; init; } = [];
    public bool IncludeCardTitles { get; init; } = true;
    public bool IncludeCardDescriptions { get; init; } = true;
    public bool IncludeChecklistItems { get; init; }
}

public sealed class ReplaceTextRequest
{
    public string? BoardId { get; init; }
    public IReadOnlyList<TextReplacementDto> Replacements { get; init; } = [];
    public ReplaceTextTargetDto Target { get; init; } = new();
    public bool DryRun { get; init; } = true;
}

public sealed class PreviewChangeDto
{
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Field { get; init; } = string.Empty;
    public string? Before { get; init; }
    public string? After { get; init; }
}

public sealed class ReplaceTextResultDto
{
    public bool Success { get; init; }
    public int CardsMatched { get; init; }
    public int CardsUpdated { get; init; }
    public int TitlesChanged { get; init; }
    public int DescriptionsChanged { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<PreviewChangeDto> PreviewChanges { get; init; } = [];
}

public sealed class UpdatePlanRequest
{
    public string? BoardId { get; init; }
    public IReadOnlyList<UpdatePlanListDto> Lists { get; init; } = [];
}

public sealed class UpdatePlanListDto
{
    public string Id { get; init; } = string.Empty;
    public string? CurrentName { get; init; }
    public string? NewName { get; init; }
    public IReadOnlyList<UpdatePlanCardDto> Cards { get; init; } = [];
}

public sealed class UpdatePlanCardDto
{
    public string Id { get; init; } = string.Empty;
    public string? CurrentTitle { get; init; }
    public string? NewTitle { get; init; }
    public IReadOnlyList<TextReplacementDto> DescriptionReplacements { get; init; } = [];
}

public sealed class UpdatePlanResultDto
{
    public bool Success { get; init; }
    public string BoardId { get; init; } = string.Empty;
    public int ListsMatched { get; init; }
    public int ListsUpdated { get; init; }
    public int CardsMatched { get; init; }
    public int CardsUpdated { get; init; }
    public int TitlesChanged { get; init; }
    public int DescriptionsChanged { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<PreviewChangeDto> PreviewChanges { get; init; } = [];
}
