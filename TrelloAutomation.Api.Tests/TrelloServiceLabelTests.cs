using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrelloAutomation.Api.Clients;
using TrelloAutomation.Api.Dtos;
using TrelloAutomation.Api.Options;
using TrelloAutomation.Api.Services;

namespace TrelloAutomation.Api.Tests;

public sealed class TrelloServiceLabelTests
{
    [Fact]
    public async Task PreviewPlan_ReusesExistingBackendLabelByName()
    {
        var trello = new FakeTrelloClient
        {
            Labels = [Label("label-backend", "Backend")]
        };
        var service = CreateService(trello);

        var result = await service.PreviewPlanAsync(Request(labels: ["Backend"]), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.CreatedLabels);
        Assert.Equal(1, result.ReusedLabels);
        Assert.Empty(result.LabelsToCreate);
        Assert.Contains(result.LabelsToReuse, label => label.Id == "label-backend");
        Assert.Equal(0, trello.CreateLabelCalls);
    }

    [Fact]
    public async Task PreviewPlan_ReusesExistingAltaLabelFromPriority()
    {
        var trello = new FakeTrelloClient
        {
            Labels = [Label("label-alta", "Alta")]
        };
        var service = CreateService(trello);

        var result = await service.PreviewPlanAsync(Request(labels: [], priority: "Alta"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.CreatedLabels);
        Assert.Equal(1, result.ReusedLabels);
        Assert.Contains(result.LabelsToReuse, label => label.Id == "label-alta");
        Assert.Equal(0, trello.CreateLabelCalls);
    }

    [Fact]
    public async Task PreviewPlan_ReusesExistingLabelById()
    {
        var trello = new FakeTrelloClient
        {
            Labels = [Label("label-backend", "Backend")]
        };
        var service = CreateService(trello);

        var result = await service.PreviewPlanAsync(Request(labels: ["label-backend"]), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.CreatedLabels);
        Assert.Equal(1, result.ReusedLabels);
        Assert.Empty(result.LabelsToCreate);
        Assert.Contains(result.LabelsToReuse, label => label.Id == "label-backend");
    }

    [Fact]
    public async Task PreviewPlan_ReusesExistingLabelWithTrimCaseAndAccentDifferences()
    {
        var trello = new FakeTrelloClient
        {
            Labels = [Label("label-docs", "Documentación")]
        };
        var service = CreateService(trello);

        var result = await service.PreviewPlanAsync(Request(labels: [" documentacion "]), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.CreatedLabels);
        Assert.Equal(1, result.ReusedLabels);
        Assert.Contains(result.LabelsToReuse, label => label.Id == "label-docs");
    }

    [Fact]
    public async Task PreviewPlan_BlocksMissingLabelWhenCreationIsNotAllowed()
    {
        var trello = new FakeTrelloClient
        {
            Labels = [Label("label-backend", "Backend")]
        };
        var service = CreateService(trello);

        var result = await service.PreviewPlanAsync(Request(labels: ["Backend", "Mobile"], allowCreateLabels: false), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, result.CreatedLabels);
        Assert.Equal(1, result.ReusedLabels);
        Assert.Empty(result.LabelsToCreate);
        Assert.Contains(result.Errors, error => error.Contains("Mobile", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, trello.CreateLabelCalls);
    }

    [Fact]
    public async Task SyncPlan_UsesExistingLabelIdWhenCreatingCard()
    {
        var trello = new FakeTrelloClient
        {
            Labels = [Label("label-backend", "Backend")]
        };
        var service = CreateService(trello);

        var result = await service.SyncPlanAsync(Request(labels: ["Backend"]), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.CreatedLabels);
        Assert.Equal(1, result.ReusedLabels);
        Assert.Equal(0, trello.CreateLabelCalls);
        Assert.Single(trello.CreatedCards);
        Assert.Equal(["label-backend"], trello.CreatedCards[0].LabelIds);
    }

    private static TrelloService CreateService(FakeTrelloClient trello) =>
        new(
            trello,
            Microsoft.Extensions.Options.Options.Create(new TrelloOptions { BoardId = "board-1" }),
            NullLogger<TrelloService>.Instance);

    private static ProjectPlanRequest Request(
        IReadOnlyList<string> labels,
        string? priority = null,
        bool allowCreateLabels = true) =>
        new()
        {
            BoardId = "board-1",
            AllowCreateLabels = allowCreateLabels,
            Lists =
            [
                new PlanListDto
                {
                    Name = "Semana 2",
                    Cards =
                    [
                        new PlanCardDto
                        {
                            Title = "Crear API",
                            Description = "Trabajo de backend",
                            Labels = labels,
                            Priority = priority,
                            Checklist = ["Implementar"]
                        }
                    ]
                }
            ]
        };

    private static TrelloLabelDto Label(string id, string name) => new()
    {
        Id = id,
        Name = name
    };

    private sealed class FakeTrelloClient : ITrelloClient
    {
        public List<TrelloListDto> Lists { get; } = [];
        public List<TrelloLabelDto> Labels { get; init; } = [];
        public List<CreatedCard> CreatedCards { get; } = [];
        public int CreateLabelCalls { get; private set; }

        public Task<IReadOnlyList<TrelloListDto>> GetBoardListsAsync(string boardId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrelloListDto>>(Lists);

        public Task<IReadOnlyList<TrelloListDto>> GetBoardListsFullAsync(string boardId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrelloListDto>>(Lists);

        public Task<IReadOnlyList<TrelloLabelDto>> GetBoardLabelsAsync(string boardId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrelloLabelDto>>(Labels);

        public Task<TrelloListDto> CreateListAsync(string boardId, string name, string position, CancellationToken cancellationToken)
        {
            var list = new TrelloListDto { Id = $"list-{Lists.Count + 1}", Name = name };
            Lists.Add(list);
            return Task.FromResult(list);
        }

        public Task<TrelloListDto> GetListAsync(string listId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<TrelloListDto> RenameListAsync(string listId, string name, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TrelloCardDto>> GetCardsInListAsync(string listId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrelloCardDto>>([]);

        public Task<TrelloCardDto> GetCardAsync(string cardId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<TrelloCardDto> CreateCardAsync(
            string listId,
            string title,
            string description,
            IReadOnlyList<string> labelIds,
            DateTimeOffset? dueDate,
            CancellationToken cancellationToken)
        {
            CreatedCards.Add(new CreatedCard(listId, title, labelIds.ToArray()));
            return Task.FromResult(new TrelloCardDto
            {
                Id = $"card-{CreatedCards.Count}",
                Name = title,
                IdList = listId,
                IdLabels = labelIds.ToArray()
            });
        }

        public Task<TrelloCardDto> UpdateCardAsync(
            string cardId,
            string? title,
            string? description,
            DateTimeOffset? dueDate,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task UpdateCardDescriptionAsync(string cardId, string description, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<TrelloLabelDto> CreateLabelAsync(string boardId, string name, string color, CancellationToken cancellationToken)
        {
            CreateLabelCalls++;
            var label = new TrelloLabelDto { Id = $"label-{Labels.Count + 1}", Name = name, Color = color };
            Labels.Add(label);
            return Task.FromResult(label);
        }

        public Task AddLabelToCardAsync(string cardId, string labelId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TrelloChecklistDto>> GetCardChecklistsAsync(string cardId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrelloChecklistDto>>([]);

        public Task<TrelloChecklistDto> CreateChecklistAsync(string cardId, string name, CancellationToken cancellationToken) =>
            Task.FromResult(new TrelloChecklistDto { Id = "checklist-1", Name = name });

        public Task CreateCheckItemAsync(string checklistId, string name, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed record CreatedCard(string ListId, string Title, IReadOnlyList<string> LabelIds);
}
