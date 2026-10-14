using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignPowerPlantReceipt(PowerPlantWildOrigin Origin, RngState RngBefore,
    RngState RngAfter, PokemonInstance InitialPokemon);
public sealed record CampaignPowerPlantPreparation(CampaignPowerPlantReceipt Receipt, Route1WildPokemonResult Generated);
public sealed record CampaignPowerPlantReturn(PowerPlantEncounterKind Kind, int Outcome);

public sealed partial class CampaignSession
{
    private const string PowerPlantMap = "map.power-plant";
    private const string PowerPlantSource = "data/maps/PowerPlant/scripts.inc";
    public bool PowerPlantEnabled => _content.PowerPlantEnabled;
    private sealed record PowerPlantStatic(string Caller, string Fought, string Hidden, int LocalId, string Species);
    private static PowerPlantStatic PowerPlantEvent(PowerPlantEncounterKind kind) => kind switch
    {
        PowerPlantEncounterKind.Zapdos => new("PowerPlant_EventScript_Zapdos", "FLAG_FOUGHT_ZAPDOS", "FLAG_HIDE_ZAPDOS", 6, "SPECIES_ZAPDOS"),
        PowerPlantEncounterKind.Electrode1 => new("PowerPlant_EventScript_Electrode1", "FLAG_FOUGHT_POWER_PLANT_ELECTRODE_1", "FLAG_HIDE_POWER_PLANT_ELECTRODE_1", 8, "SPECIES_ELECTRODE"),
        PowerPlantEncounterKind.Electrode2 => new("PowerPlant_EventScript_Electrode2", "FLAG_FOUGHT_POWER_PLANT_ELECTRODE_2", "FLAG_HIDE_POWER_PLANT_ELECTRODE_2", 7, "SPECIES_ELECTRODE"),
        _ => throw new InvalidDataException("power_plant_static_kind")
    };
    private static readonly PowerPlantEncounterKind[] PowerPlantStatics =
        [PowerPlantEncounterKind.Zapdos, PowerPlantEncounterKind.Electrode1, PowerPlantEncounterKind.Electrode2];
    private static bool IsPowerPlantStaticBattle(CampaignWildBattle battle) =>
        battle.Frame.WildEncounter?.PowerPlant is { Kind: not PowerPlantEncounterKind.Land };
    private bool SeenByPowerPlant(CampaignState state, int species) =>
        state.Exploration?.Battles.Any(b => b.SourceSpeciesId == species && IsPowerPlantHistory(b)) == true;

    private static void ValidatePowerPlantCatalog(ICampaignCatalog content)
    {
        if (!content.PowerPlantEnabled || content.PowerPlantOwnership is not { } owned ||
            content.PowerPlantOpponents is not { } opponents || content.PowerPlantMachines is not { } machines ||
            content.PowerPlantShop is not { } shop ||
            !ReferenceEquals(PowerPlantOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(owned.Predecessor, content.EventIslandOwnership) ||
            !ReferenceEquals(owned, content.FightingDojoOwnership?.Predecessor) ||
            !ReferenceEquals(PowerPlantOpponentPokemonCatalog.For(owned), opponents) ||
            !ReferenceEquals(opponents.Predecessor, content.EventIslandOpponents) ||
            !ReferenceEquals(Game.Domain.Items.PowerPlantMachineCatalog.For(machines.Catalog), machines) ||
            !ReferenceEquals(machines.Predecessor, content.EventIslandMachines) || !ReferenceEquals(machines.Pokemon, owned) ||
            !ReferenceEquals(content.FossilLabShop?.Predecessor.Predecessor, shop) ||
            !ReferenceEquals(shop.Predecessor, content.EventIslandShop)) throw new InvalidDataException("power_plant_current_binding");
        var pickups = content.FieldPickups.Where(p => p.MapSymbol == "MAP_POWER_PLANT").ToArray();
        if (pickups.Length != 7 || pickups.Count(p => p.Hidden) != 2 ||
            !pickups.Where(p => !p.Hidden).Select(p => p.ItemSymbol).Order().SequenceEqual(new[] { "ITEM_MAX_POTION", "ITEM_TM17", "ITEM_TM25", "ITEM_THUNDER_STONE", "ITEM_ELIXIR" }.Order()) ||
            !pickups.Where(p => p.Hidden).Select(p => p.ItemSymbol).Order().SequenceEqual(new[] { "ITEM_MAX_ELIXIR", "ITEM_THUNDER_STONE" }.Order()))
            throw new InvalidDataException("power_plant_pickups_source");
        var definition = content.MapDefinition(new(PowerPlantMap));
        if (definition.Programs.OnResume != "PowerPlant_OnResume" || definition.Programs.OnTransition != "PowerPlant_OnTransition")
            throw new InvalidDataException("power_plant_map_callbacks");
        foreach (var kind in PowerPlantStatics)
        {
            var source = PowerPlantEvent(kind); var program = content.FindProgram(source.Caller);
            if (program is null || program.Instructions.Count(i => i.Source.Path == PowerPlantSource && i.Opcode == "setwildbattle" &&
                    i.Args.SequenceEqual(new[] { source.Species, PowerPlantWildBattleContract.Level(kind).ToString(System.Globalization.CultureInfo.InvariantCulture) })) != 1 ||
                program.Instructions.Count(i => i.Op == StoryOp.TrainerBattle && (kind == PowerPlantEncounterKind.Zapdos
                    ? i.Opcode == "special" && i.Args.SequenceEqual(new[] { "StartLegendaryBattle" }) : i.Opcode == "dowildbattle")) != 1 ||
                !definition.Actors.Any(a => a.SourceLocalId == source.LocalId && a.ScriptId == source.Caller && a.HideFlag == source.Hidden))
                throw new InvalidDataException("power_plant_static_script_binding");
        }
    }

    private void RequirePowerPlantCaller(CampaignState state, StoryEffect effect, PowerPlantEncounterKind kind)
    {
        var source = PowerPlantEvent(kind);
        if (!_content.PowerPlantEnabled || state.World.Player.MapId.Value != PowerPlantMap ||
            effect.Source.Path != PowerPlantSource || effect.Source.Symbol != source.Caller ||
            state.LastTalked is not { } id || !state.Actors.TryGetValue(id, out var actor) ||
            actor.SourceLocalId != source.LocalId || actor.ScriptId != source.Caller ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != source.LocalId)
            throw new InvalidDataException("power_plant_static_caller");
    }

    private bool TryApplyPowerPlantEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.PowerPlantEnabled || effect.Source.Path != PowerPlantSource) return false;
        var a = effect.Args;
        if (effect.Opcode == "specialvar" && a.SequenceEqual(new[] { "VAR_RESULT", "GetBattleOutcome" }))
        {
            if (state.World.Player.MapId.Value != PowerPlantMap || state.PowerPlantReturn is not { } result ||
                !(effect.Source.Symbol == "PowerPlant_EventScript_TryRemoveStaticMon" || effect.Source.Symbol == PowerPlantEvent(result.Kind).Caller))
                throw new InvalidDataException("power_plant_outcome_context");
            bool caller = effect.Source.Symbol == PowerPlantEvent(result.Kind).Caller;
            next = state with { PowerPlantReturn = caller ? null : result,
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result.Outcome) } };
            return true;
        }
        var matches = PowerPlantStatics.Where(k => PowerPlantEvent(k).Caller == effect.Source.Symbol).ToArray();
        if (matches.Length != 1) return false;
        var kind = matches[0]; var source = PowerPlantEvent(kind);
        if (effect.Opcode == "special" && a.SequenceEqual(new[] { "QuestLog_CutRecording" }))
        { RequirePowerPlantCaller(state, effect, kind); return true; }
        if (effect.Opcode is "playmoncry" or "waitbuttonpress")
        {
            RequirePowerPlantCaller(state, effect, kind);
            if (state.PowerPlantPreparation?.Receipt.Origin.Kind != kind ||
                (effect.Opcode == "playmoncry" ? !a.SequenceEqual(new[] { source.Species, "CRY_MODE_ENCOUNTER" }) : a.Length != 0))
                throw new InvalidDataException("power_plant_encounter_presentation");
            return true;
        }
        if (effect.Opcode == "setwildbattle")
        {
            RequirePowerPlantCaller(state, effect, kind);
            if (!a.SequenceEqual(new[] { source.Species, PowerPlantWildBattleContract.Level(kind).ToString(System.Globalization.CultureInfo.InvariantCulture) }) ||
                state.PowerPlantPreparation is not null || state.PowerPlantReturn is not null || !state.Script.Locked ||
                state.Script.Flags.GetValueOrDefault(source.Fought) || state.Script.Flags.GetValueOrDefault(source.Hidden) ||
                state.Script.Flags.GetValueOrDefault(SpecialWildFlag)) throw new InvalidDataException("power_plant_preparation_context");
            var made = PowerPlantPokemonFactory.GenerateStatic(_content.PowerPlantOwnership!, kind, state.World.Rng, state.TrainerId);
            next = state with { PowerPlantPreparation = new(new(PowerPlantWildBattleContract.Origin(kind), state.World.Rng,
                made.GlobalRng, made.Pokemon), made), World = state.World with { Rng = made.GlobalRng } };
            return true;
        }
        if (effect.Opcode == "dowildbattle" || effect.Opcode == "special" && a.SequenceEqual(new[] { "StartLegendaryBattle" }))
        {
            RequirePowerPlantCaller(state, effect, kind);
            if ((kind == PowerPlantEncounterKind.Zapdos ? effect.Opcode != "special" : effect.Opcode != "dowildbattle" || a.Length != 0) ||
                state.PowerPlantPreparation is not { } pending || pending.Receipt.Origin != PowerPlantWildBattleContract.Origin(kind) ||
                state.Script.Wait?.Kind != StoryWaitKind.Battle || !state.Script.Flags.GetValueOrDefault(SpecialWildFlag) ||
                state.World.Rng != pending.Generated.GlobalRng) throw new InvalidDataException("power_plant_battle_start_context");
            next = BeginWildBattle(state with { PowerPlantPreparation = null }, scriptedPokemon: pending.Generated,
                powerPlantOrigin: pending.Receipt.Origin, powerPlantReceipt: pending.Receipt);
            return true;
        }
        return false;
    }

    private bool PowerPlantButtonWait(CampaignState state) => _content.PowerPlantEnabled &&
        state.World.Player.MapId.Value == PowerPlantMap && state.Script.Wait?.Kind == StoryWaitKind.ButtonPress &&
        !state.Script.Stack.IsEmpty && state.Script.Stack[^1].ProgramId == "PowerPlant_EventScript_Zapdos";
    private Decision<CampaignState> DecidePowerPlantButton(CampaignState state, CampaignCommand command)
    {
        if (!PowerPlantButtonWait(state)) return new(CommandStatus.Rejected, state, "power_plant_button_not_pending");
        if (command.Text is not ("A" or "B")) return new(CommandStatus.Rejected, state, "button_press_requires_a_or_b");
        var next = state with { PresentationEffects = [], PresentationTicks = 0, Motions = [] };
        return new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, next.Script.Wait!.CorrelationId, _content))));
    }

    private CampaignState ReturnFromPowerPlantBattle(CampaignState state, CampaignWildBattle battle)
    {
        var receipt = battle.PowerPlantReceipt ?? throw new InvalidDataException("power_plant_return_receipt");
        var source = PowerPlantEvent(receipt.Origin.Kind);
        if (state.World.Player.MapId.Value != PowerPlantMap || state.Script.Wait is not { Kind: StoryWaitKind.Battle } wait ||
            !state.Script.Flags.GetValueOrDefault(SpecialWildFlag)) throw new InvalidDataException("power_plant_return_context");
        var caller = state.Script;
        var next = state with { WildBattle = null, Dialogue = null, Music = FieldTravelMusic(state),
            Exploration = ResetEncounterModifiers(state.Exploration!), PowerPlantReturn = new(receipt.Origin.Kind, SnorlaxSourceOutcome(battle)),
            Script = caller with { Stack = [], Wait = null, Completed = true } };
        next = StartStory(next, "PowerPlant_OnResume");
        for (int i = 0; i < 8 && next.Script.Wait is { Kind: StoryWaitKind.NativeEffect } effect; i++)
            next = Advance(next, StoryRules.Continue(next.Script, effect.CorrelationId, _content));
        if (!next.Script.Completed || next.Script.Wait is not null || !next.Script.Stack.IsEmpty ||
            battle.Frame.Outcome == WildBattleOutcome.Caught && next.Actors.Values.Any(a => a.SourceLocalId == source.LocalId && a.Active))
            throw new InvalidDataException("power_plant_resume_incomplete");
        next = next with { Script = next.Script with { Stack = caller.Stack, Wait = caller.Wait, Completed = caller.Completed, Locked = caller.Locked } };
        return Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content)));
    }

    private bool IsPowerPlantHistory(CampaignWildHistory history)
    {
        if (!_content.PowerPlantEnabled || history.PowerPlantReceipt is not { } receipt || receipt.Origin is not { } origin ||
            !Enum.IsDefined(origin.Kind) || history.EventIslandReceipt is not null || history.SevenIsland is not null || history.SevenIslandReceipt is not null ||
            history.CeruleanCaveOrigin is not null || history.MewtwoReceipt is not null || history.CelioRoamer is not null || history.FiveIsland is not null ||
            history.SixIsland is not null || history.FourIsland is not null || history.SeviiRuby is not null || history.VictoryRoad is not null ||
            history.LostelleHypno is not null || history.ScriptedSnorlaxRoute != 0 || history.ScriptedMarowak || history.Safari ||
            history.Outcome == WildBattleOutcome.Ongoing || history.Outcome != WildBattleOutcome.Won && history.ExperienceGained != 0 ||
            receipt.InitialPokemon is not { } initial || initial.SourceSpeciesId != history.SourceSpeciesId || initial.Level != history.Level ||
            receipt.RngBefore.AlgorithmId != Gen3Rng.AlgorithmId || receipt.RngAfter.AlgorithmId != Gen3Rng.AlgorithmId ||
            receipt.RngBefore.Cursor >= receipt.RngAfter.Cursor || !PowerPlantPokemonFactory.SourceInitialInstance(_content.PowerPlantOwnership!, origin, initial)) return false;
        try
        {
            var made = origin.Kind == PowerPlantEncounterKind.Land
                ? PowerPlantPokemonFactory.GenerateSourceSlot(_content.PowerPlantOwnership!, origin, checked((byte)initial.Level), receipt.RngBefore, initial.OriginalTrainerId)
                : PowerPlantPokemonFactory.GenerateStatic(_content.PowerPlantOwnership!, origin.Kind, receipt.RngBefore, initial.OriginalTrainerId);
            return made.GlobalRng == receipt.RngAfter && SameSourceOwnedPokemon(made.Pokemon, initial);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException) { return false; }
    }

    private bool IsPowerPlantCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.SingleOrDefault(b => b.Id == capture.BattleId);
        if (capture.PowerPlant is not { } origin || history is null || !IsPowerPlantHistory(history) ||
            history.PowerPlantReceipt!.Origin != origin || history.Outcome != WildBattleOutcome.Caught ||
            capture.EventIsland is not null || capture.SevenIsland is not null || capture.CeruleanCaveOrigin is not null ||
            capture.CelioRoamer is not null || capture.FiveIsland is not null || capture.SixIsland is not null || capture.FourIsland is not null ||
            capture.SourceSpeciesId != history.SourceSpeciesId || capture.MetLevel != history.Level ||
            capture.MetLocation != _content.MapDefinition(origin.Map).RegionMapSection) return false;
        var initial = history.PowerPlantReceipt.InitialPokemon;
        var owned = HistoricalPokemon(state).SingleOrDefault(p => p.InstanceId == capture.InstanceId);
        return owned is not null && !owned.ModernFatefulEncounter && initial.OriginalTrainerId == state.TrainerId &&
            capture.Personality == initial.Personality && capture.Ivs == initial.Ivs &&
            owned.Pokemon.OriginalTrainerId == initial.OriginalTrainerId && owned.Pokemon.Nature == initial.Nature &&
            owned.Pokemon.Gender == initial.Gender;
    }

    private string? ValidatePowerPlantProgress(CampaignState state)
    {
        if (state.PowerPlantPreparation is not null || state.PowerPlantReturn is not null) return "save_power_plant_event_active";
        var histories = (state.Exploration?.Battles ?? []).Where(b => b.PowerPlantReceipt is not null).ToArray();
        if (histories.Any(b => !IsPowerPlantHistory(b) || b.PowerPlantReceipt!.InitialPokemon.OriginalTrainerId != state.TrainerId ||
            b.PowerPlantReceipt.RngAfter.Cursor > state.World.Rng.Cursor)) return "save_power_plant_history";
        foreach (var kind in PowerPlantStatics)
        {
            var source = PowerPlantEvent(kind); var attempts = histories.Where(b => b.PowerPlantReceipt!.Origin.Kind == kind).ToArray();
            bool fought = state.Script.Flags.GetValueOrDefault(source.Fought), hidden = state.Script.Flags.GetValueOrDefault(source.Hidden);
            bool Terminal(CampaignWildHistory b) => b.Outcome is WildBattleOutcome.Won or WildBattleOutcome.Caught ||
                kind != PowerPlantEncounterKind.Zapdos && b.Outcome == WildBattleOutcome.Escaped;
            var terminal = attempts.Where(Terminal).ToArray();
            if (terminal.Length > 1 || fought != (terminal.Length == 1) || fought && (!hidden || attempts[^1] != terminal[0]))
                return "save_power_plant_completion";
            if (hidden && !fought && !(kind == PowerPlantEncounterKind.Zapdos && attempts.LastOrDefault()?.Outcome == WildBattleOutcome.Escaped))
                return "save_power_plant_hide_receipt";
            if (state.World.Player.MapId.Value == PowerPlantMap && state.Actors.Values.Any(a => a.SourceLocalId == source.LocalId && hidden && a.Active))
                return "save_power_plant_actor";
        }
        return null;
    }
}
