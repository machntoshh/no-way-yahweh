using System.IO;
using System.Text.Json;

namespace NoWayYahweh.App;

public sealed record Verse(string Reference, string Theme, string Text, string Reflection);

/// <summary>extension/verses.json (Bíblia Livre, CC BY 4.0), embedded so app and extension share one source.</summary>
public static class Verses
{
    private sealed record File(string Attribution, List<Verse> Verses);

    private static readonly Lazy<File> Data = new(() =>
    {
        using var stream = typeof(Verses).Assembly.GetManifestResourceStream("verses.json")
            ?? throw new InvalidOperationException("verses.json não embutido.");
        return JsonSerializer.Deserialize<File>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    });

    public static string Attribution => Data.Value.Attribution;

    public static Verse Random() => Data.Value.Verses[System.Random.Shared.Next(Data.Value.Verses.Count)];
}
