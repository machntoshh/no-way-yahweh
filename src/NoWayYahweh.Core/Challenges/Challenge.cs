using System.Globalization;
using System.Text;

namespace NoWayYahweh.Core.Challenges;

public enum ChallengeKind { Arithmetic, Sequence, Logic, Memory, Ordering, MultiStep }

/// <summary>
/// One reasoning task shown before a pause. <see cref="Items"/> holds what the UI displays
/// separately (tiles to order, symbols to memorize); <see cref="ShowSeconds"/> is set when the
/// items must disappear before answering (memory).
/// </summary>
public sealed record Challenge(
    ChallengeKind Kind,
    string Title,
    string Prompt,
    string Answer,
    IReadOnlyList<string>? Items = null,
    int? ShowSeconds = null)
{
    public bool Check(string input) => Normalize(input) == Normalize(Answer);

    /// <summary>Case, accents, extra spaces, "h" in times and leading zeros do not matter.</summary>
    public static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        var tokens = sb.ToString().Replace('h', ':').Replace(',', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => string.Join(':', t.Split(':').Select(p => p.TrimStart('0') is { Length: > 0 } s ? s : "0")));
        return string.Join(' ', tokens);
    }
}
