namespace TrelloAutomation.Api.Dtos;

public sealed class TrelloListDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal? Pos { get; init; }
}

public sealed class TrelloLabelDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
}

public sealed class TrelloCardDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Desc { get; init; }
    public DateTimeOffset? Due { get; init; }
    public IReadOnlyList<string> IdLabels { get; init; } = [];
}

public sealed class TrelloChecklistDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<TrelloCheckItemDto> CheckItems { get; init; } = [];
}

public sealed class TrelloCheckItemDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string State { get; init; } = "incomplete";
}
