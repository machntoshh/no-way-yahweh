namespace NoWayYahweh.Core.Challenges;

/// <summary>
/// Random reasoning tasks: laborious enough to create a gap between impulse and decision,
/// never impossible. Every task is generated with its answer computed, so none can be unsolvable.
/// </summary>
public sealed class ChallengeGenerator(Random random)
{
    private static readonly string[] Names = ["Ana", "Bruno", "Carla", "Davi", "Elisa", "Felipe", "Gabriela", "Igor", "Júlia", "Lucas", "Marta", "Rafael"];
    private static readonly string[] Symbols = ["▲", "●", "■", "◆", "★", "✚", "♥", "☀"];

    public Challenge Next(IReadOnlyCollection<ChallengeKind> avoid)
    {
        var kinds = Enum.GetValues<ChallengeKind>().Where(k => !avoid.Contains(k)).ToArray();
        if (kinds.Length == 0) kinds = Enum.GetValues<ChallengeKind>();
        return Create(kinds[random.Next(kinds.Length)]);
    }

    public Challenge Create(ChallengeKind kind) => kind switch
    {
        ChallengeKind.Arithmetic => Arithmetic(),
        ChallengeKind.Sequence => Sequence(),
        ChallengeKind.Logic => Logic(),
        ChallengeKind.Memory => Memory(),
        ChallengeKind.Ordering => Ordering(),
        ChallengeKind.MultiStep => MultiStep(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private Challenge Arithmetic()
    {
        int a = random.Next(13, 49), b = random.Next(6, 19), c = random.Next(101, 400), d = random.Next(12, 90);
        return new(ChallengeKind.Arithmetic, "Conta de cabeça",
            $"Quanto é {a} × {b} − {c} + {d}?", (a * b - c + d).ToString());
    }

    private Challenge Sequence()
    {
        var terms = new long[6];
        switch (random.Next(5))
        {
            case 0: // arithmetic
                { long s = random.Next(3, 40), d = random.Next(4, 17); for (var i = 0; i < 6; i++) terms[i] = s + i * d; break; }
            case 1: // geometric
                { long s = random.Next(2, 6), r = random.Next(2, 4); for (var i = 0; i < 6; i++) terms[i] = s * (long)Math.Pow(r, i); break; }
            case 2: // alternating increments
                { long s = random.Next(1, 20), p = random.Next(2, 9), q = random.Next(10, 19); terms[0] = s; for (var i = 1; i < 6; i++) terms[i] = terms[i - 1] + (i % 2 == 1 ? p : q); break; }
            case 3: // squares plus constant
                { long c = random.Next(-3, 8), o = random.Next(1, 5); for (var i = 0; i < 6; i++) terms[i] = (i + o) * (i + o) + c; break; }
            default: // Fibonacci-like
                { terms[0] = random.Next(1, 6); terms[1] = random.Next(2, 9); for (var i = 2; i < 6; i++) terms[i] = terms[i - 1] + terms[i - 2]; break; }
        }
        return new(ChallengeKind.Sequence, "Sequência numérica",
            $"Qual é o próximo número?  {string.Join(", ", terms[..5])}, …", terms[5].ToString());
    }

    private Challenge Logic()
    {
        // A hidden ranking of four people, described only by comparisons between neighbours
        // (shuffled), so the full order follows uniquely from the statements.
        var people = Names.OrderBy(_ => random.Next()).Take(4).ToArray();
        var (adjective, comparative) = random.Next(3) switch
        {
            0 => ("alto", "mais alto"),
            1 => ("velho", "mais velho"),
            _ => ("rápido", "mais rápido"),
        };
        var statements = Enumerable.Range(0, 3)
            .Select(i => $"{people[i]} é {comparative} que {people[i + 1]}.")
            .OrderBy(_ => random.Next());
        var position = random.Next(1, 5);
        var question = position == 1 ? $"Quem é o {comparative}?" : $"Quem está em {position}º lugar, do {comparative} para o menos {adjective}?";
        return new(ChallengeKind.Logic, "Quebra-cabeça lógico",
            $"{string.Join(" ", statements)}\n{question}", people[position - 1]);
    }

    private Challenge Memory()
    {
        var items = Enumerable.Range(0, 7).Select(_ => random.Next(10).ToString()).ToArray();
        var symbol = Symbols[random.Next(Symbols.Length)];
        var position = random.Next(2, 7);
        // Digits to recall, plus one symbol in the middle that must be left out: memory plus attention.
        var shown = items.Take(position).Append(symbol).Concat(items.Skip(position)).ToArray();
        return new(ChallengeKind.Memory, "Jogo de memória",
            $"Memorize a sequência. Depois digite só os números, na mesma ordem (ignore o símbolo {symbol}).",
            string.Concat(items), shown, ShowSeconds: 7);
    }

    private Challenge Ordering()
    {
        var numbers = Enumerable.Range(0, 8).Select(_ => random.Next(10, 1000)).Distinct().ToArray();
        while (numbers.Length < 8) numbers = numbers.Append(random.Next(10, 1000)).Distinct().ToArray();
        var descending = random.Next(2) == 0;
        var sorted = descending ? numbers.OrderDescending() : numbers.Order();
        return new(ChallengeKind.Ordering, "Organize as peças",
            $"Clique nas peças em ordem {(descending ? "decrescente (da maior para a menor)" : "crescente (da menor para a maior)")}.",
            string.Join(' ', sorted), numbers.Select(n => n.ToString()).ToArray());
    }

    private Challenge MultiStep()
    {
        var start = new TimeSpan(random.Next(6, 20), random.Next(0, 12) * 5, 0);
        int leg1 = random.Next(35, 140), stop = random.Next(8, 40), leg2 = random.Next(25, 110);
        var arrival = start + TimeSpan.FromMinutes(leg1 + stop + leg2);
        return new(ChallengeKind.MultiStep, "Raciocínio em etapas",
            $"Um ônibus sai às {start:hh\\:mm}. Viaja {leg1} minutos, para {stop} minutos numa rodoviária " +
            $"e viaja mais {leg2} minutos. A que horas chega? (formato HH:MM)",
            $"{(int)arrival.TotalHours % 24:00}:{arrival.Minutes:00}");
    }
}
