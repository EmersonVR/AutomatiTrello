using TrelloAutomation.Api.Dtos;

namespace TrelloAutomation.Api.Services;

public interface IPlanValidator
{
    PlanValidationResult Validate(ProjectPlanRequest? request);
}

public sealed class PlanValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];

    public ValidationProblemDto ToProblem() => new()
    {
        Errors = Errors
    };
}
