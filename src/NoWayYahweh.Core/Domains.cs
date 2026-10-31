using System.Text.RegularExpressions;

namespace NoWayYahweh.Core;

public static partial class Domains
{
    /// <summary>Upper bound so the host's reply always stays far below Native Messaging's 1 MB limit.</summary>
    public const int MaxExtraDomains = 5000;

    [GeneratedRegex(@"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+(?:[a-z]{2,63}|xn--[a-z0-9-]{1,59})$")]
    private static partial Regex DomainPattern();

    /// <summary>Same rules as tools/domains.mjs: lowercase, no leading "www.", bare domain only.</summary>
    public static string? Normalize(string value)
    {
        var d = value.Trim().ToLowerInvariant();
        if (d.StartsWith("www.", StringComparison.Ordinal)) d = d[4..];
        return DomainPattern().IsMatch(d) ? d : null;
    }

    /// <summary>Reads a domain list, skipping comments and invalid lines. Missing file means no domains.</summary>
    public static IReadOnlyList<string> ReadList(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(Normalize)
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.Ordinal)
            .Take(MaxExtraDomains)
            .ToList();
    }
}
