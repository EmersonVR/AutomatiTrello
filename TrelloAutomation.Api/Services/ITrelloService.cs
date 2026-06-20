using TrelloAutomation.Api.Dtos;

namespace TrelloAutomation.Api.Services;

public interface ITrelloService
{
    Task<BoardContextDto> GetBoardContextAsync(CancellationToken cancellationToken);
    Task<BoardContextFullDto> GetBoardContextFullAsync(CancellationToken cancellationToken);
    Task<SyncResultDto> PreviewPlanAsync(ProjectPlanRequest request, CancellationToken cancellationToken);
    Task<SyncResultDto> SyncPlanAsync(ProjectPlanRequest request, CancellationToken cancellationToken);
    Task<ActionResultDto> RenameListAsync(string listId, RenameListRequest request, CancellationToken cancellationToken);
    Task<ActionResultDto> UpdateCardAsync(string cardId, UpdateCardRequest request, CancellationToken cancellationToken);
    Task<ReplaceTextResultDto> ReplaceTextAsync(ReplaceTextRequest request, CancellationToken cancellationToken);
    Task<UpdatePlanResultDto> PreviewUpdatePlanAsync(UpdatePlanRequest request, CancellationToken cancellationToken);
    Task<UpdatePlanResultDto> ApplyUpdatePlanAsync(UpdatePlanRequest request, CancellationToken cancellationToken);
}
