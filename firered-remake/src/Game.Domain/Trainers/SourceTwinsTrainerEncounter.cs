using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public sealed record SourceTwinsBattleDefinition(string ScriptEntry, string IntroText, string DefeatText,
    string PostBattleText, string NotEnoughMonsText, TrainerSourceRef Source);
public sealed record SourceTwinsTrainerDefinition(string SourceCommit, string RulesVersion,
    TrainerDefinition Trainer, ImmutableArray<SourceTwinsBattleDefinition> Interactions,
    string EntryScript, string Callback, int BattleMode, uint BattleTypeFlags,
    string EncounterMusic, int EncounterMusicId, int TrainerFlag, int VictoryMoney);
public sealed record SourceTwinsEncounterResult(SourceBattleFrame? State, RngState Rng,
    ImmutableArray<SourceBattleEvent> Events, ImmutableArray<RandomTrace> Trace, string? Fault)
{
    public bool Succeeded => Fault is null;
}
public sealed record SourceTwinsEnemyChoice(BattlerSlot Actor, BattlerSlot ScoredTarget,
    SourceActionSelection Selection, ImmutableArray<int> Scores,
    ImmutableArray<int> SimulatedDamagePercent, ImmutableArray<ForestTrainerAbilityRead> AbilityReads);
public sealed record SourceTwinsAiResult(SourceBattleFrame State, ImmutableArray<SourceTwinsEnemyChoice> Choices,
    ImmutableArray<RandomTrace> Trace, string? Fault)
{
    public bool Succeeded => Fault is null;
    public ImmutableArray<SourceActionSelection> Selections => Choices.Select(c => c.Selection).ToImmutableArray();
}

/// <summary>Original Route8 shared trainer, with a single four-slot battle and real complete parties.
/// Encounter and CHECK_BAD_MOVE admission are independent of move recipe completion.</summary>
public static class SourceTwinsTrainerEncounter
{
    public const string Version = "firered-route8-twins-encounter-v1";
    public static TrainerId TrainerId { get; } = new("pret:TRAINER_TWINS_ELI_ANNE");
    public const int SharedTrainerFlag = 0x500 + 484;
    public const int VictoryMoney = 528;
    private static readonly string[][] NaturalMoves = [
        ["SING", "DOUBLE_SLAP", "FOLLOW_ME", "MINIMIZE"],
        ["DEFENSE_CURL", "POUND", "DISABLE", "ROLLOUT"]];
    private static readonly string[] Species = ["CLEFAIRY", "JIGGLYPUFF"];

    public static string? ValidateDefinition(SourceTwinsTrainerDefinition? definition)
    {
        if (definition is not { Trainer: { } t } || definition.SourceCommit != TrainerPartyFactory.SourceCommit ||
            definition.RulesVersion != Version || t.Id != TrainerId || t.SourceTrainerId != 484 ||
            t.TrainerClass != "TRAINER_CLASS_TWINS" || t.SourceTrainerClass != 92 || t.TrainerPic != "TRAINER_PIC_TWINS" ||
            t.SourceTrainerPic != 127 || t.SourceTrainerName != "ELI & ANNE" || t.EncounterMusicGender != 9 ||
            !t.DoubleBattle || t.PartyFlags != 0 || t.AiFlags != 1 || t.BaseMoney != 3 ||
            t.EncodedTrainerName.IsDefault || !t.EncodedTrainerName.SequenceEqual(Encode(t.SourceTrainerName)) ||
            t.Items.IsDefault || !t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE", 4)) ||
            t.AiFlagSymbols.IsDefault || !t.AiFlagSymbols.SequenceEqual(["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
            t.Party.IsDefault || t.Party.Length != 2 || definition.Interactions.IsDefault || definition.Interactions.Length != 2 ||
            definition.EntryScript != "EventScript_TryDoDoubleTrainerBattle" || definition.Callback != "CB2_EndTrainerBattle" ||
            definition.BattleMode != 4 || definition.BattleTypeFlags != 9 || definition.EncounterMusic != "MUS_ENCOUNTER_GIRL" ||
            definition.EncounterMusicId != 284 || definition.TrainerFlag != SharedTrainerFlag || definition.VictoryMoney != VictoryMoney)
            return "source_twins_definition";
        for (int i = 0; i < 2; i++)
        {
            var p = t.Party[i]; string name = i == 0 ? "Eli" : "Anne"; var b = definition.Interactions[i];
            if (p is null || p.Species.Value != "pret:SPECIES_" + Species[i] || p.SourceSpeciesId != (i == 0 ? 35 : 39) ||
                p.SpeciesName != Species[i] || p.EncodedSpeciesName.IsDefault || !p.EncodedSpeciesName.SequenceEqual(Encode(Species[i])) ||
                p.Level != 22 || p.SourceIv != 0 || p.HeldItem != "ITEM_NONE" || p.CustomMoves.IsDefault || !p.CustomMoves.IsEmpty ||
                b is null || b.ScriptEntry != $"Route8_EventScript_{name}" || b.IntroText != $"Route8_Text_{name}Intro" ||
                b.DefeatText != $"Route8_Text_{name}Defeat" || b.PostBattleText != $"Route8_Text_{name}PostBattle" ||
                b.NotEnoughMonsText != $"Route8_Text_{name}NotEnoughMons") return "source_twins_roster_or_interaction";
        }
        return null;
    }

    public static SourceTwinsEncounterResult Begin(PokemonCatalog catalog, SourceTwinsTrainerDefinition definition,
        ImmutableArray<WildBattleCalledPartyMember> playerParty, RngState rng,
        bool attackBadgeBoost = false, bool defenseBadgeBoost = false, bool speedBadgeBoost = false)
    {
        SourceTwinsEncounterResult Fail(string code) => new(null, rng, [], [], code);
        if (ValidateDefinition(definition) is { } error) return Fail(error);
        if (SourceMechanicsCatalog.For(catalog) is not { } proof) return Fail("source_twins_catalog_proof");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fail("source_twins_rng");
        if (playerParty.IsDefault || playerParty.Length is < 2 or > 6) return Fail("source_twins_player_party");
        foreach (var member in playerParty)
            if (member is null || SourcePokemonValidator.ValidatePermanent(proof, member.Pokemon) is not null ||
                member.KnockedOffItem is not null || member.PersistentHeldItemOverride is not null ||
                !SourceFourRewards.SupportsRewardHeldItem(member.HeldItem) ||
                member.Status is not (WildBattleStatus.None or WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) &&
                !WildBattleStatus.IsSleeping(member.Status) && !WildBattleSourceFieldEffects.IsValidToxic(member.Status))
                return Fail("source_twins_player_member");
        var active = playerParty.Select((m, i) => (m, i)).Where(x => !x.m.IsEgg && x.m.Pokemon.CurrentHp > 0).Take(2).Select(x => x.i).ToArray();
        if (active.Length != 2) return Fail("source_twins_two_usable_pokemon_required");
        if (!NaturalMovesMatch(catalog)) return Fail("source_twins_natural_moves");
        try
        {
            // SetUpBattleVars runs while flags are TRAINER only: four setup draws.
            // CreateNPCTrainerParty adds DOUBLE after generation, so there is no initial flank draw.
            var setupTrace = ImmutableArray.CreateBuilder<RandomTrace>(); var generationRng = rng;
            for (int slot = 0; slot < 4; slot++) {
                var draw = Gen3Rng.Next(generationRng, $"twins.entry.ai-setup.slot-{slot}", 16);
                generationRng = draw.State; setupTrace.Add(draw.Trace);
            }
            var generated = TrainerPartyFactory.CreateValidated(definition.Trainer, catalog, generationRng, proof);
            var enemy = generated.Party.Select(m => new WildBattleCalledPartyMember(m.Pokemon, m.HeldItem)).ToImmutableArray();
            var frame = new SourceBattleFrame {
                Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
                SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer,
                Rng = generated.Rng, Parties = [new(BattleSide.Player, playerParty), new(BattleSide.Opponent, enemy)],
                Slots = Enumerable.Range(0, 4).Select(i => {
                    var side = (BattleSide)(i & 1); int index = side == BattleSide.Player ? active[i / 2] : i / 2;
                    var p = side == BattleSide.Player ? playerParty[index] : enemy[index];
                    return new SourceBattleSlot(new(i), new(side, index), 1, new WildBattleCombatant(p.Pokemon,
                        HeldItem: p.HeldItem, Status: p.Status, AttackBadgeBoost: side == BattleSide.Player && attackBadgeBoost) {
                        FirstTurnCounter = 2, DefenseBadgeBoost = side == BattleSide.Player && defenseBadgeBoost,
                        SpeedBadgeBoost = side == BattleSide.Player && speedBadgeBoost });
                }).ToImmutableArray()
            };
            var entry = SourceFourEntryAbilities.InitializeEntry(catalog, frame, frame.Revision);
            if (!entry.Succeeded) return Fail(entry.Fault!);
            var turnNumber = Gen3Rng.Next(entry.State.Rng, "twins.entry.random-turn-number");
            var ready = SourceBattleEventHooks.Publish(entry.State with { Rng = turnNumber.State, RandomTurnNumber = turnNumber.Value },
                (n, t) => new SourceTurnReadyEvent(n, t, turnNumber.Value));
            return new(ready.State, ready.State.Rng, [.. entry.Events, ready.Event],
                [.. setupTrace, .. generated.Trace, .. entry.Trace, turnNumber.Trace], null);
        }
        catch (OverflowException) { return Fail("source_twins_rng_or_counter_exhausted"); }
        catch (ArgumentException) { return Fail("source_twins_invalid_generation"); }
        catch (NotSupportedException) { return Fail("source_twins_generation_not_supported"); }
    }

    public static SourceTwinsAiResult ChooseEnemyActions(PokemonCatalog catalog, SourceBattleFrame frame,
        long expectedRevision, string? knownPlayerAbility = null)
    {
        SourceTwinsAiResult Fail(string code) => new(frame, [], [], code);
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } capability) return Fail(capability);
        if (frame.Revision != expectedRevision || frame.Outcome != WildBattleOutcome.Ongoing || frame.FourTurn is not null ||
            !frame.Queue.IsEmpty || !frame.PendingSwitches.IsEmpty || !frame.Reservations.IsEmpty || frame.DeferredKoVacancyMask != 0)
            return Fail("source_twins_ai_phase");
        if (!OriginalEnemyParty(frame)) return Fail("source_twins_ai_original_roster");
        if (knownPlayerAbility is not null && !SourceMechanicsCatalog.For(catalog)!.Abilities.ContainsKey(knownPlayerAbility))
            return Fail("source_twins_ai_ability_history");
        try
        {
            var rng = frame.Rng; var trace = ImmutableArray.CreateBuilder<RandomTrace>();
            var choices = ImmutableArray.CreateBuilder<SourceTwinsEnemyChoice>();
            foreach (var actor in new[] { new BattlerSlot(1), new BattlerSlot(3) })
            {
                if (!frame.CanAct(actor)) continue;
                var cell = frame.Get(actor); var user = cell.Body!;
                if (cell.Registers.Turn?.Lock is { } forced)
                {
                    choices.Add(new(actor, forced.Target, new(cell.Occupant!.Value, SourceActionKind.Move, forced.PpSlot, forced.Target), [], [], []));
                    continue;
                }
                // Source avoids the controller entirely during MULTIPLETURNS/RECHARGE.
                // Rollout is represented by its actual four-position lock once its recipe is admitted.
                if (user.RolloutTurnsRemaining > 0 || user.EncoreMoveSlot is not null)
                    return Fail("source_twins_ai_forced_move_owner_required");
                var simulated = ImmutableArray.CreateBuilder<int>(4);
                for (int i = 0; i < 4; i++) {
                    var draw = Gen3Rng.Next(rng, $"twins.ai.battler.{actor.Value}.setup.slot.{i}.damage-percent", 16);
                    rng = draw.State; trace.Add(draw.Trace); simulated.Add(100 - draw.Trace.Result);
                }
                var targetDraw = Gen3Rng.Next(rng, $"twins.ai.battler.{actor.Value}.setup.target-flank");
                rng = targetDraw.State; trace.Add(targetDraw.Trace);
                var target = new BattlerSlot(targetDraw.Value & 2);
                if (frame.IsSourceAbsent(target)) target = new(target.Value ^ 2);
                if (!frame.CanAct(target)) return Fail("source_twins_ai_no_living_target");
                var context = new SourceTrainerAiSlotContext(catalog, frame, actor, target, simulated.MoveToImmutable(), rng);
                var result = CeruleanTrainerAi.Choose(catalog, new(user, frame.Get(target).Body!, knownPlayerAbility, 1), rng,
                    fuchsiaRules: true, rain: frame.Field.RainTurnsRemaining > 0,
                    reflect: frame.Sides[0].ReflectTurnsRemaining > 0, userReflect: frame.Sides[1].ReflectTurnsRemaining > 0,
                    slots: context);
                if (!result.Succeeded || result.MoveSlot is not { } chosen) return Fail(result.Diagnostic?.Code ?? "source_twins_ai_choice");
                rng = result.Rng; trace.AddRange(result.Trace);
                var move = catalog.Moves[user.Pokemon.Moves[chosen].Move];
                var selectedTarget = move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? actor : target;
                choices.Add(new(actor, target, new(cell.Occupant!.Value, SourceActionKind.Move, chosen, selectedTarget),
                    result.Scores, context.Simulated, result.AbilityReads));
            }
            if (choices.Count == 0) return Fail("source_twins_ai_no_actor");
            return new(frame with { Rng = rng, Revision = checked(frame.Revision + 1) }, choices.ToImmutable(), trace.ToImmutable(), null);
        }
        catch (OverflowException) { return Fail("source_twins_ai_counter_exhausted"); }
    }

    private static bool NaturalMovesMatch(PokemonCatalog catalog) => Enumerable.Range(0, 2).All(i =>
        PokemonMoveRules.InitialMovesCore(catalog, catalog.Species[new("pret:SPECIES_" + Species[i])], 22)
            .Select(m => m.Move.Value).SequenceEqual(NaturalMoves[i].Select(m => "pret:MOVE_" + m)));
    private static bool OriginalEnemyParty(SourceBattleFrame frame)
    {
        var party = frame.Party(BattleSide.Opponent).Members;
        if (party.Length != 2) return false;
        uint nameHash = 0;
        for (int i = 0; i < 2; i++) {
            foreach (int c in Encode("ELI & ANNE" + Species[i])) nameHash += (uint)c;
            var m = party[i]; var p = m.Pokemon;
            if (m.IsEgg || p.Species.Value != "pret:SPECIES_" + Species[i] || p.Level != 22 ||
                p.Personality != 0x80 + (nameHash << 8) || p.Ivs != SixStats.Zero || p.Evs != SixStats.Zero ||
                p.AbilityId != "ABILITY_CUTE_CHARM" || p.Moves.Any(m => m.PpUps != 0) || !p.Moves.Select(s => s.Move.Value).SequenceEqual(NaturalMoves[i].Select(s => "pret:MOVE_" + s))) return false;
        }
        return true;
    }
    private static IEnumerable<int> Encode(string value) => value.Select(c => c switch { ' ' => 0, '&' => 0x2d, _ => 0xbb + c - 'A' });
}

/// <summary>Opaque validated explicit source positions for the shared scorer. It never creates a singles battle.</summary>
public sealed class SourceTrainerAiSlotContext
{
    private readonly PokemonCatalog catalog;
    private readonly SourceBattleFrame frame;
    private readonly BattlerSlot actor, target;
    private readonly RngState rng;
    internal ImmutableArray<int> Simulated { get; }
    internal SourceTrainerAiSlotContext(PokemonCatalog catalog, SourceBattleFrame frame, BattlerSlot actor,
        BattlerSlot target, ImmutableArray<int> simulated, RngState rng)
    { this.catalog = catalog; this.frame = frame; this.actor = actor; this.target = target; Simulated = simulated; this.rng = rng; }
    internal string? Validate(PokemonCatalog candidate, CeruleanTrainerAiInput input, RngState candidateRng) =>
        !ReferenceEquals(candidate, catalog) || input.User != frame.Get(actor).Body || input.Target != frame.Get(target).Body ||
        input.AiFlags != 1 || candidateRng != rng ? "source_twins_ai_slot_context" : null;
    internal bool ShouldProcess(int slot) => slot < frame.Get(actor).Body!.Pokemon.Moves.Length &&
        frame.Get(actor).Body!.Pokemon.Moves[slot].CurrentPp > 0;
    internal bool IsUsable(int slot) => WildBattleRules.IsCalledMoveSelectable(catalog, frame.Get(actor).Body!, slot) &&
        !frame.Slots.Where(c => c.Slot.Side != actor.Side && frame.CanAct(c.Slot)).Any(c => c.Body!.Imprison &&
            slot < frame.Get(actor).Body!.Pokemon.Moves.Length && c.Body.Pokemon.Moves.Any(m => m.Move == frame.Get(actor).Body!.Pokemon.Moves[slot].Move));
}
