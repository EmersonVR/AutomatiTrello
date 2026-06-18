using TrelloAutomation.Api.Dtos;

namespace TrelloAutomation.Api.Services;

public interface ITrelloService
{
    Task<BoardContextDto> GetBoardContextAsync(CancellationToken cancellationToken);
    Task<SyncResultDto> SyncPlanAsync(ProjectPlanRequest request, CancellationToken cancellationToken);
}
