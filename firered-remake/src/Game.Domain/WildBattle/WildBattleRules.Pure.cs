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
    // Pinned FireRed battle_scripts_1.s scalar recipes. These registrations are used
    // only by the authenticated called-action contract, never the historical v1 path.
    private static bool HasCalledScalarDamageRecipe(string effect) => effect is
        "EFFECT_OHKO" or "EFFECT_RECOIL_IF_MISS" or "EFFECT_RECOIL" or "EFFECT_DOUBLE_EDGE" or
        "EFFECT_PSYWAVE" or "EFFECT_TRIPLE_KICK" or "EFFECT_FALSE_SWIPE" or "EFFECT_RETURN" or
        "EFFECT_PRESENT" or "EFFECT_FRUSTRATION" or "EFFECT_PAIN_SPLIT" or "EFFECT_HIDDEN_POWER" or
        "EFFECT_FACADE" or "EFFECT_SMELLINGSALT" or "EFFECT_ENDEAVOR" or "EFFECT_ERUPTION" or
        "EFFECT_BLAZE_KICK" or "EFFECT_POISON_TAIL";

    static partial void AddCalledScalarDamageSupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledScalarDamageRecipe(move.Effect)) supported = true;
    }
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
    internal static bool Route3Neutral(WildBattleCombatant p) => p.SourceChoiceMove is null && p.LastCalledMove is null && p.LastUsedMove is null && p.DisabledMove is null &&
        p.DisableTurnsRemaining == 0 && p.RolloutTurnsRemaining == 0 && p.RolloutMoveSlot is null &&
        p.InfatuationSource is null && p.BindingMove is null && p.DigMoveSlot is null && !p.SourceTracePending;
    private static string? ValidateRoute3Volatiles(WildBattleCombatant p, string side, bool route3, bool mtMoon = false, bool cerulean = false, bool bill = false, bool vermilion = false, bool ssAnne = false, bool vermilionGym = false, bool route11 = false, bool rockTunnelRules = false, bool celadonApproach = false, bool celadonGym = false, bool rocketHideout = false, bool tower = false, bool snorlax = false, bool fuchsia = false, bool canonicalSource = false)
    {
        if (!route3) return Route3Neutral(p) ? null : $"wild_battle_{side}_route3_volatile_unsupported";
        if (p.LastCalledMove is {} called && (!snorlax || p.LastUsedMove?.Value != "pret:MOVE_SLEEP_TALK" || !p.Pokemon.Moves.Any(x => x.Move == called)))
            return $"wild_battle_{side}_called_move_history";
        if (p.DisableTurnsRemaining is < 0 or > 5 || (p.DisableTurnsRemaining == 0) != (p.DisabledMove is null) ||
            p.DisabledMove is { } disabled && !p.Pokemon.Moves.Any(m => m.Move == disabled))
            return $"wild_battle_{side}_disable_state";
        if (p.RolloutTurnsRemaining is < 0 or > 4 || (p.RolloutTurnsRemaining == 0) != (p.RolloutMoveSlot is null) ||
            p.RolloutMoveSlot is { } slot && (slot < 0 || slot >= p.Pokemon.Moves.Length || !mtMoon && p.Pokemon.Moves[slot].Move.Value != "pret:MOVE_ROLLOUT"))
            return $"wild_battle_{side}_rollout_state";
        if (p.InfatuationSource is { } source && (!Enum.IsDefined(source) ||
            side == "player" && source != WildBattleSide.Opponent || side == "opponent" && source != WildBattleSide.Player ||
            (canonicalSource ? WildBattleTransformationRules.PersistentPokemon(p).Gender : p.Pokemon.Gender) == PokemonGender.Genderless)) return $"wild_battle_{side}_infatuation_state";
        if (p.BindingMove is { } binding && (p.BindTurnsRemaining == 0 || (binding.Value is not ("pret:MOVE_WRAP" or "pret:MOVE_BIND") && !(route11 && binding.Value == "pret:MOVE_SAND_TOMB"))))
            return $"wild_battle_{side}_binding_move";
        if (p.Pokemon.CurrentHp == 0 && (p.DisabledMove is not null || p.RolloutMoveSlot is not null || p.InfatuationSource is not null))
            return $"wild_battle_{side}_fainted_volatile";
        if (p.LastUsedMove is { } last && last != StruggleMove && !(fuchsia && last.Value == "pret:MOVE_UNAVAILABLE") && !(fuchsia ? FuchsiaBattleScope.Supports(last) : snorlax ? SnorlaxBattleScope.Supports(last) : tower ? PokemonTowerBattleScope.Supports(last) : rocketHideout ? RocketHideoutBattleScope.Supports(last) : celadonGym ? CeladonGymBattleScope.Supports(last) : celadonApproach ? CeladonApproachBattleScope.Supports(last) : rockTunnelRules ? RockTunnelBattleScope.Supports(last) : route11 ? Route11BattleScope.Supports(last) : vermilionGym ? VermilionGymBattleScope.Supports(last) : ssAnne ? SsAnneBattleScope.Supports(last) : vermilion ? VermilionBattleScope.Supports(last) : bill ? BillBattleScope.Supports(last) : cerulean ? CeruleanBattleScope.Supports(last) : mtMoon ? MtMoonBattleScope.Supports(last) : Route3BattleScope.Supports(last)))
            return $"wild_battle_{side}_last_move";
        return null;
    }
    private static bool HasCalledPartyRecipe(string effect) => effect is "EFFECT_CONVERSION" or "EFFECT_CONVERSION_2" or
        "EFFECT_SPIKES" or "EFFECT_HEAL_BELL" or "EFFECT_BATON_PASS" or "EFFECT_BEAT_UP" or "EFFECT_MEMENTO" or
        "EFFECT_ROLE_PLAY" or "EFFECT_RECYCLE" or "EFFECT_SKILL_SWAP" or "EFFECT_CAMOUFLAGE" or "EFFECT_TEETER_DANCE" or
        "EFFECT_TRICK" or "EFFECT_THIEF";
    private static void AddCalledPartyEffectSupport(PokemonMoveDefinition move, ref bool supported)
    { if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledPartyRecipe(move.Effect)) supported = true; }
    private static bool HasCalledFieldRecipe(string effect) => effect is "EFFECT_FREEZE_HIT" or "EFFECT_MIST" or
        "EFFECT_THUNDER" or "EFFECT_TOXIC" or "EFFECT_LIGHT_SCREEN" or "EFFECT_THAW_HIT" or
        "EFFECT_SAFEGUARD" or "EFFECT_SUNNY_DAY" or "EFFECT_HAIL" or "EFFECT_POISON_FANG" or "EFFECT_WEATHER_BALL";
    private static void AddCalledFieldEffectSupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledFieldRecipe(move.Effect)) supported = true;
    }
    private static bool HasCalledVolatileRecipe(string effect) => effect is "EFFECT_SUBSTITUTE" or "EFFECT_PERISH_SONG" or
        "EFFECT_FUTURE_SIGHT" or "EFFECT_STOCKPILE" or "EFFECT_SPIT_UP" or "EFFECT_SWALLOW" or "EFFECT_TORMENT" or
        "EFFECT_TAUNT" or "EFFECT_WISH" or "EFFECT_MAGIC_COAT" or "EFFECT_GRUDGE";
    private static void AddCalledVolatileSupport(PokemonMoveDefinition move, ref bool supported)
    { if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledVolatileRecipe(move.Effect)) supported = true; }

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
    private static bool HasStatRecoveryRecipe(string effect) => SourceRecipeContext.HasStatRecoveryRecipe(effect);
    static partial void AddCalledStatRecoverySupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasStatRecoveryRecipe(move.Effect)) supported = true;
    }
    public const string CalledMoveKernelRulesVersion = "firered-called-action-v1";
    static partial void AddCalledMoveRecipeSupport(PokemonMoveDefinition move, ref bool supported);
    static partial void AddCalledStatRecoverySupport(PokemonMoveDefinition move, ref bool supported);
    static partial void AddCalledScalarDamageSupport(PokemonMoveDefinition move, ref bool supported);
    public static bool IsCalledMoveEffectSupported(PokemonMoveDefinition move)
    {
        if (!MetronomeSourceMoveDefinition.Matches(move)) return false;
        bool supported = FuchsiaBattleScope.Supports(move.Id) && FuchsiaBattleScope.ValidDefinition(move);
        AddCalledMoveRecipeSupport(move, ref supported);
        AddCalledStatRecoverySupport(move, ref supported);
        AddCalledScalarDamageSupport(move, ref supported);
        AddCalledTurnLockSupport(move, ref supported);
        AddCalledVolatileSupport(move, ref supported);
        AddCalledFieldEffectSupport(move, ref supported);
        AddCalledPartyEffectSupport(move, ref supported);
        AddCalledCallerSupport(move, ref supported);
        AddPreparedMoveSupport(move, ref supported);
        return supported;
    }
    private static void AddPreparedMoveSupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && move.Effect is "EFFECT_FOCUS_PUNCH" or "EFFECT_SNATCH")
            supported = true;
    }
    internal static bool MtMoonNeutral(WildBattleCombatant p) => p.SpecialAttackStageDelta == 0 && p.SpecialDefenseStageDelta == 0 &&
        p.ChargeTurnsRemaining == 0 && p.EncoreTurnsRemaining == 0 && p.EncoreMoveSlot is null && p.EncoredMove is null &&
        !p.RampageUnableToMove && p.RampageTurnsRemaining == 0 && p.RampageMoveSlot is null && !p.Rage && !p.MudSport && !p.Minimized;
    private static string? ValidateMtMoonVolatiles(WildBattleCombatant p, string side, bool mtMoon, bool canonicalSource = false)
    {
        if (!mtMoon) return MtMoonNeutral(p) ? null : $"wild_battle_{side}_mtmoon_volatile_unsupported";
        if (p.SpecialAttackStageDelta is < -6 or > 6 || p.SpecialDefenseStageDelta is < -6 or > 6) return $"wild_battle_{side}_special_stat_stage";
        if (p.ChargeTurnsRemaining is < 0 or > 2) return $"wild_battle_{side}_charge_state";
        if (canonicalSource && (p.EncoreTurnsRemaining is < 0 or > 6 || (p.EncoreTurnsRemaining == 0) != (p.EncoreMoveSlot is null) ||
            (p.EncoreTurnsRemaining == 0) != (p.EncoredMove is null) || p.EncoreMoveSlot is < 0 or > 3 ||
            p.EncoredMove is { } encored && encored.Value is "pret:MOVE_ENCORE" or "pret:MOVE_STRUGGLE" or "pret:MOVE_MIRROR_MOVE"))
            return $"wild_battle_{side}_encore_state";
        if (!canonicalSource && (p.EncoreTurnsRemaining is < 0 or > 6 || (p.EncoreTurnsRemaining == 0) != (p.EncoreMoveSlot is null) ||
            (p.EncoreTurnsRemaining == 0) != (p.EncoredMove is null) || p.EncoreMoveSlot is { } slot &&
            (slot < 0 || slot >= p.Pokemon.Moves.Length || p.Pokemon.Moves[slot].Move != p.EncoredMove ||
             p.Pokemon.Moves[slot].Move.Value is "pret:MOVE_ENCORE" or "pret:MOVE_STRUGGLE" or "pret:MOVE_MIRROR_MOVE"))) return $"wild_battle_{side}_encore_state";
        if (p.RampageTurnsRemaining is < 0 or > 3 || (p.RampageTurnsRemaining == 0) != (p.RampageMoveSlot is null) ||
            p.RampageMoveSlot is { } rampage && (rampage < 0 || rampage >= p.Pokemon.Moves.Length) ||
            p.RampageTurnsRemaining > 0 && p.RolloutTurnsRemaining > 0) return $"wild_battle_{side}_rampage_state";
        if (p.Pokemon.CurrentHp == 0 && !MtMoonNeutral(p)) return $"wild_battle_{side}_fainted_mtmoon_volatile";
        return null;
    }
    public const string SourceEndTurnRulesVersion = "firered-source-end-turn-phases-v1";
    // These are script recipes, not aliases to a different move. Every invocation retains
    // the authenticated current definition, including its type, flags and move identity.
    private static bool HasReusableSourceRecipe(string effect) => SourceRecipeContext.HasGenericRecipe(effect);
    static partial void AddCalledMoveRecipeSupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasReusableSourceRecipe(move.Effect)) supported = true;
    }
    private static bool HasCalledCallerRecipe(string effect) => effect is "EFFECT_METRONOME" or
        "EFFECT_MIRROR_MOVE" or "EFFECT_NATURE_POWER" or "EFFECT_ASSIST" or "EFFECT_SLEEP_TALK" or "EFFECT_COUNTER" or "EFFECT_MIRROR_COAT" or "EFFECT_MIMIC" or "EFFECT_SKETCH";
    private static void AddCalledCallerSupport(PokemonMoveDefinition move, ref bool supported)
    { if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledCallerRecipe(move.Effect)) supported = true; }
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
    public static bool IsMoveSupported(MoveId move, bool forestRules = false) => forestRules ? ForestBattleScope.Supports(move) : move.Value is "pret:MOVE_TACKLE" or "pret:MOVE_SCRATCH"
        or "pret:MOVE_GROWL" or "pret:MOVE_TAIL_WHIP" or "pret:MOVE_SAND_ATTACK";

    public static bool IsMoveSupported(MoveId move, string rulesVersion) => rulesVersion switch
    {
        RockTunnelRulesVersion => RockTunnelBattleScope.Supports(move),
        FuchsiaRulesVersion => FuchsiaBattleScope.Supports(move),
        SnorlaxRulesVersion => SnorlaxBattleScope.Supports(move),
        TowerRulesVersion => PokemonTowerBattleScope.Supports(move),
        RocketHideoutRulesVersion => RocketHideoutBattleScope.Supports(move),
        CeladonGymRulesVersion => CeladonGymBattleScope.Supports(move),
        CeladonApproachRulesVersion => CeladonApproachBattleScope.Supports(move),
        Route11RulesVersion => Route11BattleScope.Supports(move),
        VermilionGymRulesVersion => VermilionGymBattleScope.Supports(move),
        SsAnneRulesVersion => SsAnneBattleScope.Supports(move),
        VermilionRulesVersion => VermilionBattleScope.Supports(move),
        BillRulesVersion => BillBattleScope.Supports(move),
        CeruleanRulesVersion => CeruleanBattleScope.Supports(move),
        MtMoonRulesVersion => MtMoonBattleScope.Supports(move),
        Route3RulesVersion => Route3BattleScope.Supports(move),
        Route22RulesVersion => Route22BattleScope.Supports(move),
        EarlyPartyRulesVersion => EarlyPartyBattleScope.Supports(move),
        EarlyStarterRulesVersion => EarlyStarterBattleScope.Supports(move),
        PewterRulesVersion => PewterBattleScope.Supports(move),
        ForestRulesVersion => ForestBattleScope.Supports(move),
        RulesVersion => IsMoveSupported(move), _ => false
    };
    internal static string? ValidateCombatant(PokemonCatalog? catalog, WildBattleCombatant? p, string side, bool forestRules = false, bool pewterRules = false, bool earlyStarterRules = false, bool earlyPartyRules = false, bool route22Rules = false, bool route3Rules = false, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool calledAction = false)
    {
        bool canonicalSource = calledAction && rockTunnelRules &&
            SourceBattleAdmission.HasCapability(catalog, SourceBattleCapability.CanonicalSpecies);
        if (p?.SourceChoiceMove is { } choice && (!canonicalSource || choice.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" ||
            p.Pokemon is null || !p.Pokemon.Moves.Any(m => m.Move == choice))) return $"wild_battle_{side}_choice_state";
        if (p is { TruantLoafing: true } && (!canonicalSource || p.Pokemon?.CurrentHp is null or 0)) return $"wild_battle_{side}_truant_state";
        if (p?.SourceMoveHistory is not null) return "wild_battle_called_history_requires_contract";
        if (p?.CalledDamage is not null) return "wild_battle_called_damage_requires_contract";
        if (!calledAction && p?.CopyMoves is not null) return "wild_battle_copy_moves_requires_contract";
        if (!calledAction && p?.CalledOverlay is not null) return "wild_battle_called_overlay_requires_contract";
        if (!calledAction && p?.SourcePartyStatusOverride is not null) return "wild_battle_source_party_status_requires_contract";
        if (p?.CalledVolatile is not null) return "wild_battle_called_volatile_requires_contract";
        if (p?.CalledTurnState is not null) return "wild_battle_called_turn_requires_contract";
        if (p is not null && (p.YawnTurnsRemaining is < 0 or > 2 || p.YawnTurnsRemaining != 0 && (!rockTunnelRules || (!canonicalSource && Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is not (102 or 107)) || p.Pokemon?.CurrentHp == 0))) return $"wild_battle_{side}_yawn_state";
        if (p is not null && ValidateTowerVolatiles(p, side, rockTunnelRules && (canonicalSource || Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 101 or 102 or 107)) is { } towerError) return towerError;
        if (p is not null && (p.FirstTurnCounter is < 0 or > 2 || p.FirstTurnCounter != 0 && ((!canonicalSource && Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is not (99 or 101 or 102 or 107)) || p.Pokemon?.CurrentHp == 0))) return $"wild_battle_{side}_first_turn_profile";
        if (p is { Rooted: true } && ((!canonicalSource && Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is not (98 or 99 or 101 or 102 or 107)) || p.Pokemon?.CurrentHp == 0)) return $"wild_battle_{side}_rooted_profile";
        route11Rules |= rockTunnelRules;
        if (p is null || p.Pokemon is null) return $"wild_battle_{side}_missing";
        if (p.RampageUnableToMove && ((!canonicalSource && catalog?.Species.Count != 107) || !rockTunnelRules || p.RampageTurnsRemaining == 0 || p.Pokemon.CurrentHp == 0)) return $"wild_battle_{side}_rampage_interruption";
        if (p.FuryCutterCounter is < 0 or > 5 || p.FuryCutterCounter != 0 && ((!canonicalSource && catalog?.Species.Count != 107) || !rockTunnelRules || p.Pokemon.CurrentHp == 0)) return $"wild_battle_{side}_fury_cutter_counter";
        vermilionGymRules |= route11Rules;
        ssAnneRules |= vermilionGymRules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        if (p.ProtectConsecutiveUses is < 0 or > 255 || p.Protected && p.Enduring ||
            (p.Identified || p.DamagedByOpponentThisTurn || p.Protected || p.Enduring || p.ProtectConsecutiveUses != 0) && (!rockTunnelRules || p.Pokemon.CurrentHp == 0) ||
            p.KnockedOffItem is not null && (!rockTunnelRules || !calledAction && p.HeldItem != "ITEM_NONE" || p.KnockedOffItem == "ITEM_NONE" || !(canonicalSource ? SourceBattleAdmission.SupportsHeldItem(catalog, p.KnockedOffItem) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 107 ? FuchsiaBattleScope.SupportsHeldItem(p.KnockedOffItem) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102 ? SnorlaxBattleScope.SupportsHeldItem(p.KnockedOffItem) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 101 ? PokemonTowerBattleScope.SupportsHeldItem(p.KnockedOffItem) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 99 ? RocketHideoutBattleScope.SupportsHeldItem(p.KnockedOffItem) : RockTunnelBattleScope.SupportsHeldItem(p.KnockedOffItem))))
            return $"wild_battle_{side}_rocktunnel_volatile";
        if (p.Imprison && (!route11Rules || p.Pokemon.CurrentHp == 0)) return $"wild_battle_{side}_imprison_state";
        if (p.FlashFireBoost && (!ssAnneRules || p.Pokemon.CurrentHp == 0 || !calledAction && WildBattleEffective.Ability(p) != "ABILITY_FLASH_FIRE")) return $"wild_battle_{side}_flash_fire_state";
        if (p.DigMoveSlot is {} dig && (!billRules || p.Pokemon.CurrentHp==0 || dig<0 || dig>=p.Pokemon.Moves.Length)) return $"wild_battle_{side}_dig_state";
        if(billRules && p.InfatuationSource is not null && WildBattleEffective.Ability(p)=="ABILITY_OBLIVIOUS") return $"wild_battle_{side}_oblivious_infatuation";
        if(billRules && p.ConfusionTurnsRemaining>0 && WildBattleEffective.Ability(p)=="ABILITY_OWN_TEMPO") return $"wild_battle_{side}_own_tempo_confusion";
        var error = WildBattleTransformationRules.Validate(catalog, p, rockTunnelRules && (canonicalSource || catalog?.Species.Count == 107));
        if (error is not null) return $"wild_battle_{side}_{error}";
        if (canonicalSource && p.Pokemon.Moves.Any(m => !IsCalledMoveEffectSupported(catalog!.Moves[m.Move])))
            return $"wild_battle_{side}_move_effect_unsupported";
        if (!canonicalSource && (rockTunnelRules ? !(Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog!) == 107 ? FuchsiaBattleScope.ValidSpecies(catalog!.Species[p.Pokemon.Species]) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog!) == 102 ? EeveeBattleScope.ValidSharedSpecies(catalog!.Species[p.Pokemon.Species]) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog!) == 101 ? PokemonTowerBattleScope.ValidSpecies(catalog!.Species[p.Pokemon.Species]) : RocketHideoutBattleScope.ValidSharedSpecies(catalog!.Species[p.Pokemon.Species])) : route11Rules ? !Route11BattleScope.ValidSpecies(catalog!.Species[p.Pokemon.Species]) : ssAnneRules ? !SsAnneBattleScope.ValidSpecies(catalog!.Species[p.Pokemon.Species]) : vermilionRules && !VermilionBattleScope.ValidSpecies(catalog!.Species[p.Pokemon.Species]))) return $"wild_battle_{side}_vermilion_species_definition";
        if (catalog!.SourceCommit != SourceCommit) return "wild_battle_source_commit";
        ceruleanRules |= billRules;
        if (!canonicalSource && ceruleanRules && Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : 65)) return "wild_battle_cerulean_catalog_incomplete";
        mtMoonRules |= ceruleanRules;
        if (mtMoonRules && !ceruleanRules && catalog.Species.Count != 56) return "wild_battle_mtmoon_catalog_incomplete";
        route3Rules |= mtMoonRules;
        if (route3Rules && !mtMoonRules && catalog.Species.Count != 40) return "wild_battle_route3_catalog_incomplete";
        route22Rules |= route3Rules;
        if (!route22Rules && p.Pokemon.SourceSpeciesId is 21 or 22 or 56)
            return $"wild_battle_{side}_route22_species_unsupported";
        earlyPartyRules |= route22Rules;
        earlyStarterRules |= earlyPartyRules;
        if (!canonicalSource && (rockTunnelRules ? !(Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog!) == 107 ? FuchsiaBattleScope.SupportsSpecies(p.Pokemon.Species) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog!) == 102 ? EeveeBattleScope.SupportsSharedSpecies(p.Pokemon.Species) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog!) == 101 ? PokemonTowerBattleScope.SupportsSpecies(p.Pokemon.Species) : RocketHideoutBattleScope.SupportsSharedSpecies(p.Pokemon.Species)) : route11Rules ? !Route11BattleScope.SupportsSpecies(p.Pokemon.Species) : vermilionGymRules ? !VermilionGymBattleScope.SupportsSpecies(p.Pokemon.Species) : ssAnneRules ? !SsAnneBattleScope.SupportsSpecies(p.Pokemon.Species) : vermilionRules ? !VermilionBattleScope.SupportsSpecies(p.Pokemon.Species) : billRules ? !BillBattleScope.SupportsSpecies(p.Pokemon.Species) : ceruleanRules ? !CeruleanBattleScope.SupportsSpecies(p.Pokemon.Species) : mtMoonRules ? !MtMoonBattleScope.SupportsSpecies(p.Pokemon.Species) : route3Rules ? !Route3BattleScope.SupportsSpecies(p.Pokemon.Species) : route22Rules && !Route22BattleScope.SupportsSpecies(p.Pokemon.Species))) return $"wild_battle_{side}_route22_species_unsupported";
        // Trace may acquire an immunity at entry while a prior party status still exists.
        // The original IMMUNITY move-end stage, not ON_SWITCHIN/TRACE, clears that status.
        bool deferredTraceImmunity = canonicalSource && p.CalledOverlay?.Ability is not null &&
            WildBattleTransformationRules.PersistentPokemon(p).AbilityId == "ABILITY_TRACE";
        if (!deferredTraceImmunity && ceruleanRules && (WildBattleEffective.Ability(p) == "ABILITY_INSOMNIA" && WildBattleStatus.IsSleeping(p.Status) || WildBattleEffective.Ability(p) == "ABILITY_WATER_VEIL" && p.Status == WildBattleStatus.Burn)) return $"wild_battle_{side}_ability_status_immunity";
        if (!ceruleanRules && (p.LastLandedMove is not null || p.LastHitBy is not null)) return $"wild_battle_{side}_cerulean_history_unsupported";
        if (p.LastHitBy is { } hitBy && !Enum.IsDefined(hitBy) || p.LastLandedMove is { } landed && landed.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" or "pret:MOVE_STRUGGLE") &&
            !IsMoveSupported(landed, rockTunnelRules ? (canonicalSource || Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 107) ? FuchsiaRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102 ? SnorlaxRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 101 ? TowerRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 99 ? RocketHideoutRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 98 ? CeladonGymRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 94 ? CeladonApproachRulesVersion : RockTunnelRulesVersion : route11Rules ? Route11RulesVersion : vermilionGymRules ? VermilionGymRulesVersion : ssAnneRules ? SsAnneRulesVersion : vermilionRules ? VermilionRulesVersion : billRules ? BillRulesVersion : CeruleanRulesVersion)) return $"wild_battle_{side}_cerulean_history";
        if ((!ceruleanRules || p.Pokemon.CurrentHp == 0) && p.WaterSport) return $"wild_battle_{side}_water_sport_unsupported";
        if (!deferredTraceImmunity && route22Rules && WildBattleEffective.Ability(p) == "ABILITY_VITAL_SPIRIT" && WildBattleStatus.IsSleeping(p.Status))
            return $"wild_battle_{side}_vital_spirit_sleep";
        pewterRules |= earlyStarterRules;
        forestRules |= pewterRules;
        if (p.SpecialBadgeBoost && (!canonicalSource || side == "opponent")) return $"wild_battle_{side}_special_badge_unsupported";
        if (p.DefenseBadgeBoost && (!rockTunnelRules || (!canonicalSource && catalog.Species.Count != 107) || !canonicalSource && catalog.Moves.Count != 354 || side == "opponent")) return $"wild_battle_{side}_defense_badge_unsupported";
        if (p.SpeedBadgeBoost && (!vermilionGymRules || side == "opponent")) return $"wild_battle_{side}_speed_badge_unsupported";
        if (p.AttackBadgeBoost && (!pewterRules || side == "opponent")) return $"wild_battle_{side}_attack_badge_unsupported";
        if (pewterRules ? p.BindTurnsRemaining is < 0 or > 6 || p.Pokemon.CurrentHp == 0 && (p.BindTurnsRemaining != 0 || p.DefenseCurled)
            : p.BindTurnsRemaining != 0 || p.DefenseCurled) return $"wild_battle_{side}_volatile_unsupported";
        if (p.LeechSeedSource is { } source && (!earlyStarterRules || !Enum.IsDefined(source) || p.Pokemon.CurrentHp == 0 ||
            side == "player" && source != WildBattleSide.Opponent || side == "opponent" && source != WildBattleSide.Player))
            return $"wild_battle_{side}_leech_seed_unsupported";
        if (p.ConfusionTurnsRemaining is < 0 or > 5 || (!earlyPartyRules || p.Pokemon.CurrentHp == 0) &&
            (p.ConfusionTurnsRemaining != 0 || p.FocusEnergy || p.Flinched)) return $"wild_battle_{side}_early_party_volatile_unsupported";
        var route3Error = ValidateRoute3Volatiles(p, side, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules, Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 94, Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 98, Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 99, (canonicalSource || Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 101 or 102 or 107), (canonicalSource || Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 102 or 107), (canonicalSource || catalog.Species.Count == 107), canonicalSource) ?? ValidateMtMoonVolatiles(p, side, mtMoonRules, canonicalSource);
        if (route3Error is not null) return route3Error;
        // The source lock can outlive EXP replacement of its move slot. Its independent
        // definition must still be present and authenticated before command execution.
        if (billRules && p.DigMoveSlot is not null && (!catalog.Moves.TryGetValue(new("pret:MOVE_DIG"),out var digMove) || !BillBattleScope.ValidDefinition(digMove))) return "wild_battle_locked_move_definition_unsupported";
        if (mtMoonRules)
        {
            MoveId? lockedMove = p.RolloutTurnsRemaining > 0 ? new("pret:MOVE_ROLLOUT") :
                p.RampageTurnsRemaining > 0 ? new("pret:MOVE_THRASH") : null;
            if (lockedMove is { } locked && (!catalog.Moves.TryGetValue(locked, out var definition) ||
                !MtMoonBattleScope.ValidDefinition(definition))) return "wild_battle_locked_move_definition_unsupported";
        }
        if (p.HeldItem != "ITEM_NONE" && !(rockTunnelRules ? canonicalSource ? SourceBattleAdmission.SupportsHeldItem(catalog, p.HeldItem) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 107 ? FuchsiaBattleScope.SupportsHeldItem(p.HeldItem) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102 ? SnorlaxBattleScope.SupportsHeldItem(p.HeldItem) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 101 ? PokemonTowerBattleScope.SupportsHeldItem(p.HeldItem) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 99 ? RocketHideoutBattleScope.SupportsHeldItem(p.HeldItem) : RockTunnelBattleScope.SupportsHeldItem(p.HeldItem) : route11Rules ? Route11BattleScope.SupportsHeldItem(p.HeldItem) : ssAnneRules ? SsAnneBattleScope.SupportsHeldItem(p.HeldItem) : vermilionRules ? VermilionBattleScope.SupportsHeldItem(p.HeldItem) : mtMoonRules ? MtMoonBattleScope.SupportsHeldItem(p.HeldItem) : route3Rules && p.HeldItem == "ITEM_ORAN_BERRY")) return $"wild_battle_{side}_held_item_unsupported";
        if (forestRules ? !WildBattleStatus.IsSupported(p.Status, earlyStarterRules, route11Rules) : p.Status != 0) return $"wild_battle_{side}_status_unsupported";
        if (p.AttackStage is < 0 or > 12 || p.DefenseStage is < 0 or > 12 || p.AccuracyStage is < 0 or > 12 ||
            p.EvasionStage is < 0 or > 12 || p.SpeedStage is < 0 or > 12) return $"wild_battle_{side}_stat_stage";
        if (canonicalSource ? !SourceBattleAdmission.SupportsAbility(catalog, WildBattleEffective.Ability(p)) : rockTunnelRules ? !(Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 102 or 107 ? SnorlaxBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 99 or 101 ? RocketHideoutBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : RockTunnelBattleScope.SupportsAbility(WildBattleEffective.Ability(p))) : route11Rules ? !Route11BattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : ssAnneRules ? !SsAnneBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : vermilionRules ? !VermilionBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : billRules ? !BillBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : ceruleanRules ? !CeruleanBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : mtMoonRules ? !MtMoonBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : route3Rules ? !Route3BattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : route22Rules ? !Route22BattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : pewterRules ? !PewterBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : forestRules ? !ForestBattleScope.SupportsAbility(WildBattleEffective.Ability(p)) : WildBattleEffective.Ability(p) is not ("ABILITY_OVERGROW" or "ABILITY_BLAZE" or "ABILITY_TORRENT" or
            "ABILITY_KEEN_EYE" or "ABILITY_RUN_AWAY" or "ABILITY_GUTS")) return $"wild_battle_{side}_ability_unsupported";
        if (forestRules)
        {
            // Reject an unchecked out-of-scope set at entry in the opt-in closure, rather
            // than accepting a battle that can later softlock on random foe selection.
            if (earlyPartyRules && !((Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102 && side != "opponent" || (canonicalSource || catalog.Species.Count == 107) && (side != "opponent" || p.Transform is not null))) && p.Pokemon.Moves.Any(m => !(calledAction && catalog.Moves.TryGetValue(m.Move, out var calledDefinition) && IsCalledMoveEffectSupported(calledDefinition)) && !(rockTunnelRules ? CeladonApproachBattleScope.SupportsForCatalog(catalog, m.Move) : route11Rules ? Route11BattleScope.Supports(m.Move) : vermilionGymRules ? VermilionGymBattleScope.Supports(m.Move) : ssAnneRules ? SsAnneBattleScope.Supports(m.Move) : vermilionRules ? VermilionBattleScope.Supports(m.Move) : billRules ? BillBattleScope.Supports(m.Move) : ceruleanRules ? CeruleanBattleScope.Supports(m.Move) : mtMoonRules ? MtMoonBattleScope.Supports(m.Move) : route3Rules ? Route3BattleScope.Supports(m.Move) : route22Rules ? Route22BattleScope.Supports(m.Move) : EarlyPartyBattleScope.Supports(m.Move))))
                return $"wild_battle_{side}_move_effect_unsupported";
            foreach (var slot in p.Pokemon.Moves.Where(m => !canonicalSource && IsMoveSupported(m.Move, rockTunnelRules ? Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 107 ? FuchsiaRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102 ? SnorlaxRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 101 ? TowerRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 99 ? RocketHideoutRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 98 ? CeladonGymRulesVersion : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 94 ? CeladonApproachRulesVersion : RockTunnelRulesVersion : route11Rules ? Route11RulesVersion : vermilionGymRules ? VermilionGymRulesVersion : ssAnneRules ? SsAnneRulesVersion : vermilionRules ? VermilionRulesVersion : billRules ? BillRulesVersion : ceruleanRules ? CeruleanRulesVersion : mtMoonRules ? MtMoonRulesVersion : route3Rules ? Route3RulesVersion : route22Rules ? Route22RulesVersion : earlyPartyRules ? EarlyPartyRulesVersion : earlyStarterRules ? EarlyStarterRulesVersion : pewterRules ? PewterRulesVersion : ForestRulesVersion)))
                if (!(rockTunnelRules ? CeladonApproachBattleScope.ValidDefinitionForCatalog(catalog, catalog.Moves[slot.Move]) : route11Rules ? Route11BattleScope.ValidDefinition(catalog.Moves[slot.Move]) : vermilionGymRules ? VermilionGymBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : ssAnneRules ? SsAnneBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : vermilionRules ? VermilionBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : billRules ? BillBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : ceruleanRules ? CeruleanBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : mtMoonRules ? MtMoonBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : route3Rules ? Route3BattleScope.ValidDefinition(catalog.Moves[slot.Move]) : route22Rules ? Route22BattleScope.ValidDefinition(catalog.Moves[slot.Move]) : earlyPartyRules ? EarlyPartyBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : earlyStarterRules ? EarlyStarterBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : pewterRules ? PewterBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : ForestBattleScope.ValidDefinition(catalog.Moves[slot.Move]))) return "wild_battle_move_definition_unsupported";
            return null;
        }
        foreach (var slot in p.Pokemon.Moves.Where(m => !canonicalSource && IsMoveSupported(m.Move)))
        {
            var m = catalog.Moves[slot.Move];
            var expected = m.Id.Value switch
            {
                "pret:MOVE_TACKLE" => (33, 35, 35, 95, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED"),
                "pret:MOVE_SCRATCH" => (10, 35, 40, 100, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED"),
                "pret:MOVE_GROWL" => (45, 40, 0, 100, "TYPE_NORMAL", "EFFECT_ATTACK_DOWN", "MOVE_TARGET_BOTH"),
                "pret:MOVE_TAIL_WHIP" => (39, 30, 0, 100, "TYPE_NORMAL", "EFFECT_DEFENSE_DOWN", "MOVE_TARGET_BOTH"),
                _ => (28, 15, 0, 100, "TYPE_GROUND", "EFFECT_ACCURACY_DOWN", "MOVE_TARGET_SELECTED")
            };
            if ((m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target) != expected ||
                m.Priority != 0 || m.SecondaryEffectChance != 0) return "wild_battle_move_definition_unsupported";
        }
        return null;
    }
    // Cmd_furycuttercalc increments BEFORE power calculation, with a five-hit cap.
    // This helper takes the stored counter before the next use, including at the cap.
    internal static int NextFuryCutterPower(int counter) => counter is >= 0 and <= 5
        ? 10 << Math.Min(counter, 4) : throw new ArgumentOutOfRangeException(nameof(counter));
    private static bool TowerNeutral(WildBattleCombatant p) => !p.Cursed && !p.Nightmare && !p.DestinyBond && p.MeanLookSource is null;
    private static string? ValidateTowerVolatiles(WildBattleCombatant p, string side, bool tower)
    {
        if ((!tower || p.Pokemon?.CurrentHp == 0) && !TowerNeutral(p)) return $"wild_battle_{side}_tower_volatile_profile";
        if (p.Nightmare && !WildBattleStatus.IsSleeping(p.Status)) return $"wild_battle_{side}_nightmare_awake";
        if (p.MeanLookSource is { } source && (!Enum.IsDefined(source) || side == "player" && source != WildBattleSide.Opponent || side == "opponent" && source != WildBattleSide.Player)) return $"wild_battle_{side}_mean_look_source";
        return null;
    }
    public const string SourceWildRulesVersion = "firered-source-wild-turns-v1";
    private static bool HasCalledTurnLockRecipe(string effect) => effect is "EFFECT_RAZOR_WIND" or
        "EFFECT_SEMI_INVULNERABLE" or "EFFECT_RECHARGE" or "EFFECT_SOLAR_BEAM" or "EFFECT_RAMPAGE" or
        "EFFECT_EARTHQUAKE" or "EFFECT_BIDE" or "EFFECT_SKULL_BASH" or "EFFECT_SKY_ATTACK" or
        "EFFECT_LOCK_ON" or "EFFECT_FURY_CUTTER" or "EFFECT_UPROAR" or "EFFECT_ROLLOUT" or "EFFECT_SKY_UPPERCUT" or "EFFECT_GUST" or "EFFECT_TWISTER";

    private static void AddCalledTurnLockSupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledTurnLockRecipe(move.Effect)) supported = true;
    }

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
