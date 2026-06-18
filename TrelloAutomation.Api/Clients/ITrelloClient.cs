using TrelloAutomation.Api.Dtos;

namespace TrelloAutomation.Api.Clients;

public interface ITrelloClient
{
    Task<IReadOnlyList<TrelloListDto>> GetBoardListsAsync(string boardId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrelloLabelDto>> GetBoardLabelsAsync(string boardId, CancellationToken cancellationToken);
    Task<TrelloListDto> CreateListAsync(string boardId, string name, string position, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrelloCardDto>> GetCardsInListAsync(string listId, CancellationToken cancellationToken);
    Task<TrelloCardDto> CreateCardAsync(string listId, string title, string description, IReadOnlyList<string> labelIds, DateTimeOffset? dueDate, CancellationToken cancellationToken);
    Task UpdateCardDescriptionAsync(string cardId, string description, CancellationToken cancellationToken);
    Task<TrelloLabelDto> CreateLabelAsync(string boardId, string name, string color, CancellationToken cancellationToken);
    Task AddLabelToCardAsync(string cardId, string labelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrelloChecklistDto>> GetCardChecklistsAsync(string cardId, CancellationToken cancellationToken);
    Task<TrelloChecklistDto> CreateChecklistAsync(string cardId, string name, CancellationToken cancellationToken);
    Task CreateCheckItemAsync(string checklistId, string name, CancellationToken cancellationToken);
}
