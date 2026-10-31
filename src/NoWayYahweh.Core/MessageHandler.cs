using System.Text.Json;
using System.Text.RegularExpressions;

namespace NoWayYahweh.Core;

/// <summary>
/// Validates extension messages and produces replies. The protocol is deliberately tiny and
/// strict: unknown types, unknown fields or out-of-range values are rejected. No message
/// carries a URL, domain or page title — the host never learns what was browsed.
///
///   {"type":"hello","browser":"chrome"|"edge"|"brave","version":"0.1.0"}
///       → {"type":"state","extraDomains":[...],"pausedUntil":unix-ms|null}
///   {"type":"blocked","count":1..10000}                                  → {"type":"ack"}
///   {"type":"ping"}  (integrity check; changes nothing)                  → {"type":"pong"}
/// </summary>
public sealed partial class MessageHandler(StateStore state, Func<IReadOnlyList<string>> extraDomains)
{
    public const int MaxBlockedCount = 10_000;
    private static readonly string[] Browsers = ["chrome", "edge", "brave"];

    [GeneratedRegex(@"^[0-9]{1,5}(\.[0-9]{1,5}){0,3}$")]
    private static partial Regex VersionPattern();

    public static MessageHandler Default => new(StateStore.Default, () => Domains.ReadList(AppPaths.ExtraDomainsFile));

    public object Handle(ReadOnlySpan<byte> body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body.ToArray());
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Error("Mensagem deve ser um objeto JSON.");

            return GetString(root, "type") switch
            {
                "hello" => Hello(root),
                "blocked" => Blocked(root),
                "ping" => HasOnly(root, "type") ? new { type = "pong" } : Error("Campos inesperados."),
                _ => Error("Tipo de mensagem desconhecido."),
            };
        }
        catch (JsonException)
        {
            return Error("JSON inválido.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException)
        {
            // stderr ends up in the browser's log, never in the reply.
            Console.Error.WriteLine(e.Message);
            return Error("Estado local indisponível.");
        }
    }

    private object Hello(JsonElement root)
    {
        if (!HasOnly(root, "type", "browser", "version")) return Error("Campos inesperados.");
        var browser = GetString(root, "browser");
        if (browser is null || !Browsers.Contains(browser)) return Error("Navegador inválido.");
        var version = GetString(root, "version");
        if (version is null || !VersionPattern().IsMatch(version)) return Error("Versão inválida.");

        var now = DateTimeOffset.UtcNow;
        var current = state.Update(s => s.BrowsersLastSeen[browser] = now);
        var pausedUntil = Challenges.PausePolicy.IsPaused(current, now) ? current.PausedUntil!.Value.ToUnixTimeMilliseconds() : (long?)null;
        return new { type = "state", extraDomains = extraDomains(), pausedUntil };
    }

    private object Blocked(JsonElement root)
    {
        if (!HasOnly(root, "type", "count")) return Error("Campos inesperados.");
        if (!root.TryGetProperty("count", out var c) || c.ValueKind != JsonValueKind.Number
            || !c.TryGetInt32(out var count) || count < 1 || count > MaxBlockedCount)
            return Error("Contagem inválida.");

        state.Update(s => s.BlockCount += count);
        return new { type = "ack" };
    }

    public static object Error(string message) => new { type = "error", error = message };

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool HasOnly(JsonElement root, params string[] allowed) =>
        root.EnumerateObject().All(p => allowed.Contains(p.Name));
}
