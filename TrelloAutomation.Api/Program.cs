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
