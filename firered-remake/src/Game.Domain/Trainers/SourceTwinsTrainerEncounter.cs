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
        bool attackBadgeBoost = false, bool defenseBadgeBoost = false, bool speedBadgeBoost = false, OwnedBattlePokemonCatalog? successorOwned = null)
    {
        SourceTwinsEncounterResult Fail(string code) => new(null, rng, [], [], code);
        if (ValidateDefinition(definition) is { } error) return Fail(error);
        if (SourceMechanicsCatalog.For(catalog) is not { } proof) return Fail("source_twins_catalog_proof");
        if (successorOwned is not null && (!ReferenceEquals(OwnedBattlePokemonCatalog.For(successorOwned.Catalog),successorOwned) ||
            !ReferenceEquals(successorOwned.Mechanics,proof) ||
            !(CeruleanCaveOwnedPokemonCatalog.For(successorOwned.Catalog) is {} cave && CeruleanCaveOpponentPokemonCatalog.For(cave) is not null ||
              SeviiRubyOwnedPokemonCatalog.For(successorOwned.Catalog) is {} ruby && SeviiRubyOpponentPokemonCatalog.For(ruby) is not null ||
              FourIslandOwnedPokemonCatalog.For(successorOwned.Catalog) is {} four && FourIslandOpponentPokemonCatalog.For(four) is not null ||
              SixIslandOwnedPokemonCatalog.For(successorOwned.Catalog) is {} six && SixIslandOpponentPokemonCatalog.For(six) is not null ||
              FiveIslandOwnedPokemonCatalog.For(successorOwned.Catalog) is {} five && FiveIslandOpponentPokemonCatalog.For(five) is not null ||
              CelioOwnedPokemonCatalog.For(successorOwned.Catalog) is {} celio && CelioOpponentPokemonCatalog.For(celio) is not null)))
            return Fail("source_twins_successor_owned");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fail("source_twins_rng");
        if (playerParty.IsDefault || playerParty.Length is < 2 or > 6) return Fail("source_twins_player_party");
        foreach (var member in playerParty)
            if (member?.Pokemon is null || successorOwned is not null && !successorOwned.Catalog.Species.ContainsKey(member.Pokemon.Species) || SourcePokemonValidator.ValidatePermanent(proof, member.Pokemon) is not null ||
                member.KnockedOffItem is not null || member.PersistentHeldItemOverride is not null ||
                !SourceFrameBerryItems.SupportsHeldItem(member.HeldItem, catalog) ||
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
                SourceMechanicsRulesVersion = SourceBattleKernel.Version, SourceCampaignRulesVersion = successorOwned?.TrainerOpponentProfile, BattleKind = WildBattleCalledBattleKind.Trainer,
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
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } capability)
            return new(frame, [], [], capability);
        if (!OriginalEnemyParty(frame)) return new(frame, [], [], "source_twins_ai_original_roster");
        var result = SourceFourTrainerAi.ChooseCore(catalog, frame, expectedRevision, 1, knownPlayerAbility, "twins");
        return new(result.State, result.Choices.Select(c => new SourceTwinsEnemyChoice(c.Actor, c.ScoredTarget,
            c.Selection, c.Scores, c.SimulatedDamagePercent, c.AbilityReads)).ToImmutableArray(), result.Trace, result.Fault);
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
