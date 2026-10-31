using System.Text.Json;
using System.Text.RegularExpressions;
using NoWayYahweh.Core;
using NoWayYahweh.Core.Challenges;

namespace NoWayYahweh.Tests;

/// <summary>
/// Every generated challenge must be solvable and its stored answer right. Each kind is checked
/// by re-deriving the answer from what the user actually sees (prompt and items).
/// </summary>
public class ChallengeGeneratorTests
{
    private static IEnumerable<Challenge> Many(ChallengeKind kind) =>
        Enumerable.Range(0, 200).Select(seed => new ChallengeGenerator(new Random(seed)).Create(kind));

    private static long[] Numbers(string text) =>
        Regex.Matches(text, @"-?\d+").Select(m => long.Parse(m.Value)).ToArray();

    [Fact]
    public void Arithmetic_answer_matches_the_expression()
    {
        foreach (var c in Many(ChallengeKind.Arithmetic))
        {
            var n = Numbers(c.Prompt);
            Assert.True(c.Check((n[0] * n[1] - n[2] + n[3]).ToString()), c.Prompt);
        }
    }

    [Fact]
    public void Sequence_answer_follows_a_rule_visible_in_the_terms()
    {
        foreach (var c in Many(ChallengeKind.Sequence))
        {
            var t = Numbers(c.Prompt);
            Assert.Equal(5, t.Length);
            long[] candidates =
            [
                t[4] + (t[4] - t[3]),                         // arithmetic
                t[3] != 0 ? t[4] * (t[4] / t[3]) : long.MinValue, // geometric
                t[4] + (t[3] - t[2]),                         // alternating increments
                t[4] + (t[4] - t[3]) + ((t[4] - t[3]) - (t[3] - t[2])), // constant second difference (squares)
                t[3] + t[4],                                  // Fibonacci-like
            ];
            var answer = long.Parse(c.Answer);
            Assert.Contains(answer, candidates);
        }
    }

    [Fact]
    public void Logic_answer_follows_from_the_statements()
    {
        foreach (var c in Many(ChallengeKind.Logic))
        {
            var lines = c.Prompt.Split('\n');
            var pairs = Regex.Matches(lines[0], @"(\p{L}+) é mais \p{L}+ que (\p{L}+)\.").Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
            Assert.Equal(3, pairs.Count);
            // Rebuild the ranking: the top is the only name never on the right side.
            var order = new List<string> { pairs.Select(p => p.Item1).Except(pairs.Select(p => p.Item2)).Single() };
            while (order.Count < 4) order.Add(pairs.Single(p => p.Item1 == order[^1]).Item2);
            var position = Regex.Match(lines[1], @"(\d)º") is { Success: true } m ? int.Parse(m.Groups[1].Value) : 1;
            Assert.True(c.Check(order[position - 1]), c.Prompt);
        }
    }

    [Fact]
    public void Memory_answer_is_the_digits_without_the_symbol()
    {
        foreach (var c in Many(ChallengeKind.Memory))
        {
            Assert.NotNull(c.ShowSeconds);
            Assert.True(c.Check(string.Concat(c.Items!.Where(i => i.All(char.IsDigit)))));
            Assert.Single(c.Items!, i => !i.All(char.IsDigit));
        }
    }

    [Fact]
    public void Ordering_answer_is_the_tiles_sorted_as_asked()
    {
        foreach (var c in Many(ChallengeKind.Ordering))
        {
            var tiles = c.Items!.Select(int.Parse).ToList();
            Assert.Equal(8, tiles.Distinct().Count());
            var sorted = c.Prompt.Contains("decrescente") ? tiles.OrderDescending() : tiles.Order();
            Assert.True(c.Check(string.Join(' ', sorted)), c.Prompt);
        }
    }

    [Fact]
    public void MultiStep_arrival_time_is_correct()
    {
        foreach (var c in Many(ChallengeKind.MultiStep))
        {
            var n = Numbers(c.Prompt); // HH, MM, leg1, stop, leg2
            var arrival = new TimeSpan((int)n[0], (int)n[1], 0) + TimeSpan.FromMinutes(n[2] + n[3] + n[4]);
            Assert.True(c.Check($"{(int)arrival.TotalHours % 24}h{arrival.Minutes:00}"), c.Prompt);
        }
    }

    [Theory]
    [InlineData("09:05", "9:05")]
    [InlineData("09:05", " 9h05 ")]
    [InlineData("Júlia", "julia")]
    [InlineData("12 34 567", "12, 34,  567")]
    [InlineData("-7", "-7")]
    public void Answers_are_compared_leniently(string answer, string typed) =>
        Assert.True(new Challenge(ChallengeKind.Logic, "", "", answer).Check(typed));

    [Theory]
    [InlineData("10:05", "10:50")]
    [InlineData("Davi", "Elisa")]
    [InlineData("123", "132")]
    public void Wrong_answers_are_rejected(string answer, string typed) =>
        Assert.False(new Challenge(ChallengeKind.Logic, "", "", answer).Check(typed));

    [Fact]
    public void Next_avoids_recent_kinds()
    {
        var generator = new ChallengeGenerator(new Random(1));
        var avoid = new[] { ChallengeKind.Arithmetic, ChallengeKind.Sequence, ChallengeKind.Logic };
        for (var i = 0; i < 100; i++) Assert.DoesNotContain(generator.Next(avoid).Kind, avoid);
    }

    [Fact]
    public void All_six_kinds_appear()
    {
        var generator = new ChallengeGenerator(new Random(2));
        Assert.Equal(6, Enumerable.Range(0, 200).Select(_ => generator.Next([]).Kind).Distinct().Count());
    }
}

public class PausePolicyTests : IDisposable
{
    private readonly TempDir _dir = new();
    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Longer_pauses_cost_more_challenges_and_end_on_their_own()
    {
        Assert.Equal([1, 2, 3], PausePolicy.Options.Select(o => o.Challenges));
        var now = DateTimeOffset.UtcNow;
        var state = new ProtectionState();
        PausePolicy.Pause(state, PausePolicy.Options[1], now);
        Assert.True(PausePolicy.IsPaused(state, now.AddMinutes(59)));
        Assert.False(PausePolicy.IsPaused(state, now.AddMinutes(61)));
        PausePolicy.Resume(state);
        Assert.False(PausePolicy.IsPaused(state, now));
    }

    [Fact]
    public void Only_the_last_three_kinds_are_remembered()
    {
        var state = new ProtectionState();
        foreach (var k in new[] { ChallengeKind.Arithmetic, ChallengeKind.Logic, ChallengeKind.Memory, ChallengeKind.Ordering })
            PausePolicy.Remember(state, k);
        Assert.Equal([ChallengeKind.Logic, ChallengeKind.Memory, ChallengeKind.Ordering], state.RecentChallengeKinds);
    }

    [Fact]
    public void Hello_reports_an_active_pause_and_nothing_otherwise()
    {
        var store = new StateStore(_dir.File("state.json"));
        var handler = new MessageHandler(store, () => []);
        JsonElement Hello() => JsonSerializer.SerializeToElement(handler.Handle("""{"type":"hello","browser":"brave","version":"0.4.0"}"""u8));

        Assert.Equal(JsonValueKind.Null, Hello().GetProperty("pausedUntil").ValueKind);

        var until = DateTimeOffset.UtcNow.AddMinutes(15);
        store.Update(s => s.PausedUntil = until);
        Assert.Equal(until.ToUnixTimeMilliseconds(), Hello().GetProperty("pausedUntil").GetInt64());

        store.Update(s => s.PausedUntil = DateTimeOffset.UtcNow.AddMinutes(-1)); // expired
        Assert.Equal(JsonValueKind.Null, Hello().GetProperty("pausedUntil").ValueKind);
    }
}
