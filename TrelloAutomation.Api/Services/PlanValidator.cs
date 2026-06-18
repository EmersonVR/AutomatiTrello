using TrelloAutomation.Api.Dtos;

namespace TrelloAutomation.Api.Services;

public sealed class PlanValidator : IPlanValidator
{
    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alta",
        "Media",
        "Baja"
    };

    public PlanValidationResult Validate(ProjectPlanRequest? request)
    {
        var result = new PlanValidationResult();

        if (request is null)
        {
            result.Errors.Add("Request body is required.");
            return result;
        }

        if (request.Lists.Count == 0)
        {
            result.Errors.Add("At least one list is required.");
        }

        var totalCards = request.Lists.Sum(list => list.Cards.Count);
        if (totalCards > 50)
        {
            result.Errors.Add("A maximum of 50 cards per request is allowed.");
        }

        for (var listIndex = 0; listIndex < request.Lists.Count; listIndex++)
        {
            var list = request.Lists[listIndex];
            if (string.IsNullOrWhiteSpace(list.Name))
            {
                result.Errors.Add($"List at index {listIndex} must have a name.");
            }

            for (var cardIndex = 0; cardIndex < list.Cards.Count; cardIndex++)
            {
                var card = list.Cards[cardIndex];
                if (string.IsNullOrWhiteSpace(card.Title))
                {
                    result.Errors.Add($"Card at list index {listIndex}, card index {cardIndex} must have a title.");
                }

                if (!string.IsNullOrWhiteSpace(card.Priority) && !AllowedPriorities.Contains(card.Priority))
                {
                    result.Errors.Add($"Card '{card.Title}' has invalid priority '{card.Priority}'. Allowed values are Alta, Media, Baja.");
                }
            }
        }

        return result;
    }
}
