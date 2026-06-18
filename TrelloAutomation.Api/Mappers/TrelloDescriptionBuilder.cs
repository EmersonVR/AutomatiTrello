using System.Text;
using TrelloAutomation.Api.Dtos;

namespace TrelloAutomation.Api.Mappers;

public static class TrelloDescriptionBuilder
{
    public static string Build(PlanCardDto card)
    {
        var builder = new StringBuilder();

        builder.AppendLine("## Descripción");
        builder.AppendLine();
        builder.AppendLine(ValueOrDash(card.Description));
        builder.AppendLine();
        builder.AppendLine("## Prioridad");
        builder.AppendLine();
        builder.AppendLine(ValueOrDash(card.Priority));
        builder.AppendLine();
        builder.AppendLine("## Estimación");
        builder.AppendLine();
        builder.AppendLine(ValueOrDash(card.Estimate));
        builder.AppendLine();
        builder.AppendLine("## Criterios de aceptación");
        builder.AppendLine();
        AppendList(builder, card.AcceptanceCriteria);
        builder.AppendLine();
        builder.AppendLine("## Notas para Codex");
        builder.AppendLine();
        builder.AppendLine(ValueOrDash(card.CodexNotes));
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine("Generado por ChatGPT Trello Automation");

        return builder.ToString();
    }

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static void AppendList(StringBuilder builder, IReadOnlyList<string> values)
    {
        var cleanValues = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList();
        if (cleanValues.Count == 0)
        {
            builder.AppendLine("-");
            return;
        }

        foreach (var value in cleanValues)
        {
            builder.AppendLine($"- {value}");
        }
    }
}
