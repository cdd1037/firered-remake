using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The source-created individual, before battle-entry RNG, held items or damage.</summary>
public sealed record CampaignSeafoamReceipt(SeafoamWildOrigin Origin, RngState RngBefore,
    RngState RngAfter, PokemonInstance InitialPokemon);
public sealed record CampaignArticunoPreparation(CampaignSeafoamReceipt Receipt, Route1WildPokemonResult Generated);

public sealed partial class CampaignSession
{
    private const string ArticunoMap = "map.seafoam-islands-b4f";
    private const string ArticunoSource = "data/maps/SeafoamIslands_B4F/scripts.inc";
    private const string ArticunoScript = "SeafoamIslands_B4F_EventScript_Articuno";
    private const string ArticunoResume = "SeafoamIslands_B4F_OnResume";
    private const string ArticunoRemove = "SeafoamIslands_B4F_EventScript_TryRemoveArticuno";
    private const string ArticunoHide = "FLAG_HIDE_ARTICUNO";
    private const string ArticunoFought = "FLAG_FOUGHT_ARTICUNO";
    private const int ArticunoLocalId = 3;
    public bool SeafoamEnabled => _content.SeafoamEnabled;
    private static bool IsSeafoamMap(MapId map) => SeafoamEncounterTables.Land.Any(t => t.Map == map) || SeafoamEncounterTables.Water.Any(t => t.Map == map);
    private static bool IsArticunoBattle(CampaignWildBattle battle) =>
        battle.Frame.WildEncounter?.Seafoam?.Kind == SeafoamEncounterKind.Articuno;

    private static void ValidateSeafoamEncounterCatalog(ICampaignCatalog content)
    {
        if (!content.SeafoamEnabled || content.SeafoamOwnership is not { } owned ||
            !ReferenceEquals(owned, content.PokemonContent?.Pokemon))
            throw new InvalidDataException("seafoam_current_binding");
        var definition = content.MapDefinition(new(ArticunoMap));
        var program = content.FindProgram(ArticunoScript);
        if (definition.Programs.OnResume != ArticunoResume ||
            !definition.Actors.Any(a => a.SourceLocalId == ArticunoLocalId && a.ScriptId == ArticunoScript && a.HideFlag == ArticunoHide) ||
            program is null || program.Instructions.Count(i => i.Source.Path == ArticunoSource &&
                i.Opcode == "setwildbattle" && i.Op == StoryOp.Special && i.Args.SequenceEqual(new[] { "SPECIES_ARTICUNO", "50" })) != 1 ||
            program.Instructions.Count(i => i.Source.Path == ArticunoSource && i.Opcode == "special" &&
                i.Op == StoryOp.TrainerBattle && i.Args.SequenceEqual(new[] { "StartLegendaryBattle" })) != 1)
            throw new InvalidDataException("seafoam_articuno_script_binding");
    }

    private EarlyLandWildPokemonResult GenerateSeafoamLand(CampaignState state, EarlyLandEncounterState encounter)
    {
        var candidate = encounter.Pending?.Candidate ?? throw new InvalidDataException("seafoam_land_candidate_missing");
        var origin = new SeafoamWildOrigin(candidate.Map, SeafoamEncounterKind.Land, candidate.Slot);
        var made = SeafoamPokemonFactory.GenerateSourceSlot(_content.SeafoamOwnership!, origin,
            candidate.Level, state.World.Rng, state.TrainerId);
        if (made.Pokemon.Species != candidate.Species) throw new InvalidDataException("seafoam_land_candidate_species");
        return new(made.Pokemon, made.GlobalRng, made.RandomTrace);
    }

    private CampaignSeafoamReceipt? PrepareSeafoamReceipt(CampaignState state, Route1WildPokemonResult made,
        SeafoamWildOrigin? origin, CampaignSeafoamReceipt? receipt, RngState? generationRng,
        bool scripted, bool surfing, bool fishing, FishingEncounterOrigin? fishingOrigin = null)
    {
        if (origin is null)
        {
            // Super Rod retains its exact ordinary typed slot, including on Seafoam maps.
            // Every other Seafoam entry still requires its existing generation receipt.
            bool typedSuperRod = !scripted && !surfing && fishing && fishingOrigin is { Rod: FishingRod.Super } &&
                fishingOrigin.Map == state.World.Player.MapId && IsTypedFishingOrigin(fishingOrigin) &&
                FishingBattleContract.Matches(fishingOrigin, made.Pokemon);
            if (receipt is not null || IsSeafoamMap(state.World.Player.MapId) && !typedSuperRod)
                throw new InvalidDataException("seafoam_wild_missing_origin");
            return null;
        }
        bool kindMatches = scripted ? origin.Kind == SeafoamEncounterKind.Articuno : surfing ? origin.Kind == SeafoamEncounterKind.Surf :
            fishing ? origin.Kind is SeafoamEncounterKind.OldRod or SeafoamEncounterKind.GoodRod : origin.Kind == SeafoamEncounterKind.Land;
        if (!_content.SeafoamEnabled || origin.Map != state.World.Player.MapId || !kindMatches ||
            !SeafoamPokemonFactory.SourceInitialInstance(_content.SeafoamOwnership!, origin, made.Pokemon))
            throw new InvalidDataException("seafoam_wild_field_origin");
        if (scripted)
        {
            // setwildbattle has already consumed creation RNG. Reuse its exact individual.
            if (receipt is null || receipt.Origin != origin || receipt.RngAfter != made.GlobalRng ||
                state.World.Rng != receipt.RngAfter || !SameSourceOwnedPokemon(receipt.InitialPokemon, made.Pokemon))
                throw new InvalidDataException("seafoam_static_creation_receipt");
            return receipt;
        }
        if (receipt is not null) throw new InvalidDataException("seafoam_ordinary_preset_receipt");
        var before = generationRng ?? state.World.Rng;
        if (before.AlgorithmId != Gen3Rng.AlgorithmId || made.GlobalRng.AlgorithmId != Gen3Rng.AlgorithmId ||
            before.Cursor >= made.GlobalRng.Cursor || made.Pokemon.OriginalTrainerId != state.TrainerId)
            throw new InvalidDataException("seafoam_creation_rng");
        return new(origin, before, made.GlobalRng, made.Pokemon);
    }

    private void RequireArticunoCaller(CampaignState state, StoryEffect effect)
    {
        if (!_content.SeafoamEnabled || state.World.Player.MapId.Value != ArticunoMap ||
            effect.Source.Path != ArticunoSource || effect.Source.Symbol != ArticunoScript ||
            state.LastTalked is not { } id || !state.Actors.TryGetValue(id, out var actor) ||
            actor.SourceLocalId != ArticunoLocalId || actor.ScriptId != ArticunoScript || !actor.Active ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != ArticunoLocalId)
            throw new InvalidDataException("articuno_script_context");
    }

    private bool TryApplyArticunoEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.SeafoamEnabled || effect.Source.Path != ArticunoSource) return false;
        var args = effect.Args;
        bool caller = effect.Source.Symbol == ArticunoScript;
        if (effect.Opcode == "specialvar" && args.SequenceEqual(new[] { "VAR_RESULT", "GetBattleOutcome" }))
        {
            if (state.World.Player.MapId.Value != ArticunoMap || state.ArticunoReturn is not { } outcome ||
                effect.Source.Symbol is not (ArticunoScript or ArticunoRemove))
                throw new InvalidDataException("articuno_outcome_context");
            // OnResume and the suspended script each read the same gBattleOutcome.
            next = state with { ArticunoReturn = caller ? null : outcome,
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", outcome) } };
            return true;
        }
        if (!caller) return false;
        if (effect.Opcode == "special" && args.SequenceEqual(new[] { "QuestLog_CutRecording" }))
        { RequireArticunoCaller(state, effect); return true; }
        if (effect.Opcode is "playmoncry" or "waitbuttonpress")
        {
            RequireArticunoCaller(state, effect);
            if (state.ArticunoPreparation?.Receipt.Origin != SeafoamWildBattleContract.ArticunoOrigin ||
                (effect.Opcode == "playmoncry" ? !args.SequenceEqual(new[] { "SPECIES_ARTICUNO", "CRY_MODE_ENCOUNTER" }) : args.Length != 0))
                throw new InvalidDataException("articuno_encounter_presentation");
            return true;
        }
        if (effect.Opcode == "setwildbattle")
        {
            RequireArticunoCaller(state, effect);
            if (!args.SequenceEqual(new[] { "SPECIES_ARTICUNO", "50" }) || !state.Script.Locked ||
                state.ArticunoPreparation is not null || state.ArticunoReturn is not null ||
                state.Script.Flags.GetValueOrDefault(ArticunoFought) || state.Script.Flags.GetValueOrDefault(ArticunoHide) ||
                state.Script.Flags.GetValueOrDefault(SpecialWildFlag)) throw new InvalidDataException("articuno_preparation_context");
            var made = SeafoamPokemonFactory.GenerateArticuno(_content.SeafoamOwnership!, state.World.Rng, state.TrainerId);
            next = state with { ArticunoPreparation = new(new(SeafoamWildBattleContract.ArticunoOrigin,
                state.World.Rng, made.GlobalRng, made.Pokemon), made), World = state.World with { Rng = made.GlobalRng } };
            return true;
        }
        if (effect.Opcode == "special" && args.SequenceEqual(new[] { "StartLegendaryBattle" }))
        {
            RequireArticunoCaller(state, effect);
            if (state.ArticunoPreparation is not { } pending || pending.Receipt.Origin != SeafoamWildBattleContract.ArticunoOrigin ||
                state.Script.Wait?.Kind != StoryWaitKind.Battle || !state.Script.Flags.GetValueOrDefault(SpecialWildFlag) ||
                state.World.Rng != pending.Generated.GlobalRng || !IsSeafoamReceipt(pending.Receipt))
                throw new InvalidDataException("articuno_battle_start_context");
            next = BeginWildBattle(state with { ArticunoPreparation = null }, scriptedPokemon: pending.Generated,
                seafoamOrigin: pending.Receipt.Origin, seafoamReceipt: pending.Receipt);
            return true;
        }
        return false;
    }

    private bool ArticunoButtonWait(CampaignState state) => _content.SeafoamEnabled &&
        state.World.Player.MapId.Value == ArticunoMap && state.Script.Wait?.Kind == StoryWaitKind.ButtonPress &&
        !state.Script.Stack.IsEmpty && state.Script.Stack[^1].ProgramId == ArticunoScript;
    private Decision<CampaignState> DecideArticunoButton(CampaignState state, CampaignCommand command)
    {
        if (!ArticunoButtonWait(state)) return new(CommandStatus.Rejected, state, "articuno_button_not_pending");
        if (command.Text is not ("A" or "B")) return new(CommandStatus.Rejected, state, "button_press_requires_a_or_b");
        var next = state with { PresentationEffects = [], PresentationTicks = 0, Motions = [] };
        return new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, next.Script.Wait!.CorrelationId, _content))));
    }

    private CampaignState ReturnFromArticunoBattle(CampaignState state, CampaignWildBattle battle)
    {
        if (state.World.Player.MapId.Value != ArticunoMap || state.Script.Wait is not { Kind: StoryWaitKind.Battle } wait ||
            !state.Script.Flags.GetValueOrDefault(SpecialWildFlag) || battle.SeafoamReceipt is not { } receipt ||
            receipt.Origin != SeafoamWildBattleContract.ArticunoOrigin || !IsSeafoamReceipt(receipt) ||
            battle.Frame.Outcome is not (WildBattleOutcome.Won or WildBattleOutcome.Caught or WildBattleOutcome.Escaped))
            throw new InvalidDataException("articuno_return_context");
        var caller = state.Script;
        var next = state with { WildBattle = null, Dialogue = null, Music = FieldTravelMusic(state),
            Exploration = ResetEncounterModifiers(state.Exploration!), ArticunoReturn = SnorlaxSourceOutcome(battle),
            Script = caller with { Stack = [], Wait = null, Completed = true } };
        if (_content.MapDefinition(state.World.Player.MapId).Programs.OnResume != ArticunoResume)
            throw new InvalidDataException("articuno_resume_source");
        next = StartStory(next, ArticunoResume);
        for (int i = 0; i < 8 && next.Script.Wait is { Kind: StoryWaitKind.NativeEffect } effect; i++)
            next = Advance(next, StoryRules.Continue(next.Script, effect.CorrelationId, _content));
        if (!next.Script.Completed || next.Script.Wait is not null || !next.Script.Stack.IsEmpty ||
            battle.Frame.Outcome == WildBattleOutcome.Caught && next.Actors.Values.Any(a => a.SourceLocalId == ArticunoLocalId && a.Active))
            throw new InvalidDataException("articuno_resume_incomplete");
        next = next with { Script = next.Script with { Stack = caller.Stack, Wait = caller.Wait, Completed = caller.Completed, Locked = caller.Locked } };
        return Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content)));
    }

    private bool IsSeafoamReceipt(CampaignSeafoamReceipt receipt)
    {
        if (!_content.SeafoamEnabled || receipt.Origin is not { } origin ||
            !SeafoamWildBattleContract.IsRuntimeKind(origin.Kind) || receipt.InitialPokemon is not { } initial ||
            receipt.RngBefore.AlgorithmId != Gen3Rng.AlgorithmId || receipt.RngAfter.AlgorithmId != Gen3Rng.AlgorithmId ||
            receipt.RngBefore.Cursor >= receipt.RngAfter.Cursor) return false;
        try
        {
            if (!SeafoamPokemonFactory.SourceInitialInstance(_content.SeafoamOwnership!, origin, initial)) return false;
            var made = origin.Kind == SeafoamEncounterKind.Articuno
                ? SeafoamPokemonFactory.GenerateArticuno(_content.SeafoamOwnership!, receipt.RngBefore, initial.OriginalTrainerId)
                : SeafoamPokemonFactory.GenerateSourceSlot(_content.SeafoamOwnership!, origin, checked((byte)initial.Level),
                    receipt.RngBefore, initial.OriginalTrainerId);
            return made.GlobalRng == receipt.RngAfter && SameSourceOwnedPokemon(made.Pokemon, initial);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        { return false; }
    }

    private bool IsSeafoamHistory(CampaignWildHistory history) => history.SeafoamReceipt is { } receipt &&
        history.Fishing is null && history.PowerPlantReceipt is null && history.EventIslandReceipt is null &&
        history.SevenIsland is null && history.SevenIslandReceipt is null && history.CeruleanCaveOrigin is null &&
        history.MewtwoReceipt is null && history.CelioRoamer is null && history.FiveIsland is null && history.SixIsland is null &&
        history.FourIsland is null && history.SeviiRuby is null && history.VictoryRoad is null && history.LostelleHypno is null &&
        history.ScriptedSnorlaxRoute == 0 && !history.ScriptedMarowak && !history.Safari && Enum.IsDefined(history.Outcome) &&
        history.Outcome != WildBattleOutcome.Ongoing && history.ExperienceGained >= 0 &&
        (history.Outcome == WildBattleOutcome.Won || history.ExperienceGained == 0) && IsSeafoamReceipt(receipt) &&
        receipt.InitialPokemon.SourceSpeciesId == history.SourceSpeciesId && receipt.InitialPokemon.Level == history.Level;

    private bool IsSeafoamCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.FirstOrDefault(b => b.Id == capture.BattleId);
        if (capture.Seafoam is not { } origin || history is null || !IsSeafoamHistory(history) ||
            history.SeafoamReceipt!.Origin != origin || history.Outcome != WildBattleOutcome.Caught ||
            capture.Fishing is not null || capture.PowerPlant is not null || capture.EventIsland is not null ||
            capture.SevenIsland is not null || capture.CeruleanCaveOrigin is not null || capture.CelioRoamer is not null ||
            capture.FiveIsland is not null || capture.SixIsland is not null || capture.FourIsland is not null ||
            capture.SourceSpeciesId != history.SourceSpeciesId || capture.MetLevel != history.Level ||
            capture.MetLocation != _content.MapDefinition(origin.Map).RegionMapSection) return false;
        var initial = history.SeafoamReceipt.InitialPokemon;
        var owned = OwnedPokemon(state).FirstOrDefault(p => p.InstanceId == capture.InstanceId);
        // Evolution/training may change species/stats/moves after capture; original identity must survive.
        return owned is not null && !owned.ModernFatefulEncounter && initial.OriginalTrainerId == state.TrainerId &&
            capture.Personality == initial.Personality && capture.Ivs == initial.Ivs &&
            owned.Pokemon.OriginalTrainerId == initial.OriginalTrainerId && owned.Pokemon.Personality == initial.Personality &&
            owned.Pokemon.Ivs == initial.Ivs && owned.Pokemon.Nature == initial.Nature && owned.Pokemon.Gender == initial.Gender;
    }

    private string? ValidateSeafoamEncounterProgress(CampaignState state)
    {
        if (state.ArticunoPreparation is not null || state.ArticunoReturn is not null) return "save_articuno_event_active";
        var histories = (state.Exploration?.Battles ?? []).Where(b => b.SeafoamReceipt is not null).ToArray();
        if (histories.Any(b => !IsSeafoamHistory(b) || b.SeafoamReceipt!.InitialPokemon.OriginalTrainerId != state.TrainerId ||
            b.SeafoamReceipt.RngAfter.Cursor > state.World.Rng.Cursor)) return "save_seafoam_history";
        for (int i = 1; i < histories.Length; i++)
            if (histories[i].SeafoamReceipt!.RngBefore.Cursor < histories[i - 1].SeafoamReceipt!.RngAfter.Cursor)
                return "save_seafoam_creation_order";
        var attempts = histories.Where(b => b.SeafoamReceipt!.Origin.Kind == SeafoamEncounterKind.Articuno).ToArray();
        var terminal = attempts.Where(b => b.Outcome is WildBattleOutcome.Won or WildBattleOutcome.Caught).ToArray();
        bool fought = state.Script.Flags.GetValueOrDefault(ArticunoFought), hidden = state.Script.Flags.GetValueOrDefault(ArticunoHide);
        if (terminal.Length > 1 || fought != (terminal.Length == 1) || fought && (!hidden || attempts[^1] != terminal[0]))
            return "save_articuno_completion";
        if (hidden && !fought && attempts.LastOrDefault()?.Outcome != WildBattleOutcome.Escaped)
            return "save_articuno_hide_receipt";
        // Off-camera source objects may be inactive without a hide flag. Only a
        // hidden-but-live object contradicts the persistent event outcome.
        if (state.World.Player.MapId.Value == ArticunoMap && hidden &&
            state.Actors.Values.Any(a => a.SourceLocalId == ArticunoLocalId && a.ScriptId == ArticunoScript && a.Active))
            return "save_articuno_actor";
        return null;
    }
}
