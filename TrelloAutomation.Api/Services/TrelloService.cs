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
