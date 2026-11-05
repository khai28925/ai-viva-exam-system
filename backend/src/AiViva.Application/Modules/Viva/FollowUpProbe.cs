using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AiViva.ModuleContracts.QuestionPreparation;

namespace AiViva.Application.Modules.Viva;

/// <summary>A follow-up question and the exact words of the student's latest answer it probes.</summary>
public sealed record FollowUpCandidate(string Question, string? Target, string? Type);

/// <summary>
/// Checks that an AI follow-up behaves like an examiner probing the student's own answer: it must quote
/// words the student actually said, and it must not reveal expected key points the student has not said.
/// </summary>
public static partial class FollowUpProbe
{
    public static readonly IReadOnlySet<string> Types =
        new HashSet<string>(["clarify", "justify", "example", "challenge", "contradiction", "deepen"], StringComparer.Ordinal);

    // Generic technical words that may appear in a key point without giving the answer away.
    private static readonly HashSet<string> GenericTerms = new(StringComparer.Ordinal)
    {
        "service", "services", "database", "databases", "data", "system", "systems", "model", "models", "layer",
        "layers", "class", "classes", "module", "modules", "request", "requests", "response", "user", "users",
        "client", "server", "code", "test", "tests", "design", "pattern", "patterns", "application", "process",
        "method", "methods", "object", "objects", "function", "value", "values", "time", "with", "from", "that",
        "this", "into", "when", "only", "each", "also", "used", "uses", "using"
    };

    private const int GramSize = 4;
    private const int MaxWords = 60;

    /// <summary>Returns why the follow-up must be rewritten; empty when it is acceptable.</summary>
    public static IReadOnlyList<string> Problems(FollowUpCandidate followUp, AssessmentContext context,
        IReadOnlyList<InterviewTurn> turns)
    {
        var problems = new List<string>();
        var latest = turns.Count == 0 ? "" : turns[^1].Answer;
        if (string.IsNullOrWhiteSpace(followUp.Target))
            problems.Add("it does not name the student's words it probes");
        else
        {
            if (!Covers(latest, followUp.Target))
                problems.Add("the quoted words are not in the student's latest answer");
            if (!Covers(followUp.Question, followUp.Target))
                problems.Add("the question does not quote the student's words");
        }
        if (Tokens(followUp.Question).Length > MaxWords)
            problems.Add("the question is too long");
        var revealed = RevealedTerms(followUp.Question, context, turns);
        if (revealed.Count > 0)
            problems.Add("it reveals expected answer content the student has not said: " + string.Join(", ", revealed));
        return problems;
    }

    /// <summary>Key-point wording that appears in the question but nowhere in the main question or the student's answers.</summary>
    public static IReadOnlyList<string> RevealedTerms(string question, AssessmentContext context,
        IReadOnlyList<InterviewTurn> turns)
    {
        var said = Tokens(string.Join(' ', turns.Select(t => t.Answer).Prepend(turns.Count == 0 ? "" : turns[0].Question)));
        var saidTokens = said.ToHashSet(StringComparer.Ordinal);
        var saidGrams = Grams(said).ToHashSet(StringComparer.Ordinal);
        var asked = Tokens(question);
        var askedTokens = asked.ToHashSet(StringComparer.Ordinal);
        var askedGrams = Grams(asked).ToHashSet(StringComparer.Ordinal);
        var terms = new List<string>();
        var copied = new List<string>();
        foreach (var keyPoint in context.ExpectedKeyPoints)
        {
            var tokens = Tokens(keyPoint);
            // A technical term (Latin script, e.g. "Saga", "eventual") the student never used.
            terms.AddRange(tokens.Where(t => t.Length >= 4 && t.All(char.IsAsciiLetterOrDigit) && !GenericTerms.Contains(t)
                && !saidTokens.Contains(t) && askedTokens.Contains(t)));
            // Key-point wording copied word for word (four consecutive words) that the student never said.
            copied.AddRange(Grams(tokens).Where(g => !saidGrams.Contains(g) && askedGrams.Contains(g)));
        }
        copied = copied.Distinct(StringComparer.Ordinal).ToList();
        // One shared four-word phrase can be a coincidence ("xử lý nghiệp vụ"); copying a sentence yields several.
        return terms.Concat(copied.Count >= 2 ? copied : []).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>True when the words of <paramref name="quote"/> appear in <paramref name="text"/> (tolerant to punctuation and small STT differences).</summary>
    public static bool Covers(string text, string quote)
    {
        var target = Tokens(quote);
        if (target.Length == 0) return false;
        var normalizedText = " " + string.Join(' ', Tokens(text)) + " ";
        if (normalizedText.Contains(" " + string.Join(' ', target) + " ", StringComparison.Ordinal)) return true;
        var available = Tokens(text).ToHashSet(StringComparer.Ordinal);
        return target.Length >= 3 && target.Count(available.Contains) >= Math.Ceiling(target.Length * 0.8);
    }

    public static string[] Tokens(string text)
    {
        var lowered = (text ?? "").ToLower(CultureInfo.InvariantCulture).Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(lowered.Length);
        foreach (var ch in lowered) builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        return Whitespace().Split(builder.ToString().Trim()).Where(t => t.Length > 0).ToArray();
    }

    private static IEnumerable<string> Grams(IReadOnlyList<string> tokens)
    {
        for (var i = 0; i + GramSize <= tokens.Count; i++)
            yield return string.Join(' ', tokens.Skip(i).Take(GramSize));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
