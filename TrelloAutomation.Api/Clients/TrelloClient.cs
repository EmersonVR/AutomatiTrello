using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using TrelloAutomation.Api.Dtos;
using TrelloAutomation.Api.Options;

namespace TrelloAutomation.Api.Clients;

public sealed class TrelloClient : ITrelloClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly TrelloOptions _options;

    public TrelloClient(HttpClient httpClient, IOptions<TrelloOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<IReadOnlyList<TrelloListDto>> GetBoardListsAsync(string boardId, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<TrelloListDto>>(HttpMethod.Get, $"boards/{Uri.EscapeDataString(boardId)}/lists", new Dictionary<string, string?>
        {
            ["filter"] = "open",
            ["fields"] = "name,pos"
        }, cancellationToken);

    public Task<IReadOnlyList<TrelloLabelDto>> GetBoardLabelsAsync(string boardId, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<TrelloLabelDto>>(HttpMethod.Get, $"boards/{Uri.EscapeDataString(boardId)}/labels", new Dictionary<string, string?>
        {
            ["fields"] = "name,color",
            ["limit"] = "1000"
        }, cancellationToken);

    public Task<TrelloListDto> CreateListAsync(string boardId, string name, string position, CancellationToken cancellationToken) =>
        SendAsync<TrelloListDto>(HttpMethod.Post, "lists", new Dictionary<string, string?>
        {
            ["idBoard"] = boardId,
            ["name"] = name,
            ["pos"] = position
        }, cancellationToken);

    public Task<IReadOnlyList<TrelloCardDto>> GetCardsInListAsync(string listId, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<TrelloCardDto>>(HttpMethod.Get, $"lists/{Uri.EscapeDataString(listId)}/cards", new Dictionary<string, string?>
        {
            ["fields"] = "name,desc,idLabels,due"
        }, cancellationToken);

    public Task<TrelloCardDto> CreateCardAsync(
        string listId,
        string title,
        string description,
        IReadOnlyList<string> labelIds,
        DateTimeOffset? dueDate,
        CancellationToken cancellationToken) =>
        SendAsync<TrelloCardDto>(HttpMethod.Post, "cards", new Dictionary<string, string?>
        {
            ["idList"] = listId,
            ["name"] = title,
            ["desc"] = description,
            ["idLabels"] = labelIds.Count == 0 ? null : string.Join(",", labelIds),
            ["due"] = dueDate?.UtcDateTime.ToString("O")
        }, cancellationToken);

    public Task UpdateCardDescriptionAsync(string cardId, string description, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Put, $"cards/{Uri.EscapeDataString(cardId)}", new Dictionary<string, string?>
        {
            ["desc"] = description
        }, cancellationToken);

    public Task<TrelloLabelDto> CreateLabelAsync(string boardId, string name, string color, CancellationToken cancellationToken) =>
        SendAsync<TrelloLabelDto>(HttpMethod.Post, "labels", new Dictionary<string, string?>
        {
            ["idBoard"] = boardId,
            ["name"] = name,
            ["color"] = color
        }, cancellationToken);

    public Task AddLabelToCardAsync(string cardId, string labelId, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Post, $"cards/{Uri.EscapeDataString(cardId)}/idLabels", new Dictionary<string, string?>
        {
            ["value"] = labelId
        }, cancellationToken);

    public Task<IReadOnlyList<TrelloChecklistDto>> GetCardChecklistsAsync(string cardId, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<TrelloChecklistDto>>(HttpMethod.Get, $"cards/{Uri.EscapeDataString(cardId)}/checklists", new Dictionary<string, string?>
        {
            ["fields"] = "name",
            ["checkItems"] = "all"
        }, cancellationToken);

    public Task<TrelloChecklistDto> CreateChecklistAsync(string cardId, string name, CancellationToken cancellationToken) =>
        SendAsync<TrelloChecklistDto>(HttpMethod.Post, "checklists", new Dictionary<string, string?>
        {
            ["idCard"] = cardId,
            ["name"] = name
        }, cancellationToken);

    public Task CreateCheckItemAsync(string checklistId, string name, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Post, $"checklists/{Uri.EscapeDataString(checklistId)}/checkItems", new Dictionary<string, string?>
        {
            ["name"] = name
        }, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, Dictionary<string, string?> query, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(path, query));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value ?? throw new TrelloApiException("Trello returned an empty response.", (int)response.StatusCode);
    }

    private async Task SendWithoutBodyAsync(HttpMethod method, string path, Dictionary<string, string?> query, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(path, query));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private string BuildUri(string path, Dictionary<string, string?> query)
    {
        EnsureConfigured();

        query["key"] = _options.ApiKey;
        query["token"] = _options.Token;

        return QueryHelpers.AddQueryString(path, query);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException("Trello:ApiKey and Trello:Token must be configured.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(body)
            ? response.ReasonPhrase ?? "Trello request failed."
            : body;

        throw new TrelloApiException($"Trello request failed with status {(int)response.StatusCode}: {message}", (int)response.StatusCode);
    }
}
