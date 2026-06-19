using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using TrelloAutomation.Api.Clients;
using TrelloAutomation.Api.Dtos;
using TrelloAutomation.Api.Middleware;
using TrelloAutomation.Api.Options;
using TrelloAutomation.Api.Services;
using TrelloAutomation.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!builder.Environment.IsDevelopment() || !string.IsNullOrWhiteSpace(port))
{
    if (string.IsNullOrWhiteSpace(port))
    {
        port = "8080";
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<IntegrationApiKeyOperationFilter>();
});
builder.Services.Configure<TrelloOptions>(builder.Configuration.GetSection(TrelloOptions.SectionName));
builder.Services.Configure<IntegrationOptions>(builder.Configuration.GetSection(IntegrationOptions.SectionName));
builder.Services.AddTransient<ApiKeyMiddleware>();
builder.Services.AddSingleton<IPlanValidator, PlanValidator>();
builder.Services.AddScoped<ITrelloService, TrelloService>();
builder.Services.AddHttpClient<ITrelloClient, TrelloClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TrelloOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "TrelloAutomation.Api"
}))
.WithName("Health")
.WithOpenApi();

var trello = app.MapGroup("/api/trello").WithTags("Trello");

trello.MapPost("/preview-plan", Results<Ok<SyncResultDto>, BadRequest<ValidationProblemDto>> (
    ProjectPlanRequest request,
    IPlanValidator validator,
    IOptions<TrelloOptions> options) =>
{
    var validation = validator.Validate(request);
    if (!validation.IsValid)
    {
        return TypedResults.BadRequest(validation.ToProblem());
    }

    var totalCards = request.Lists.Sum(list => list.Cards.Count);
    var totalCheckItems = request.Lists.SelectMany(list => list.Cards).Sum(card => card.Checklist.Count);
    var totalLabels = request.Lists
        .SelectMany(list => list.Cards)
        .SelectMany(card => card.Labels.Append(card.Priority))
        .Where(label => !string.IsNullOrWhiteSpace(label))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    return TypedResults.Ok(new SyncResultDto
    {
        Success = true,
        BoardId = ResolveBoardId(request.BoardId, options.Value.BoardId),
        CreatedLists = request.Lists.Count,
        CreatedCards = totalCards,
        CreatedLabels = totalLabels,
        CreatedChecklists = totalCards,
        CreatedCheckItems = totalCheckItems,
        Warnings = ["Preview only; no Trello calls were made."],
        Errors = []
    });
})
.WithName("PreviewPlan")
.WithOpenApi();

trello.MapGet("/board-context", async Task<Results<Ok<BoardContextDto>, ProblemHttpResult>> (
    ITrelloService trelloService,
    CancellationToken cancellationToken) =>
{
    try
    {
        return TypedResults.Ok(await trelloService.GetBoardContextAsync(cancellationToken));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("GetBoardContext")
.WithOpenApi();

trello.MapGet("/board-context-full", async Task<Results<Ok<BoardContextFullDto>, ProblemHttpResult>> (
    ITrelloService trelloService,
    CancellationToken cancellationToken) =>
{
    try
    {
        return TypedResults.Ok(await trelloService.GetBoardContextFullAsync(cancellationToken));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("getBoardContextFull")
.WithOpenApi();

trello.MapPatch("/lists/{listId}/rename", async Task<Results<Ok<ActionResultDto>, BadRequest<ValidationProblemDto>, ProblemHttpResult>> (
    string listId,
    RenameListRequest request,
    ITrelloService trelloService,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateRenameList(listId, request);
    if (errors.Count > 0)
    {
        return TypedResults.BadRequest(ToProblem(errors));
    }

    try
    {
        return TypedResults.Ok(await trelloService.RenameListAsync(listId, request, cancellationToken));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("renameList")
.WithOpenApi();

trello.MapPatch("/cards/{cardId}", async Task<Results<Ok<ActionResultDto>, BadRequest<ValidationProblemDto>, ProblemHttpResult>> (
    string cardId,
    UpdateCardRequest request,
    ITrelloService trelloService,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateUpdateCard(cardId, request);
    if (errors.Count > 0)
    {
        return TypedResults.BadRequest(ToProblem(errors));
    }

    try
    {
        return TypedResults.Ok(await trelloService.UpdateCardAsync(cardId, request, cancellationToken));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("updateCard")
.WithOpenApi();

trello.MapPost("/replace-text", async Task<Results<Ok<ReplaceTextResultDto>, BadRequest<ValidationProblemDto>, ProblemHttpResult>> (
    ReplaceTextRequest request,
    ITrelloService trelloService,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateReplaceText(request);
    if (errors.Count > 0)
    {
        return TypedResults.BadRequest(ToProblem(errors));
    }

    try
    {
        return TypedResults.Ok(await trelloService.ReplaceTextAsync(request, cancellationToken));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("replaceText")
.WithOpenApi();

trello.MapPost("/update-plan-preview", async Task<Results<Ok<UpdatePlanResultDto>, BadRequest<ValidationProblemDto>, ProblemHttpResult>> (
    UpdatePlanRequest request,
    ITrelloService trelloService,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateUpdatePlan(request);
    if (errors.Count > 0)
    {
        return TypedResults.BadRequest(ToProblem(errors));
    }

    try
    {
        return TypedResults.Ok(await trelloService.PreviewUpdatePlanAsync(request, cancellationToken));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("updatePlanPreview")
.WithOpenApi();

trello.MapPost("/update-plan", async Task<Results<Ok<UpdatePlanResultDto>, BadRequest<ValidationProblemDto>, ProblemHttpResult>> (
    UpdatePlanRequest request,
    ITrelloService trelloService,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateUpdatePlan(request);
    if (errors.Count > 0)
    {
        return TypedResults.BadRequest(ToProblem(errors));
    }

    try
    {
        return TypedResults.Ok(await trelloService.ApplyUpdatePlanAsync(request, cancellationToken));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("updatePlan")
.WithOpenApi();

trello.MapPost("/sync-plan", async Task<Results<Ok<SyncResultDto>, BadRequest<ValidationProblemDto>, ProblemHttpResult>> (
    ProjectPlanRequest request,
    IPlanValidator validator,
    ITrelloService trelloService,
    CancellationToken cancellationToken) =>
{
    var validation = validator.Validate(request);
    if (!validation.IsValid)
    {
        return TypedResults.BadRequest(validation.ToProblem());
    }

    try
    {
        return TypedResults.Ok(await trelloService.SyncPlanAsync(request, cancellationToken));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("SyncPlan")
.WithOpenApi();

app.Run();

static string ResolveBoardId(string? requestBoardId, string configuredBoardId)
{
    return string.IsNullOrWhiteSpace(requestBoardId) ? configuredBoardId : requestBoardId.Trim();
}

static ValidationProblemDto ToProblem(IReadOnlyList<string> errors) => new()
{
    Errors = errors
};

static List<string> ValidateRenameList(string listId, RenameListRequest? request)
{
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(listId))
    {
        errors.Add("listId is required.");
    }

    if (request is null || string.IsNullOrWhiteSpace(request.Name))
    {
        errors.Add("name is required.");
    }

    return errors;
}

static List<string> ValidateUpdateCard(string cardId, UpdateCardRequest? request)
{
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(cardId))
    {
        errors.Add("cardId is required.");
    }

    if (request is null)
    {
        errors.Add("Request body is required.");
    }

    return errors;
}

static List<string> ValidateReplaceText(ReplaceTextRequest? request)
{
    var errors = new List<string>();
    if (request is null)
    {
        errors.Add("Request body is required.");
        return errors;
    }

    if (request.Replacements.Count == 0)
    {
        errors.Add("At least one replacement is required.");
    }

    for (var index = 0; index < request.Replacements.Count; index++)
    {
        if (string.IsNullOrEmpty(request.Replacements[index].From))
        {
            errors.Add($"Replacement at index {index} must include from.");
        }
    }

    if (request.Target.ListIds.Count == 0 && request.Target.CardIds.Count == 0)
    {
        errors.Add("At least one listId or cardId is required to avoid accidental board-wide updates.");
    }

    if (!request.Target.IncludeCardTitles && !request.Target.IncludeCardDescriptions && !request.Target.IncludeChecklistItems)
    {
        errors.Add("At least one target field must be enabled.");
    }

    return errors;
}

static List<string> ValidateUpdatePlan(UpdatePlanRequest? request)
{
    var errors = new List<string>();
    if (request is null)
    {
        errors.Add("Request body is required.");
        return errors;
    }

    if (request.Lists.Count == 0)
    {
        errors.Add("At least one list is required.");
    }

    for (var listIndex = 0; listIndex < request.Lists.Count; listIndex++)
    {
        var list = request.Lists[listIndex];
        if (string.IsNullOrWhiteSpace(list.Id))
        {
            errors.Add($"List at index {listIndex} must include id.");
        }

        for (var cardIndex = 0; cardIndex < list.Cards.Count; cardIndex++)
        {
            if (string.IsNullOrWhiteSpace(list.Cards[cardIndex].Id))
            {
                errors.Add($"Card at list index {listIndex}, card index {cardIndex} must include id.");
            }
        }
    }

    return errors;
}
