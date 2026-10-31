namespace NoWayYahweh.Core.Challenges;

/// <summary>
/// What a pause costs and what it does. A pause only switches off the secondary protections
/// (hostname-keyword rule and the user's extra domains); the list of known adult sites stays
/// blocked at all times. Pauses always end on their own.
/// </summary>
public static class PausePolicy
{
    public sealed record Option(TimeSpan Duration, int Challenges, string Label);

    /// <summary>Longer pauses ask for more work.</summary>
    public static readonly IReadOnlyList<Option> Options =
    [
        new(TimeSpan.FromMinutes(15), 1, "15 minutos — 1 desafio"),
        new(TimeSpan.FromHours(1), 2, "1 hora — 2 desafios"),
        new(TimeSpan.FromHours(4), 3, "4 horas — 3 desafios"),
    ];

    /// <summary>Seconds the reflection stays on screen before "continue anyway" can be pressed.</summary>
    public const int ReflectionSeconds = 20;

    /// <summary>Challenge kinds remembered so the next ones differ.</summary>
    public const int RecentKindsToAvoid = 3;

    public static bool IsPaused(ProtectionState state, DateTimeOffset now) => state.PausedUntil > now;

    public static void Pause(ProtectionState state, Option option, DateTimeOffset now) =>
        state.PausedUntil = now + option.Duration;

    public static void Resume(ProtectionState state) => state.PausedUntil = null;

    public static void Remember(ProtectionState state, ChallengeKind kind)
    {
        state.RecentChallengeKinds.Add(kind);
        if (state.RecentChallengeKinds.Count > RecentKindsToAvoid)
            state.RecentChallengeKinds.RemoveRange(0, state.RecentChallengeKinds.Count - RecentKindsToAvoid);
    }
}
