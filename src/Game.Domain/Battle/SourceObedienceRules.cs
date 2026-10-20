using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Battle input supplied by the party owner. Names are literal Unicode names.
/// Mew and Deoxys follow the same ownership/badge rules as every other species.</summary>
public sealed record SourceObedienceMember(PartyMemberKey Key, string OriginalTrainerName);
public sealed record SourceObedienceContext(uint PlayerTrainerId, string PlayerTrainerName, byte BadgeMask,
    ImmutableArray<SourceObedienceMember> PlayerParty);
public enum SourceObedienceOutcome { Obey, IgnoreAsleep, Loaf, Nap, SelfHit, AlternateMove }
public sealed record SourceObedienceInput(int Level, bool PlayerSide, bool OtherTrainer, byte BadgeMask, uint Status, string Ability, MoveId Move, byte UnusableMoves, bool Uproar);
public sealed record SourceObedienceDecision(SourceObedienceOutcome Outcome, int? MoveSlot = null, int? LoafString = null)
{
    public int SourceReturn => Outcome is SourceObedienceOutcome.Obey ? 0 :
        Outcome is SourceObedienceOutcome.SelfHit or SourceObedienceOutcome.AlternateMove ? 2 : 1;
}
public sealed record SourceObedienceResult(SourceObedienceDecision Decision, RngState Rng, ImmutableArray<RandomTrace> Trace);

/// <summary>IsMonDisobedient's integer branches. Does not schedule turns or mutate battle storage.</summary>
public static class SourceObedienceRules
{
    public static bool IsOtherTrainer(uint otId, string otName, uint playerId, string playerName) =>
        otId != playerId || !playerName.StartsWith(otName, StringComparison.Ordinal);

    public static SourceObedienceResult Evaluate(SourceObedienceInput input, RngState rng)
    {
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        var decision = Decide(input, (purpose, bound) => {
            var draw = Gen3Rng.Next(rng, "source-obedience." + purpose, bound);
            rng = draw.State; trace.Add(draw.Trace); return draw.Value;
        });
        return new(decision, rng, trace.ToImmutable());
    }
    internal static SourceObedienceDecision Decide(SourceObedienceInput input, Func<string, int, ushort> draw)
    {
        if (input.Level is < 1 or > 100 || input.UnusableMoves > 15) throw new ArgumentOutOfRangeException(nameof(input));
        if (!input.PlayerSide) return new(SourceObedienceOutcome.Obey);
        // Intentional remake rule: no species-specific event-origin obedience gate.
        if (!input.OtherTrainer || (input.BadgeMask & 128) != 0) return new(SourceObedienceOutcome.Obey);
        int threshold = (input.BadgeMask & 32) != 0 ? 70 : (input.BadgeMask & 8) != 0 ? 50 :
            (input.BadgeMask & 2) != 0 ? 30 : 10;
        if (input.Level <= threshold || ((input.Level + threshold) * (draw("check", 256) & 255) >> 8) < threshold)
            return new(SourceObedienceOutcome.Obey);
        if ((input.Status & 7) != 0 && input.Move.Value is "pret:MOVE_SNORE" or "pret:MOVE_SLEEP_TALK")
            return new(SourceObedienceOutcome.IgnoreAsleep);
        int second = (input.Level + threshold) * (draw("alternate-check", 256) & 255) >> 8;
        if (second < threshold && input.Move.Value != "pret:MOVE_FOCUS_PUNCH")
        {
            if (input.UnusableMoves == 15) return Loaf();
            int slot; do { slot = draw("move-slot", 4) & 3; } while ((input.UnusableMoves & (1 << slot)) != 0);
            return new(SourceObedienceOutcome.AlternateMove, slot);
        }
        int difference = input.Level - threshold;
        int roll = draw("outcome", 256) & 255;
        if (roll < difference && (input.Status & 255) == 0 && input.Ability is not ("ABILITY_VITAL_SPIRIT" or "ABILITY_INSOMNIA") && !input.Uproar)
            return new(SourceObedienceOutcome.Nap);
        // Source subtracts even when nap was blocked: negative values cause self-damage.
        if (roll - difference < difference) return new(SourceObedienceOutcome.SelfHit);
        return Loaf();
        SourceObedienceDecision Loaf() => new(SourceObedienceOutcome.Loaf, LoafString: draw("loaf-string", 4) & 3);
    }

    internal static string? Validate(SourceObedienceContext? context, ImmutableArray<WildBattleCalledPartyMember> party)
    {
        if (context is null) return null;
        static bool Name(string? name) => NameTextRules.IsValidTrainerName(name);
        if (!Name(context.PlayerTrainerName) || context.PlayerParty.IsDefault || party.IsDefault ||
            context.PlayerParty.Length != party.Length || context.PlayerParty.Any(m => m is null ||
                m.Key.Side != BattleSide.Player || m.Key.Index < 0 || m.Key.Index >= party.Length || !Name(m.OriginalTrainerName)) ||
            context.PlayerParty.Select(m => m.Key).Distinct().Count() != party.Length)
            return "source_obedience_identity";
        if (party.Any(member => member?.Pokemon is null)) return "source_obedience_identity";
        return null;
    }
    internal static string? Validate(WildBattleState state)
    {
        if (state.Obedience is null) return null;
        if (state.SourceMechanicsRulesVersion != BattleRulesProfile.CanonicalSourceSinglesVersion)
            return "source_obedience_profile";
        if (state.CalledParty is not { } party) return "source_obedience_actual_party_required";
        return Validate(state.Obedience, party.Player);
    }
}

public sealed record SourceObedienceEvent(long Sequence, int Turn, BattlerSlot Actor, MoveId AttemptedMove,
    SourceObedienceDecision Decision) : SourceBattleEvent(Sequence, Turn);
