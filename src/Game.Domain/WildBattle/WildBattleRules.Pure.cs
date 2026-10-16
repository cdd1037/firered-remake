using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.Rules;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Game.Domain.WildBattle;

/// <summary>Pure source facts and shared calculations. Battle lifecycles are owned by SourceFourActionTurns.</summary>
public static partial class WildBattleRules
{
    internal static bool IsImprisoned(WildBattleCombatant actor, WildBattleCombatant? opposing, MoveId move) =>
        SourceMoveRestrictionRules.Imprisoned(move, opposing is null ? [] : [opposing]);
    public static bool IsMoveSelectable(WildBattleState state, WildBattleSide side, int slot)
    {
        var actor = side == WildBattleSide.Player ? state.Player : state.Opponent;
        var opposing = side == WildBattleSide.Player ? state.Opponent : state.Player;
        if (!UsesRoute11Rules(state.RulesVersion)) return IsMoveSelectable(actor, slot);
        if (ForcedMoveSlot(actor) is { } forced) return slot == forced;
        if (slot == StruggleSlot) return RequiresStruggle(actor, opposing);
        return IsMoveSelectable(actor, slot) && !IsImprisoned(actor, opposing, actor.Pokemon.Moves[slot].Move);
    }
    public static bool RequiresStruggle(WildBattleState state, WildBattleSide side) =>
        RequiresStruggle(side == WildBattleSide.Player ? state.Player : state.Opponent,
            UsesRoute11Rules(state.RulesVersion) ? side == WildBattleSide.Player ? state.Opponent : state.Player : null);
    private static bool RequiresStruggle(WildBattleCombatant actor, WildBattleCombatant? opposing) =>
        ForcedMoveSlot(actor) is null && !Enumerable.Range(0, actor.Pokemon.Moves.Length).Any(slot =>
            IsMoveSelectable(actor, slot) && !IsImprisoned(actor, opposing, actor.Pokemon.Moves[slot].Move));
    internal static bool ProtectFamily(MoveId? move) => move?.Value is "pret:MOVE_PROTECT" or "pret:MOVE_DETECT" or "pret:MOVE_ENDURE";
    // User-selected repair, 2026-10-10 03:29:56 UTC: fifth and subsequent attempts stay at 12.5%.
    // The original ROM reads beyond its four-entry table; this bound is not claimed as original behavior.
    public static int ProtectSuccessThreshold(int consecutiveUses) => consecutiveUses < 0 || consecutiveUses > 255
        ? throw new ArgumentOutOfRangeException(nameof(consecutiveUses)) : 65535 >> Math.Min(consecutiveUses, 3);
    /// <summary>Source move-selection restrictions. Taunt is also checked later by the action canceller.</summary>
    public static bool IsCalledMoveSelectable(PokemonCatalog catalog, WildBattleCombatant mon, int slot)
    {
        if (mon.CalledTurnState?.Lock is { } locked) return slot == locked.PpSlot;
        if (ForcedMoveSlot(mon) is { } forced) return slot == forced;
        bool Available(int i) => IsMoveSelectable(mon, i) && !CalledVolatileMoveLimited(catalog, mon, i);
        if (slot == StruggleSlot) return !Enumerable.Range(0, mon.Pokemon.Moves.Length).Any(Available);
        return slot >= 0 && slot < mon.Pokemon.Moves.Length && Available(slot);
    }
    private static bool CalledVolatileMoveLimited(PokemonCatalog catalog, WildBattleCombatant mon, int slot)
    {
        if (slot < 0 || slot >= mon.Pokemon.Moves.Length) return false;
        MoveId id = mon.Pokemon.Moves[slot].Move;
        return mon.CalledVolatile is { } v && (v.Tormented && id != StruggleMove && id == mon.LastUsedMove ||
            v.TauntTurnsRemaining > 0 && catalog.Moves.TryGetValue(id, out var move) && move.Power == 0);
    }
    public const string CalledMoveKernelRulesVersion = "firered-called-action-v1";
    public static bool IsCalledMoveEffectSupported(PokemonMoveDefinition move) => SourceBattleKernel.SupportsRecipe(move);
    public const string SourceEndTurnRulesVersion = "firered-source-end-turn-phases-v1";
    public const string RulesVersion = "firered-route1-wild-battle-semantic-v1";
    public const string ForestRulesVersion = "firered-forest-wild-battle-semantic-v1";
    // User-approved correction: Pewter and its explicit extensions use roll < secondaryChance; legacy versions retain <=.
    public const string PewterRulesVersion = "firered-pewter-battle-secondary-corrected-v1";
    public const string EarlyStarterRulesVersion = "firered-early-starter-battle-semantic-v1";
    public const string EarlyPartyRulesVersion = "firered-early-party-battle-semantic-v1";
    public const string Route22RulesVersion = "firered-route22-battle-semantic-v1";
    public const string Route3RulesVersion = "firered-route3-battle-semantic-v1";
    public const string MtMoonRulesVersion = "firered-mtmoon-battle-semantic-v1";
    public const string CeruleanRulesVersion = "firered-cerulean-battle-semantic-v1";
    public const string BillRulesVersion = "firered-bill-battle-semantic-v1";
    public const string VermilionRulesVersion = "firered-vermilion-battle-semantic-v1";
    public const string SsAnneRulesVersion = "firered-ssanne-battle-semantic-v1";
    public const string VermilionGymRulesVersion = "firered-vermilion-gym-battle-semantic-v1";
    public const string RockTunnelRulesVersion = "firered-rocktunnel-battle-semantic-v1";
    public const string CeladonApproachRulesVersion = "firered-celadon-approach-battle-semantic-v1";
    public const string RocketHideoutRulesVersion = "firered-rocket-hideout-battle-semantic-v1";
    public const string FuchsiaRulesVersion = "firered-fuchsia-route-battle-semantic-v1";
    public const string SnorlaxRulesVersion = "firered-snorlax-battle-semantic-v1";
    public const string TowerRulesVersion = "firered-pokemon-tower-battle-semantic-v1";
    public const string CeladonGymRulesVersion = "firered-celadon-gym-battle-semantic-v1";
    public const string Route11RulesVersion = "firered-route11-battle-semantic-v1";
    internal static bool UsesRoute11Rules(string version) => version is Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or TowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion;
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int StruggleSlot = -1;
    public static MoveId StruggleMove { get; } = new("pret:MOVE_STRUGGLE");

    public static int? ForcedMoveSlot(WildBattleCombatant combatant) => combatant.DigMoveSlot is {} digSlot ? digSlot : combatant.RolloutTurnsRemaining > 0 ? combatant.RolloutMoveSlot : combatant.RampageTurnsRemaining > 0 ? combatant.RampageMoveSlot : null;
    public static bool IsMoveSelectable(WildBattleCombatant combatant, int slot)
    {
        if (ForcedMoveSlot(combatant) is { } forced) return slot == forced;
        if (slot == StruggleSlot) return RequiresStruggle(combatant);
        return slot >= 0 && slot < combatant.Pokemon.Moves.Length && combatant.Pokemon.Moves[slot].CurrentPp > 0 &&
            (combatant.DisableTurnsRemaining == 0 || combatant.DisabledMove != combatant.Pokemon.Moves[slot].Move) &&
            (combatant.EncoreTurnsRemaining == 0 || combatant.EncoreMoveSlot == slot) &&
            SourceBattleHeldItems.ChoiceAllows(combatant, combatant.Pokemon.Moves[slot].Move);
    }
    public static bool RequiresStruggle(WildBattleCombatant combatant) => ForcedMoveSlot(combatant) is null &&
        !Enumerable.Range(0, combatant.Pokemon.Moves.Length).Any(slot =>
            combatant.Pokemon.Moves[slot].CurrentPp > 0 && (combatant.DisableTurnsRemaining == 0 || combatant.DisabledMove != combatant.Pokemon.Moves[slot].Move) &&
            (combatant.EncoreTurnsRemaining == 0 || combatant.EncoreMoveSlot == slot) &&
            SourceBattleHeldItems.ChoiceAllows(combatant, combatant.Pokemon.Moves[slot].Move));
    // Cmd_furycuttercalc increments BEFORE power calculation, with a five-hit cap.
    // This helper takes the stored counter before the next use, including at the cap.
    internal static int NextFuryCutterPower(int counter) => counter is >= 0 and <= 5
        ? 10 << Math.Min(counter, 4) : throw new ArgumentOutOfRangeException(nameof(counter));
    public const string SourceWildRulesVersion = "firered-source-wild-turns-v1";
    public static WildBattleSemiInvulnerable CalledSemiInvulnerable(WildBattleCombatant mon) =>
        mon.CalledTurnState?.Lock is { Kind: WildBattleCalledLockKind.Charge } charge ? SourceChargeRules.SemiInvulnerable(charge.Move)
            : mon.DigMoveSlot is not null ? WildBattleSemiInvulnerable.Underground : WildBattleSemiInvulnerable.None;
    internal static bool SnorlaxSleepTalkCandidate(PokemonCatalog catalog, WildBattleCombatant mon, WildBattleCombatant? foe, int slot)
    {
        if (slot < 0 || slot >= mon.Pokemon.Moves.Length) return false;
        var id = mon.Pokemon.Moves[slot].Move; var move = catalog.Moves[id];
        if (id.Value is "pret:MOVE_NONE" or "pret:MOVE_SLEEP_TALK" or "pret:MOVE_ASSIST" or "pret:MOVE_MIRROR_MOVE" or "pret:MOVE_METRONOME" or "pret:MOVE_FOCUS_PUNCH" or "pret:MOVE_UPROAR") return false;
        if (move.Effect is "EFFECT_SKULL_BASH" or "EFFECT_RAZOR_WIND" or "EFFECT_SKY_ATTACK" or "EFFECT_SOLAR_BEAM" or "EFFECT_SEMI_INVULNERABLE" or "EFFECT_BIDE") return false;
        if (mon.DisabledMove == id && mon.DisableTurnsRemaining > 0) return false;
        if (foe?.Imprison == true && foe.Pokemon.Moves.Any(m => m.Move == id)) return false;
        if (mon.EncoreTurnsRemaining > 0 && mon.EncoreMoveSlot is {} encore && mon.Pokemon.Moves[encore].Move != id) return false;
        return true;
    }
}
