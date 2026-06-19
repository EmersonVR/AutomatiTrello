using Microsoft.Extensions.Options;
using TrelloAutomation.Api.Clients;
using TrelloAutomation.Api.Dtos;
using TrelloAutomation.Api.Mappers;
using TrelloAutomation.Api.Options;

namespace TrelloAutomation.Api.Services;

public sealed class TrelloService : ITrelloService
{
    private const string TasksChecklistName = "Tareas";

    private static readonly IReadOnlyDictionary<string, string> LabelColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Alta"] = "red",
        ["Media"] = "yellow",
        ["Baja"] = "green",
        ["Backend"] = "blue",
        ["Frontend"] = "purple",
        ["DevOps"] = "orange",
        ["Documentación"] = "sky",
        ["Pruebas"] = "lime",
        ["Diseño"] = "pink",
        ["Producto"] = "black"
    };

    private readonly ITrelloClient _trelloClient;
    private readonly TrelloOptions _options;
    private readonly ILogger<TrelloService> _logger;

    public TrelloService(ITrelloClient trelloClient, IOptions<TrelloOptions> options, ILogger<TrelloService> logger)
    {
        _trelloClient = trelloClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BoardContextDto> GetBoardContextAsync(CancellationToken cancellationToken)
    {
        var boardId = GetConfiguredBoardId();
        return new BoardContextDto
        {
            BoardId = boardId,
            Lists = await _trelloClient.GetBoardListsAsync(boardId, cancellationToken),
            Labels = await _trelloClient.GetBoardLabelsAsync(boardId, cancellationToken)
        };
    }

    public async Task<BoardContextFullDto> GetBoardContextFullAsync(CancellationToken cancellationToken)
    {
        var boardId = GetConfiguredBoardId();
        var lists = await _trelloClient.GetBoardListsFullAsync(boardId, cancellationToken);
        var labels = await _trelloClient.GetBoardLabelsAsync(boardId, cancellationToken);
        var fullLists = new List<BoardContextFullListDto>();

        foreach (var list in lists)
        {
            var cards = await _trelloClient.GetCardsInListAsync(list.Id, cancellationToken);
            var fullCards = new List<BoardContextFullCardDto>();

            foreach (var card in cards)
            {
                var checklists = await _trelloClient.GetCardChecklistsAsync(card.Id, cancellationToken);
                fullCards.Add(new BoardContextFullCardDto
                {
                    Id = card.Id,
                    Name = card.Name,
                    Desc = card.Desc,
                    IdList = card.IdList,
                    Labels = card.Labels,
                    Checklists = checklists
                });
            }

            fullLists.Add(new BoardContextFullListDto
            {
                Id = list.Id,
                Name = list.Name,
                Pos = list.Pos,
                Closed = list.Closed,
                Cards = fullCards
            });
        }

        return new BoardContextFullDto
        {
            BoardId = boardId,
            Lists = fullLists,
            Labels = labels
        };
    }

    public async Task<ActionResultDto> RenameListAsync(string listId, RenameListRequest request, CancellationToken cancellationToken)
    {
        var current = await _trelloClient.GetListAsync(listId.Trim(), cancellationToken);
        var renamed = await _trelloClient.RenameListAsync(listId.Trim(), request.Name.Trim(), cancellationToken);

        return new ActionResultDto
        {
            Success = true,
            ListId = renamed.Id,
            OldName = current.Name,
            NewName = renamed.Name
        };
    }

    public async Task<ActionResultDto> UpdateCardAsync(string cardId, UpdateCardRequest request, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var card = await _trelloClient.GetCardAsync(cardId.Trim(), cancellationToken);
        var oldName = card.Name;
        var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        var description = request.Description;

        if (title is not null || description is not null || request.DueDate.HasValue)
        {
            card = await _trelloClient.UpdateCardAsync(card.Id, title, description, request.DueDate, cancellationToken);
        }

        if (request.Labels is not null)
        {
            var boardId = GetConfiguredBoardId();
            var boardLabels = (await _trelloClient.GetBoardLabelsAsync(boardId, cancellationToken)).ToList();
            var labelIds = await ResolveLabelNamesAsync(boardId, request.Labels, boardLabels, warnings, cancellationToken);

            foreach (var labelId in labelIds.Where(labelId => !card.IdLabels.Contains(labelId, StringComparer.OrdinalIgnoreCase)))
            {
                await _trelloClient.AddLabelToCardAsync(card.Id, labelId, cancellationToken);
            }
        }

        if (title is null && description is null && !request.DueDate.HasValue && request.Labels is null)
        {
            warnings.Add("No fields were provided to update.");
        }

        return new ActionResultDto
        {
            Success = true,
            CardId = card.Id,
            OldName = oldName,
            NewName = title ?? card.Name,
            Warnings = warnings
        };
    }

    public async Task<ReplaceTextResultDto> ReplaceTextAsync(ReplaceTextRequest request, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        if (request.Target.IncludeChecklistItems)
        {
            warnings.Add("Checklist item text replacement is not supported in this phase; checklist items were not modified.");
        }

        var cards = await ResolveTargetCardsAsync(request.Target, cancellationToken);
        var changes = new List<PreviewChangeDto>();
        var cardsUpdated = 0;
        var titlesChanged = 0;
        var descriptionsChanged = 0;

        foreach (var card in cards)
        {
            var changedTitle = request.Target.IncludeCardTitles
                ? ApplyReplacements(card.Name, request.Replacements)
                : card.Name;
            var changedDescription = request.Target.IncludeCardDescriptions
                ? ApplyReplacements(card.Desc ?? string.Empty, request.Replacements)
                : card.Desc;

            var titleChanged = request.Target.IncludeCardTitles && changedTitle != card.Name;
            var descriptionChanged = request.Target.IncludeCardDescriptions && changedDescription != (card.Desc ?? string.Empty);

            if (titleChanged)
            {
                titlesChanged++;
                changes.Add(new PreviewChangeDto
                {
                    EntityType = "card",
                    EntityId = card.Id,
                    Field = "title",
                    Before = card.Name,
                    After = changedTitle
                });
            }

            if (descriptionChanged)
            {
                descriptionsChanged++;
                changes.Add(new PreviewChangeDto
                {
                    EntityType = "card",
                    EntityId = card.Id,
                    Field = "description",
                    Before = card.Desc,
                    After = changedDescription
                });
            }

            if (!request.DryRun && (titleChanged || descriptionChanged))
            {
                await _trelloClient.UpdateCardAsync(
                    card.Id,
                    titleChanged ? changedTitle : null,
                    descriptionChanged ? changedDescription : null,
                    null,
                    cancellationToken);
                cardsUpdated++;
            }
        }

        return new ReplaceTextResultDto
        {
            Success = errors.Count == 0,
            CardsMatched = cards.Count,
            CardsUpdated = cardsUpdated,
            TitlesChanged = titlesChanged,
            DescriptionsChanged = descriptionsChanged,
            Warnings = warnings,
            Errors = errors,
            PreviewChanges = changes
        };
    }

    public Task<UpdatePlanResultDto> PreviewUpdatePlanAsync(UpdatePlanRequest request, CancellationToken cancellationToken) =>
        ProcessUpdatePlanAsync(request, applyChanges: false, cancellationToken);

    public Task<UpdatePlanResultDto> ApplyUpdatePlanAsync(UpdatePlanRequest request, CancellationToken cancellationToken) =>
        ProcessUpdatePlanAsync(request, applyChanges: true, cancellationToken);

    public async Task<SyncResultDto> SyncPlanAsync(ProjectPlanRequest request, CancellationToken cancellationToken)
    {
        var boardId = string.IsNullOrWhiteSpace(request.BoardId) ? GetConfiguredBoardId() : request.BoardId.Trim();
        var warnings = new List<string>();
        var result = new SyncResultDto { Success = true, BoardId = boardId };

        var boardLists = (await _trelloClient.GetBoardListsAsync(boardId, cancellationToken)).ToList();
        var boardLabels = (await _trelloClient.GetBoardLabelsAsync(boardId, cancellationToken)).ToList();

        foreach (var planList in request.Lists)
        {
            var trelloList = boardLists.FirstOrDefault(list => SameName(list.Name, planList.Name));
            if (trelloList is null)
            {
                trelloList = await _trelloClient.CreateListAsync(boardId, planList.Name.Trim(), NormalizePosition(planList.Position), cancellationToken);
                boardLists.Add(trelloList);
                result.CreatedLists++;
            }
            else
            {
                result.ReusedLists++;
            }

            var existingCards = (await _trelloClient.GetCardsInListAsync(trelloList.Id, cancellationToken)).ToList();

            foreach (var planCard in planList.Cards)
            {
                var labelIds = await ResolveLabelsAsync(boardId, planCard, boardLabels, result, warnings, cancellationToken);
                var description = TrelloDescriptionBuilder.Build(planCard);
                var existingCard = existingCards.FirstOrDefault(card => SameName(card.Name, planCard.Title));
                TrelloCardDto trelloCard;

                if (existingCard is null)
                {
                    trelloCard = await _trelloClient.CreateCardAsync(
                        trelloList.Id,
                        planCard.Title.Trim(),
                        description,
                        labelIds,
                        planCard.DueDate,
                        cancellationToken);

                    existingCards.Add(trelloCard);
                    result.CreatedCards++;
                }
                else
                {
                    trelloCard = existingCard;
                    await _trelloClient.UpdateCardDescriptionAsync(trelloCard.Id, description, cancellationToken);
                    result.UpdatedCards++;

                    foreach (var labelId in labelIds.Where(labelId => !trelloCard.IdLabels.Contains(labelId, StringComparer.OrdinalIgnoreCase)))
                    {
                        await _trelloClient.AddLabelToCardAsync(trelloCard.Id, labelId, cancellationToken);
                    }
                }

                await SyncChecklistAsync(trelloCard.Id, planCard.Checklist, result, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Synced Trello plan for board {BoardId}. Created cards: {CreatedCards}, updated cards: {UpdatedCards}.",
            boardId,
            result.CreatedCards,
            result.UpdatedCards);

        result.Warnings = warnings;
        result.Success = true;
        return result;
    }

    private async Task<IReadOnlyList<string>> ResolveLabelsAsync(
        string boardId,
        PlanCardDto planCard,
        List<TrelloLabelDto> boardLabels,
        SyncResultDto result,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var labelNames = planCard.Labels
            .Append(planCard.Priority)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var labelIds = new List<string>();
        foreach (var labelName in labelNames)
        {
            var existingLabel = boardLabels.FirstOrDefault(label => SameName(label.Name, labelName));
            if (existingLabel is not null)
            {
                labelIds.Add(existingLabel.Id);
                result.ReusedLabels++;
                continue;
            }

            var color = LabelColors.GetValueOrDefault(labelName, "blue");
            try
            {
                var createdLabel = await _trelloClient.CreateLabelAsync(boardId, labelName, color, cancellationToken);
                boardLabels.Add(createdLabel);
                labelIds.Add(createdLabel.Id);
                result.CreatedLabels++;
            }
            catch (TrelloApiException ex)
            {
                warnings.Add($"Label '{labelName}' could not be created with color '{color}'. Trello response: {ex.Message}");
            }
        }

        return labelIds;
    }

    private async Task SyncChecklistAsync(
        string cardId,
        IReadOnlyList<string> items,
        SyncResultDto result,
        CancellationToken cancellationToken)
    {
        var cleanItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanItems.Count == 0)
        {
            return;
        }

        var checklists = await _trelloClient.GetCardChecklistsAsync(cardId, cancellationToken);
        var tasksChecklist = checklists.FirstOrDefault(checklist => SameName(checklist.Name, TasksChecklistName));
        if (tasksChecklist is null)
        {
            tasksChecklist = await _trelloClient.CreateChecklistAsync(cardId, TasksChecklistName, cancellationToken);
            result.CreatedChecklists++;
        }
        else
        {
            result.ReusedChecklists++;
        }

        foreach (var item in cleanItems.Where(item => !tasksChecklist.CheckItems.Any(checkItem => SameName(checkItem.Name, item))))
        {
            await _trelloClient.CreateCheckItemAsync(tasksChecklist.Id, item, cancellationToken);
            result.CreatedCheckItems++;
        }
    }

    private async Task<UpdatePlanResultDto> ProcessUpdatePlanAsync(
        UpdatePlanRequest request,
        bool applyChanges,
        CancellationToken cancellationToken)
    {
        var boardId = string.IsNullOrWhiteSpace(request.BoardId) ? GetConfiguredBoardId() : request.BoardId.Trim();
        var warnings = new List<string>();
        var errors = new List<string>();
        var changes = new List<PreviewChangeDto>();
        var listsMatched = 0;
        var listsUpdated = 0;
        var cardsMatched = 0;
        var cardsUpdated = 0;
        var titlesChanged = 0;
        var descriptionsChanged = 0;

        foreach (var planList in request.Lists)
        {
            if (string.IsNullOrWhiteSpace(planList.Id))
            {
                warnings.Add("A list update was skipped because it does not include an id.");
                continue;
            }

            var list = await _trelloClient.GetListAsync(planList.Id.Trim(), cancellationToken);
            listsMatched++;

            if (!string.IsNullOrWhiteSpace(planList.CurrentName) && !SameName(list.Name, planList.CurrentName))
            {
                warnings.Add($"List '{planList.Id}' currentName mismatch. Trello has '{list.Name}', request expected '{planList.CurrentName}'.");
            }

            if (!string.IsNullOrWhiteSpace(planList.NewName) && !SameName(list.Name, planList.NewName))
            {
                changes.Add(new PreviewChangeDto
                {
                    EntityType = "list",
                    EntityId = list.Id,
                    Field = "name",
                    Before = list.Name,
                    After = planList.NewName.Trim()
                });

                if (applyChanges)
                {
                    await _trelloClient.RenameListAsync(list.Id, planList.NewName.Trim(), cancellationToken);
                }

                listsUpdated++;
            }

            foreach (var planCard in planList.Cards)
            {
                if (string.IsNullOrWhiteSpace(planCard.Id))
                {
                    warnings.Add($"A card update in list '{planList.Id}' was skipped because it does not include an id.");
                    continue;
                }

                var card = await _trelloClient.GetCardAsync(planCard.Id.Trim(), cancellationToken);
                cardsMatched++;

                if (!string.IsNullOrWhiteSpace(planCard.CurrentTitle) && !SameName(card.Name, planCard.CurrentTitle))
                {
                    warnings.Add($"Card '{planCard.Id}' currentTitle mismatch. Trello has '{card.Name}', request expected '{planCard.CurrentTitle}'.");
                }

                var newTitle = string.IsNullOrWhiteSpace(planCard.NewTitle) ? card.Name : planCard.NewTitle.Trim();
                var newDescription = ApplyReplacements(card.Desc ?? string.Empty, planCard.DescriptionReplacements);
                var titleChanged = !SameName(card.Name, newTitle);
                var descriptionChanged = newDescription != (card.Desc ?? string.Empty);

                if (titleChanged)
                {
                    titlesChanged++;
                    changes.Add(new PreviewChangeDto
                    {
                        EntityType = "card",
                        EntityId = card.Id,
                        Field = "title",
                        Before = card.Name,
                        After = newTitle
                    });
                }

                if (descriptionChanged)
                {
                    descriptionsChanged++;
                    changes.Add(new PreviewChangeDto
                    {
                        EntityType = "card",
                        EntityId = card.Id,
                        Field = "description",
                        Before = card.Desc,
                        After = newDescription
                    });
                }

                if (titleChanged || descriptionChanged)
                {
                    if (applyChanges)
                    {
                        await _trelloClient.UpdateCardAsync(
                            card.Id,
                            titleChanged ? newTitle : null,
                            descriptionChanged ? newDescription : null,
                            null,
                            cancellationToken);
                    }

                    cardsUpdated++;
                }
            }
        }

        return new UpdatePlanResultDto
        {
            Success = errors.Count == 0,
            BoardId = boardId,
            ListsMatched = listsMatched,
            ListsUpdated = listsUpdated,
            CardsMatched = cardsMatched,
            CardsUpdated = cardsUpdated,
            TitlesChanged = titlesChanged,
            DescriptionsChanged = descriptionsChanged,
            Warnings = warnings,
            Errors = errors,
            PreviewChanges = changes
        };
    }

    private async Task<IReadOnlyList<TrelloCardDto>> ResolveTargetCardsAsync(
        ReplaceTextTargetDto target,
        CancellationToken cancellationToken)
    {
        var cardsById = new Dictionary<string, TrelloCardDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var listId in target.ListIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var listCards = await _trelloClient.GetCardsInListAsync(listId, cancellationToken);
            foreach (var card in listCards)
            {
                cardsById[card.Id] = card;
            }
        }

        foreach (var cardId in target.CardIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cardsById[cardId] = await _trelloClient.GetCardAsync(cardId, cancellationToken);
        }

        return cardsById.Values.ToList();
    }

    private async Task<IReadOnlyList<string>> ResolveLabelNamesAsync(
        string boardId,
        IReadOnlyList<string> labelNames,
        List<TrelloLabelDto> boardLabels,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var labelIds = new List<string>();
        foreach (var labelName in labelNames
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var existingLabel = boardLabels.FirstOrDefault(label => SameName(label.Name, labelName));
            if (existingLabel is not null)
            {
                labelIds.Add(existingLabel.Id);
                continue;
            }

            var color = LabelColors.GetValueOrDefault(labelName, "blue");
            try
            {
                var createdLabel = await _trelloClient.CreateLabelAsync(boardId, labelName, color, cancellationToken);
                boardLabels.Add(createdLabel);
                labelIds.Add(createdLabel.Id);
            }
            catch (TrelloApiException ex)
            {
                warnings.Add($"Label '{labelName}' could not be created with color '{color}'. Trello response: {ex.Message}");
            }
        }

        return labelIds;
    }

    private static string ApplyReplacements(string value, IReadOnlyList<TextReplacementDto> replacements)
    {
        var result = value;
        foreach (var replacement in replacements)
        {
            if (string.IsNullOrEmpty(replacement.From))
            {
                continue;
            }

            result = result.Replace(replacement.From, replacement.To ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }

    private string GetConfiguredBoardId()
    {
        if (string.IsNullOrWhiteSpace(_options.BoardId))
        {
            throw new InvalidOperationException("Trello:BoardId is not configured.");
        }

        return _options.BoardId.Trim();
    }

    private static bool SameName(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizePosition(string? position) =>
        string.IsNullOrWhiteSpace(position) ? "bottom" : position.Trim().ToLowerInvariant();
}
