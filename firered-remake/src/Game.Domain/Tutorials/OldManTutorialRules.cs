using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Tutorials;

public enum OldManTutorialEventKind
{
    WeedleCreated, BattleSetupCompleted, WildAppeared, EntryOrderDetermined, ActionPrompt, MoveCursorToBag,
    OpenDemoBag, SwitchToKeyItems, SwitchToPokeBalls, SelectPokeBall, RestorePlayerBag,
    EnemyMoveSelected, ThrowPokeBall, BallShake, Caught, ReturnToField, IntimidateActivated
}
/// <summary>Tick values belong to the named controller, not a combined wall-clock/ROM frame count.</summary>
public sealed record OldManTutorialEvent(OldManTutorialEventKind Kind, string SourceSymbol,
    string Clock = "none", int SourceTick = 0, int Value = 0);
public sealed record OldManMaleAttempt(uint OriginalTrainerId, uint Personality, PokemonGender Gender);
public sealed record OldManTutorialResult(PokemonInstance Weedle, RngState Rng,
    ImmutableArray<RandomTrace> RandomTrace, ImmutableArray<OldManMaleAttempt> MaleAttempts,
    ImmutableArray<int> SimulatedAiRng, int HeldItemRoll, ImmutableArray<int> InitialBattlerOrder,
    ushort RandomTurnNumber, ImmutableArray<int> EnemyMoveSlotAttempts, int EnemyMoveSlot,
    ImmutableArray<OldManTutorialEvent> Events)
{
    /// <summary>Temporary battle stage only; the forced capture never executes an enemy attack.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int EnemyAttackStageDelta { get; init; }
    public int GenerationAttempts => MaleAttempts.Length;
    public int SourceOutcome => 7; // B_OUTCOME_CAUGHT
    public int SelectedItemSourceId => 4; // ITEM_POKE_BALL
    public bool CanCancel => false;
}
public sealed record OldManTutorialReturn(int SourceOutcome, int SelectedItemSourceId, string SavedCallback,
    ImmutableDictionary<int, int> SpecialVariables, RngState Rng);

/// <summary>
/// Source semantic RNG for the early single-battle, no status/badge/weather profile.
/// Route 3 explicitly admits Oran and deterministic Intimidate entry effects.
/// Mt. Moon adds its authenticated catalog, neutral abilities and source wild-held items.
/// Start is an all-or-nothing pure calculation. Presentation consumes Events without performing rules.
/// No party, inventory, dex, EXP, EV, PP, HP, capture formula or next-turn rules are invoked.
/// The caller supplies its first alive non-egg party member and retains every original player field.
/// </summary>
public static class OldManTutorialRules
{
    public const string RulesVersion = "firered-old-man-tutorial-semantic-v1";
    public const int CaughtOutcome = 7;
    public const int PokeBallSourceItemId = 4;
    public static SpeciesId WeedleSpecies { get; } = new("pret:SPECIES_WEEDLE");

    public static OldManTutorialResult Start(PokemonCatalog catalog, PartyPokemon hiddenPlayerPokemon, RngState rng, bool route22Rules = false, bool route3Rules = false, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false)
    {
        celadonGymRules |= rocketHideoutRules;
        rockTunnelRules |= celadonApproachRules || celadonGymRules;
        route11Rules |= rockTunnelRules;
        ssAnneRules |= route11Rules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        ceruleanRules |= billRules;
        mtMoonRules |= ceruleanRules;
        route3Rules |= mtMoonRules;
        PokemonFactory.ValidateCatalog(catalog);
        ArgumentNullException.ThrowIfNull(hiddenPlayerPokemon);
        if ((rocketHideoutRules ? catalog.Species.Count != 99 : celadonGymRules ? catalog.Species.Count != 98 : celadonApproachRules ? catalog.Species.Count != 94 : rockTunnelRules ? catalog.Species.Count != 93 : route11Rules ? catalog.Species.Count != 79 : ssAnneRules ? catalog.Species.Count != 73 : vermilionRules ? catalog.Species.Count != 70 : billRules ? catalog.Species.Count != 68 : ceruleanRules ? catalog.Species.Count != 65 : mtMoonRules ? catalog.Species.Count != 56 : route3Rules ? catalog.Species.Count != 40 : route22Rules ? catalog.Species.Count != 32 : catalog.Species.Count is not (17 or 22 or 29)) || !catalog.Species.TryGetValue(WeedleSpecies, out var species))
            throw new NotSupportedException("The authenticated catalog must match the selected tutorial profile.");
        var error = PokemonStateValidator.Validate(catalog, hiddenPlayerPokemon.Pokemon);
        if (error is not null) throw new ArgumentException(error, nameof(hiddenPlayerPokemon));
        if (hiddenPlayerPokemon.IsEgg || hiddenPlayerPokemon.Pokemon.CurrentHp <= 0)
            throw new ArgumentException("Select the first living non-egg member before starting the tutorial.", nameof(hiddenPlayerPokemon));
        // Source items.json assigns these cave wild-held items HOLD_EFFECT_NONE.
        bool mtMoonHeldItem = mtMoonRules && hiddenPlayerPokemon.HeldItem is
            "ITEM_TINY_MUSHROOM" or "ITEM_BIG_MUSHROOM" or "ITEM_MOON_STONE";
        if ((hiddenPlayerPokemon.HeldItem != "ITEM_NONE" && !(route3Rules && hiddenPlayerPokemon.HeldItem == "ITEM_ORAN_BERRY") && !mtMoonHeldItem && !(rockTunnelRules && Game.Domain.WildBattle.RocketHideoutBattleScope.SupportsHeldItemForCatalog(catalog, hiddenPlayerPokemon.HeldItem)) && !(route11Rules && Game.Domain.WildBattle.Route11BattleScope.SupportsHeldItem(hiddenPlayerPokemon.HeldItem)) && !(ssAnneRules && Game.Domain.WildBattle.SsAnneBattleScope.SupportsHeldItem(hiddenPlayerPokemon.HeldItem)) && !(vermilionRules && VermilionPickupRules.IsKnownHeldItem(hiddenPlayerPokemon.HeldItem))) ||
            hiddenPlayerPokemon.Status != 0 || hiddenPlayerPokemon.Mail != 255)
            throw new NotSupportedException("Tutorial profile excludes unmodeled held items, status and mail.");
        // Old profiles stay narrow. Route 3 models Intimidate below; the other admitted abilities
        // have no effect before the forced catch in this no-weather/no-damage tutorial.
        bool route3Ability = route3Rules && hiddenPlayerPokemon.Pokemon.AbilityId is
            "ABILITY_INTIMIDATE" or "ABILITY_SWIFT_SWIM" or "ABILITY_POISON_POINT" or "ABILITY_CUTE_CHARM" or
            "ABILITY_COMPOUND_EYES" or "ABILITY_STATIC" or "ABILITY_SAND_VEIL" or "ABILITY_ROCK_HEAD" or "ABILITY_STURDY";
        // None of the additional source abilities handles ABILITYEFFECT_ON_SWITCHIN.
        // The forced ball completes before attacks, contact, weather turns or escape checks.
        bool mtMoonAbility = mtMoonRules && hiddenPlayerPokemon.Pokemon.AbilityId is
            "ABILITY_INNER_FOCUS" or "ABILITY_CHLOROPHYLL" or "ABILITY_EFFECT_SPORE" or "ABILITY_MAGNET_PULL" or
            "ABILITY_STENCH" or "ABILITY_STICKY_HOLD" or "ABILITY_SOUNDPROOF" or "ABILITY_LEVITATE";
        bool ceruleanAbility = ceruleanRules && hiddenPlayerPokemon.Pokemon.AbilityId is
            "ABILITY_SYNCHRONIZE" or "ABILITY_INSOMNIA" or "ABILITY_SHELL_ARMOR" or
            "ABILITY_WATER_VEIL" or "ABILITY_NATURAL_CURE" or "ABILITY_ILLUMINATE";
        bool billAbility=billRules && hiddenPlayerPokemon.Pokemon.AbilityId is "ABILITY_OBLIVIOUS" or "ABILITY_OWN_TEMPO";
        if (hiddenPlayerPokemon.Pokemon.AbilityId is not ("ABILITY_OVERGROW" or "ABILITY_BLAZE" or "ABILITY_TORRENT" or
            "ABILITY_KEEN_EYE" or "ABILITY_RUN_AWAY" or "ABILITY_GUTS" or "ABILITY_SHIELD_DUST" or "ABILITY_SHED_SKIN" or "ABILITY_SWARM") && !((route22Rules || route3Rules) && hiddenPlayerPokemon.Pokemon.AbilityId=="ABILITY_VITAL_SPIRIT") && !route3Ability && !mtMoonAbility && !ceruleanAbility && !billAbility && !(rockTunnelRules && Game.Domain.WildBattle.RocketHideoutBattleScope.SupportsAbilityForCatalog(catalog, hiddenPlayerPokemon.Pokemon.AbilityId)) && !(route11Rules && Game.Domain.WildBattle.Route11BattleScope.SupportsAbility(hiddenPlayerPokemon.Pokemon.AbilityId)) && !(ssAnneRules && Game.Domain.WildBattle.SsAnneBattleScope.SupportsAbility(hiddenPlayerPokemon.Pokemon.AbilityId)) && !(vermilionRules && hiddenPlayerPokemon.Pokemon.AbilityId=="ABILITY_PICKUP"))
            throw new NotSupportedException("Unmodeled tutorial entry ability.");
        if (species.SourceSpeciesId != 13 || species.GenderRatio != 127 || species.Ability1 != "ABILITY_SHIELD_DUST")
            throw new ArgumentException("Unexpected tutorial Weedle definition.", nameof(catalog));
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unsupported RNG algorithm.");
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        ushort Draw(string purpose, int bound = 65536)
        {
            var next = Gen3Rng.Next(rng, purpose, bound);
            rng = next.State;
            trace.Add(next.Trace);
            return next.Value;
        }
        uint Draw32(string purpose) => (uint)Draw(purpose + ".low16") | ((uint)Draw(purpose + ".high16") << 16);
        var attempts = ImmutableArray.CreateBuilder<OldManMaleAttempt>();
        uint ot, pid;
        PokemonGender gender;
        do
        {
            // CreateMaleMon redraws BOTH random preset OT and fixed personality on each rejection.
            ot = Draw32("old_man.create_male.ot");
            pid = Draw32("old_man.create_male.personality");
            gender = PokemonFactory.GenderFromPersonality(species.GenderRatio, pid);
            attempts.Add(new(ot, pid, gender));
        } while (gender != PokemonGender.Male);
        // Fixed personality / preset OT consumes only the two existing factory IV draws.
        var created = PokemonFactory.Create(catalog, new(WeedleSpecies, 5, ot, IvPolicy.Random, PersonalityPolicy.Fixed(pid)), rng);
        rng = created.Rng;
        trace.AddRange(created.Trace);
        var weedle = created.Pokemon;
        if (weedle.Moves.Length != 2 || weedle.Moves[0].Move.Value != "pret:MOVE_POISON_STING" ||
            weedle.Moves[1].Move.Value != "pret:MOVE_STRING_SHOT")
            throw new ArgumentException("Tutorial requires the original ordered Weedle learnset.", nameof(catalog));
        var ai = ImmutableArray.CreateBuilder<int>(4);
        for (int i = 0; i < 4; i++) ai.Add(100 - Draw("old_man.setup.simulated_ai." + i, 16) % 16);
        // SetWildMonHeldItem draws even when both source held-item entries are ITEM_NONE.
        int heldRoll = Draw("old_man.setup.wild_held_item", 100) % 100;
        int playerSpeed = hiddenPlayerPokemon.Pokemon.Stats.Speed, enemySpeed = weedle.Stats.Speed;
        bool enemyFirst = playerSpeed < enemySpeed;
        if (playerSpeed == enemySpeed)
            enemyFirst = (Draw("old_man.entry.speed_tie", 2) & 1) != 0;
        ImmutableArray<int> initialOrder = enemyFirst ? [1, 0] : [0, 1];
        // Source TryDoEventsBeforeFirstTurn sorts speed BEFORE assigning gRandomTurnNumber.
        ushort turnNumber = Draw("old_man.entry.random_turn_number");
        var slots = ImmutableArray.CreateBuilder<int>();
        int slot;
        do
        {
            slot = Draw("old_man.enemy.move_slot", 4) & 3;
            slots.Add(slot);
        } while (slot >= weedle.Moves.Length);
        // Item action sorts ahead of the enemy move. The forced ball branch never draws shake/odds RNG.
        ImmutableArray<OldManTutorialEvent> events = [
            new(OldManTutorialEventKind.WeedleCreated, "CreateMaleMon"),
            new(OldManTutorialEventKind.BattleSetupCompleted, "SetWildMonHeldItem"),
            new(OldManTutorialEventKind.WildAppeared, "sText_WildPkmnAppearedPause"),
            new(OldManTutorialEventKind.EntryOrderDetermined, "TryDoEventsBeforeFirstTurn"),
            new(OldManTutorialEventKind.ActionPrompt, "gText_WhatWillOldManDo"),
            new(OldManTutorialEventKind.EnemyMoveSelected, "OpponentHandleChooseMove", Value: slot),
            new(OldManTutorialEventKind.MoveCursorToBag, "SimulateInputChooseAction", "action_controller", 64),
            new(OldManTutorialEventKind.OpenDemoBag, "InitOldManBag", "action_controller", 128),
            new(OldManTutorialEventKind.SwitchToKeyItems, "Task_Bag_OldManTutorial", "bag_controller", 102),
            new(OldManTutorialEventKind.SwitchToPokeBalls, "Task_Bag_OldManTutorial", "bag_controller", 204),
            new(OldManTutorialEventKind.SelectPokeBall, "Task_Bag_OldManTutorial", "bag_controller", 306, PokeBallSourceItemId),
            new(OldManTutorialEventKind.RestorePlayerBag, "RestorePlayerBag", "bag_controller", 408),
            new(OldManTutorialEventKind.ThrowPokeBall, "sText_OldManUsedItem", Value: PokeBallSourceItemId),
            new(OldManTutorialEventKind.BallShake, "BALL_3_SHAKES_SUCCESS", Value: 1),
            new(OldManTutorialEventKind.BallShake, "BALL_3_SHAKES_SUCCESS", Value: 2),
            new(OldManTutorialEventKind.BallShake, "BALL_3_SHAKES_SUCCESS", Value: 3),
            new(OldManTutorialEventKind.Caught, "sText_GotchaPkmnCaught2", Value: CaughtOutcome),
            new(OldManTutorialEventKind.ReturnToField, "CB2_ReturnToFieldContinueScriptPlayMapMusic")
        ];
        // AbilityBattleEffects(INTIMIDATE1) runs before switch-in items and random turn number.
        // Weedle has Shield Dust, which does not block Intimidate; the script consumes no RNG.
        bool intimidate = route3Rules && hiddenPlayerPokemon.Pokemon.AbilityId == "ABILITY_INTIMIDATE";
        if (intimidate)
            events = events.Insert(4, new(OldManTutorialEventKind.IntimidateActivated,
                "BattleScript_IntimidateActivatesEnd3", Value: -1));
        // Oran's HOLD_EFFECT_RESTORE_HP has no switch-in handler. The forced ball ends the
        // battle before ITEMEFFECT_NORMAL, so even a low-HP hidden lead retains its berry.
        return new(weedle, rng, trace.ToImmutable(), attempts.ToImmutable(), ai.ToImmutable(), heldRoll,
            initialOrder, turnNumber, slots.ToImmutable(), slot, events) { EnemyAttackStageDelta = intimidate ? -1 : 0 };
    }

    /// <summary>No mutation/RNG here. Resume the existing waitstate, including its original call stack.</summary>
    public static OldManTutorialReturn Finish(OldManTutorialResult tutorial)
    {
        ArgumentNullException.ThrowIfNull(tutorial);
        return new(CaughtOutcome, PokeBallSourceItemId, "CB2_ReturnToFieldContinueScriptPlayMapMusic",
            ImmutableDictionary<int, int>.Empty.Add(0x800D, CaughtOutcome).Add(0x800E, PokeBallSourceItemId), tutorial.Rng);
    }
}
