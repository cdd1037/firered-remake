using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.WildBattle;

public sealed record WildBattleSourceCallSelection(int? SourceMoveId, RngState Rng,
    ImmutableArray<RandomTrace> Trace, ImmutableArray<int> Candidates);

/// <summary>Source party slots used by Assist. HP, status and PP do not filter its source pool.</summary>
public sealed record WildBattleAssistMember(bool IsEgg, ImmutableArray<int> Moves);

/// <summary>
/// Pinned source selection commands only. They neither admit an effect nor execute an action.
/// Target selection follows these commands and uses the selected move's authentic target mode.
/// </summary>
public static class WildBattleSourceCallers
{
    private static readonly ImmutableHashSet<int> Forbidden =
        [118, 165, 166, 102, 68, 243, 182, 197, 203, 194, 214, 168, 266, 289, 270, 343, 271, 264];
    public static bool IsDirectMetronomeChoice(int sourceMoveId) =>
        sourceMoveId is >= 1 and <= 354 && !Forbidden.Contains(sourceMoveId);

    public static WildBattleSourceCallSelection Metronome(RngState rng)
    {
        ValidateRng(rng);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        while (true)
        {
            var draw = Gen3Rng.Next(rng, "wild-battle.metronome-select", 512);
            rng = draw.State; trace.Add(draw.Trace);
            int candidate = (draw.Value & 511) + 1;
            if (IsDirectMetronomeChoice(candidate)) return new(candidate, rng, trace.ToImmutable(), []);
        }
    }

    public static WildBattleSourceCallSelection Assist(ImmutableArray<WildBattleAssistMember> party,
        int actorPartyIndex, RngState rng)
    {
        ValidateRng(rng);
        if (party.IsDefaultOrEmpty || party.Length > 6 || actorPartyIndex < 0 || actorPartyIndex >= party.Length)
            throw new ArgumentException("Assist requires the actual bounded party and actor slot.");
        var candidates = ImmutableArray.CreateBuilder<int>();
        for (int member = 0; member < party.Length; member++)
        {
            var mon = party[member];
            if (mon is null || mon.Moves.IsDefault || mon.Moves.Length > 4 ||
                mon.Moves.Any(move => move is < 0 or > 354)) throw new ArgumentException("Invalid source Assist party moves.");
            if (member == actorPartyIndex || mon.IsEgg) continue;
            foreach (int move in mon.Moves)
                if (IsDirectMetronomeChoice(move) && move is not (119 or 274)) candidates.Add(move);
        }
        var pool = candidates.ToImmutable();
        if (pool.IsEmpty) return new(null, rng, [], pool);
        var draw = Gen3Rng.Next(rng, "wild-battle.assist-select", 256);
        // The source multiplies the low byte; modulo would give a different distribution.
        int index = ((draw.Value & 255) * pool.Length) >> 8;
        return new(pool[index], draw.State, [draw.Trace], pool);
    }

    public static WildBattleSourceCallSelection MirrorMove(int actorBattler, int lastTakenMove,
        ImmutableArray<int> lastTakenFromBattlers, RngState rng)
    {
        ValidateRng(rng);
        if (lastTakenFromBattlers.IsDefault || lastTakenFromBattlers.Length is not (2 or 4) ||
            actorBattler < 0 || actorBattler >= lastTakenFromBattlers.Length || !ValidHistory(lastTakenMove) ||
            lastTakenFromBattlers.Any(move => !ValidHistory(move)))
            throw new ArgumentException("Mirror Move requires valid source battler-history registers.");
        var pool = lastTakenFromBattlers.Where((move, slot) => slot != actorBattler && Available(move)).ToImmutableArray();
        // A direct last-taken register wins even when a fallback candidate differs.
        if (Available(lastTakenMove)) return new(lastTakenMove, rng, [], pool);
        if (pool.IsEmpty) return new(null, rng, [], pool);
        var draw = Gen3Rng.Next(rng, "wild-battle.mirror-move-select", pool.Length);
        // Even a one-candidate fallback consumes the source Random() call.
        return new(pool[draw.Value % pool.Length], draw.State, [draw.Trace], pool);
    }

    public static int NaturePower(WildBattleTerrain terrain) => terrain switch
    {
        WildBattleTerrain.Grass => 78, WildBattleTerrain.LongGrass => 75,
        WildBattleTerrain.Sand => 89, WildBattleTerrain.Underwater => 56,
        WildBattleTerrain.Water => 57, WildBattleTerrain.Pond => 61,
        WildBattleTerrain.Mountain => 157, WildBattleTerrain.Cave => 247,
        WildBattleTerrain.Building or WildBattleTerrain.Plain => 129,
        _ => throw new ArgumentOutOfRangeException(nameof(terrain))
    };
    private static bool Available(int move) => move is >= 1 and <= 354;
    private static bool ValidHistory(int move) => Available(move) || move is 0 or 65535;
    private static void ValidateRng(RngState rng)
    {
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown RNG algorithm.");
    }
}
