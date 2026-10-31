using System.Text.Json;

namespace NoWayYahweh.Core;

public sealed class ProtectionState
{
    public DateTimeOffset ProtectedSince { get; set; } = DateTimeOffset.UtcNow;
    public long BlockCount { get; set; }

    /// <summary>Set after the pause challenge; secondary protections resume on their own at this time.</summary>
    public DateTimeOffset? PausedUntil { get; set; }

    /// <summary>Sign-in verification is turned on once by default; afterwards only the user changes it.</summary>
    public bool StartupDefaultApplied { get; set; }

    /// <summary>Optional responsible-person password (PasswordGuard hash), or null.</summary>
    public string? GuardPasswordHash { get; set; }

    /// <summary>Last challenge kinds shown, so the next challenges differ.</summary>
    public List<Challenges.ChallengeKind> RecentChallengeKinds { get; set; } = [];

    /// <summary>Last time each browser's extension contacted the host ("chrome", "edge").</summary>
    public Dictionary<string, DateTimeOffset> BrowsersLastSeen { get; set; } = [];
}

/// <summary>
/// state.json is shared by every host process (one per browser connection) and the app,
/// so every read-modify-write holds a session-wide named mutex.
/// </summary>
public sealed class StateStore(string path)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static StateStore Default => new(AppPaths.StateFile);

    public ProtectionState Read() => WithLock(Load);

    public ProtectionState Update(Action<ProtectionState> change) => WithLock(() =>
    {
        var state = Load();
        change(state);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, Json));
        File.Move(temp, path, overwrite: true);
        return state;
    });

    private ProtectionState Load()
    {
        if (!File.Exists(path)) return new ProtectionState();
        try
        {
            return JsonSerializer.Deserialize<ProtectionState>(File.ReadAllText(path), Json) ?? new ProtectionState();
        }
        catch (JsonException)
        {
            // A damaged file must not stop protection; counters restart from zero.
            return new ProtectionState();
        }
    }

    private T WithLock<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, @"Local\NoWayYahweh.State");
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Estado ocupado por outro processo.");
        }
        catch (AbandonedMutexException)
        {
            // Previous holder crashed; the atomic file replace means the file is still consistent.
        }
        try { return action(); }
        finally { mutex.ReleaseMutex(); }
    }
}
