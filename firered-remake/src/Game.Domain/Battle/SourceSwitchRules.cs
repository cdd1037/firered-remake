using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Shared SwitchInClearSetData scalar preparation and source party-selection commands.
/// Owners supply position references, permanent party storage and entry script timing.</summary>
internal static class SourceSwitchRules
{
    internal static WildBattleCombatant PrepareBody(WildBattleCombatant departing,
        WildBattleCalledPartyMember incoming, bool baton)
    {
        var next = new WildBattleCombatant(incoming.Pokemon,
            HeldItem: incoming.KnockedOffItem is not null ? "ITEM_NONE" : WildBattleSourcePartyEffects.PersistentHeldItem(incoming),
            Status: WildBattleSourceFieldEffects.ResetToxicCounter(incoming.Status), AttackBadgeBoost: departing.AttackBadgeBoost)
        { SpeedBadgeBoost = departing.SpeedBadgeBoost, DefenseBadgeBoost = departing.DefenseBadgeBoost,
            FirstTurnCounter = 2, KnockedOffItem = incoming.KnockedOffItem, CalledDamage = departing.CalledDamage };
        if (!baton) return next;
        return next with {
            AttackStage = departing.AttackStage, DefenseStage = departing.DefenseStage,
            SpeedStage = departing.SpeedStage, AccuracyStage = departing.AccuracyStage, EvasionStage = departing.EvasionStage,
            SpecialAttackStageDelta = departing.SpecialAttackStageDelta, SpecialDefenseStageDelta = departing.SpecialDefenseStageDelta,
            ConfusionTurnsRemaining = departing.ConfusionTurnsRemaining, FocusEnergy = departing.FocusEnergy,
            MeanLookSource = departing.MeanLookSource, Cursed = departing.Cursed, LeechSeedSource = departing.LeechSeedSource,
            Rooted = departing.Rooted, MudSport = departing.MudSport, WaterSport = departing.WaterSport,
            CalledVolatile = departing.CalledVolatile is { } v && (v.SubstituteHp > 0 || v.PerishSongTimer is not null)
                ? new() { SubstituteHp = v.SubstituteHp, PerishSongTimer = v.PerishSongTimer } : null,
            CalledTurnState = departing.CalledTurnState is { SureHitTurnsRemaining: > 0 } turn
                ? new() { SureHitTurnsRemaining = turn.SureHitTurnsRemaining, SureHitSource = turn.SureHitSource } : null
        };
    }

    internal static int ChooseForcedReserve(ImmutableArray<int> legal, Func<string, int, ushort> draw)
    {
        if (legal.IsDefaultOrEmpty) throw new SourceRecipeUnsupported("called_party_force_out_no_reserve");
        int index;
        do { index = draw("wild-battle.force-out.choose-party", 6) % 6; } while (!legal.Contains(index));
        return index;
    }

    // HandleTurnActionSelectionState queries stored abilities. None of these source queries
    // filters HP or absence. Magnet Pull also checks the user's partner after opposing bodies.
    internal static string? TrappingAbility(PokemonCatalog catalog, BattlerSlot actor,
        IEnumerable<BattlerSlot> slots, Func<BattlerSlot, WildBattleCombatant> get)
    {
        var bodies = slots.ToArray(); var own = get(actor);
        if (bodies.Any(s => s.Side != actor.Side && WildBattleEffective.Ability(get(s)) == "ABILITY_SHADOW_TAG"))
            return "ABILITY_SHADOW_TAG";
        if (WildBattleEffective.Ability(own) != "ABILITY_LEVITATE" && !WildBattleEffective.HasType(catalog, own, "TYPE_FLYING") &&
            bodies.Any(s => s.Side != actor.Side && WildBattleEffective.Ability(get(s)) == "ABILITY_ARENA_TRAP"))
            return "ABILITY_ARENA_TRAP";
        if (WildBattleEffective.HasType(catalog, own, "TYPE_STEEL") &&
            bodies.Any(s => s != actor && WildBattleEffective.Ability(get(s)) == "ABILITY_MAGNET_PULL"))
            return "ABILITY_MAGNET_PULL";
        return null;
    }
}
