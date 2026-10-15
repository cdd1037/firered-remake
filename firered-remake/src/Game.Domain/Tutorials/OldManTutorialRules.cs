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
/// Source semantic RNG for the forced-capture tutorial, with explicit campaign ownership.
/// No status, badge or weather effects; Intimidate is the modeled entry effect.
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

    public static OldManTutorialResult Start(GamePokemonCatalog pokemon, PartyPokemon hiddenPlayerPokemon, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(hiddenPlayerPokemon);
        var catalog = pokemon.Catalog;
        if (!catalog.Species.TryGetValue(WeedleSpecies, out var species))
            throw new NotSupportedException("Tutorial Weedle is missing from the campaign catalog.");
        var error = PokemonStateValidator.Validate(catalog, hiddenPlayerPokemon.Pokemon);
        if (error is not null) throw new ArgumentException(error, nameof(hiddenPlayerPokemon));
        if (hiddenPlayerPokemon.IsEgg || hiddenPlayerPokemon.Pokemon.CurrentHp <= 0)
            throw new ArgumentException("Select the first living non-egg member before starting the tutorial.", nameof(hiddenPlayerPokemon));
        // The forced catch has no damage, weather turn, contact or escape phase. Retain the
        // implemented tutorial item/entry-ability boundary; ownership alone does not enable effects.
        if (hiddenPlayerPokemon.HeldItem != "ITEM_NONE" && !Game.Domain.WildBattle.FuchsiaBattleScope.SupportsHeldItem(hiddenPlayerPokemon.HeldItem) ||
            hiddenPlayerPokemon.Status != 0 || hiddenPlayerPokemon.Mail != 255)
            throw new NotSupportedException("Tutorial excludes unmodeled held items, status and mail.");
        if (!Game.Domain.WildBattle.FuchsiaBattleScope.SupportsAbility(hiddenPlayerPokemon.Pokemon.AbilityId))
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
        bool intimidate = hiddenPlayerPokemon.Pokemon.AbilityId == "ABILITY_INTIMIDATE";
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
