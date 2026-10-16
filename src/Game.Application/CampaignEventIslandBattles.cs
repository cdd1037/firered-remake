using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed record CampaignEventIslandReceipt(EventIslandWildOrigin Origin, RngState RngBefore,
    RngState RngAfter, PokemonInstance InitialPokemon, int HallEntries);
public sealed record CampaignEventIslandPreparation(CampaignEventIslandReceipt Receipt, Route1WildPokemonResult Generated);
public sealed record CampaignEventIslandReturn(EventIslandEncounterKind Kind, int Outcome);

public sealed partial class CampaignSession
{
    private sealed record EventLegendaryScript(string Source, string Caller, string Resume, string Remove,
        string Fought, string Hidden, string FlewAway, int LocalId, string Species);
    private static EventLegendaryScript EventLegendary(EventIslandEncounterKind kind) => kind switch
    {
        EventIslandEncounterKind.HoOh => new("data/maps/NavelRock_Summit/scripts.inc", "NavelRock_Summit_EventScript_HoOh",
            "NavelRock_Summit_OnResume", "NavelRock_Summit_EventScript_TryRemoveHoOh", "FLAG_FOUGHT_HO_OH", "FLAG_HIDE_HO_OH", "FLAG_HO_OH_FLEW_AWAY", 1, "SPECIES_HO_OH"),
        EventIslandEncounterKind.Lugia => new("data/maps/NavelRock_Base/scripts.inc", "NavelRock_Base_EventScript_Lugia",
            "NavelRock_Base_OnResume", "NavelRock_Base_EventScript_TryRemoveLugia", "FLAG_FOUGHT_LUGIA", "FLAG_HIDE_LUGIA", "FLAG_LUGIA_FLEW_AWAY", 1, "SPECIES_LUGIA"),
        EventIslandEncounterKind.Deoxys => new("data/maps/BirthIsland_Exterior/scripts.inc", "BirthIsland_Exterior_EventScript_Deoxys",
            "BirthIsland_Exterior_OnResume", "BirthIsland_Exterior_EventScript_TryRemoveDeoxys", "FLAG_FOUGHT_DEOXYS", "FLAG_HIDE_DEOXYS", "FLAG_DEOXYS_FLEW_AWAY", 2, "SPECIES_DEOXYS"),
        _ => throw new InvalidDataException("event_island_kind")
    };
    private static int EventIslandHallEntries(CampaignState s) => (s.PokemonLeague?.Resets ?? []).Count(r => r.Reason == "hall-of-fame");
    private static bool IsEventIslandBattle(CampaignWildBattle battle) => battle.Frame.WildEncounter?.EventIsland is not null;

    private bool TryApplyEventIslandBattleEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        var matching = Enum.GetValues<EventIslandEncounterKind>().Where(k => EventLegendary(k).Source == effect.Source.Path).ToArray();
        if (matching.Length != 1) return false;
        var kind = matching[0]; var source = EventLegendary(kind); var origin = EventIslandWildBattleContract.Origin(kind);
        bool caller = effect.Source.Symbol == source.Caller;
        if (effect.Opcode == "specialvar" && effect.Args.SequenceEqual(new[] { "VAR_RESULT", "GetBattleOutcome" }))
        {
            if (state.World.Player.MapId != origin.Map || state.EventIslandReturn is not { } returned || returned.Kind != kind ||
                !(caller || effect.Source.Symbol == source.Remove)) throw new InvalidDataException("event_island_outcome_context");
            next = state with { EventIslandReturn = caller ? null : returned,
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", returned.Outcome) } };
            return true;
        }
        if (!caller || !(effect.Opcode == "seteventmon" || effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "StartLegendaryBattle" }))) return false;
        if (state.World.Player.MapId != origin.Map || !state.Script.Locked ||
            state.Script.Flags.GetValueOrDefault(source.Fought) || state.Script.Flags.GetValueOrDefault(source.FlewAway) ||
            // Birth uses addobject while its persistent hide flag remains set; the
            // awakened source actor, rather than that map-entry flag, owns this event.
            kind != EventIslandEncounterKind.Deoxys && state.Script.Flags.GetValueOrDefault(source.Hidden) ||
            kind == EventIslandEncounterKind.Deoxys && !state.Script.Flags.GetValueOrDefault(Game.Domain.World.BirthIslandTriangleRules.AwakenedFlag) ||
            !state.Actors.Values.Any(a => a.SourceLocalId == source.LocalId && a.Active))
            throw new InvalidDataException("event_island_battle_caller");
        if (effect.Opcode == "seteventmon")
        {
            if (!effect.Args.SequenceEqual(new[] { source.Species, EventIslandWildBattleContract.Level(kind).ToString(System.Globalization.CultureInfo.InvariantCulture) }) ||
                state.EventIslandPreparation is not null || state.EventIslandReturn is not null || state.Script.Flags.GetValueOrDefault(SpecialWildFlag))
                throw new InvalidDataException("event_island_preparation_context");
            var generated = EventIslandPokemonFactory.Generate(_content.GamePokemon!, kind, state.World.Rng, state.TrainerId);
            next = state with { EventIslandPreparation = new(new(origin, state.World.Rng, generated.GlobalRng, generated.Pokemon, EventIslandHallEntries(state)), generated),
                World = state.World with { Rng = generated.GlobalRng } };
            return true;
        }
        if (state.EventIslandPreparation is not { } prepared || prepared.Receipt.Origin != origin || state.Script.Wait?.Kind != StoryWaitKind.Battle ||
            !state.Script.Flags.GetValueOrDefault(SpecialWildFlag) || state.World.Rng != prepared.Generated.GlobalRng)
            throw new InvalidDataException("event_island_battle_start_context");
        next = BeginWildBattle(state with { EventIslandPreparation = null }, scriptedPokemon: prepared.Generated,
            eventIslandReceipt: prepared.Receipt);
        return true;
    }

    private CampaignState ReturnFromEventIslandBattle(CampaignState state, CampaignWildBattle battle)
    {
        var receipt = battle.EventIslandReceipt ?? throw new InvalidDataException("event_island_return_receipt");
        var origin = receipt.Origin; var source = EventLegendary(origin.Kind);
        if (state.World.Player.MapId != origin.Map || state.Script.Wait is not { Kind: StoryWaitKind.Battle } wait ||
            !state.Script.Flags.GetValueOrDefault(SpecialWildFlag)) throw new InvalidDataException("event_island_return_context");
        var caller = state.Script;
        var next = state with { WildBattle = null, Dialogue = null, Music = FieldTravelMusic(state),
            Exploration = ResetEncounterModifiers(state.Exploration!), EventIslandReturn = new(origin.Kind, SnorlaxSourceOutcome(battle)),
            Script = caller with { Stack = [], Wait = null, Completed = true } };
        if (_content.MapDefinition(origin.Map).Programs.OnResume != source.Resume) throw new InvalidDataException("event_island_resume_binding");
        next = StartStory(next, source.Resume);
        for (int i = 0; i < 8 && next.Script.Wait is { Kind: StoryWaitKind.NativeEffect } effect; i++)
            next = Advance(next, StoryRules.Continue(next.Script, effect.CorrelationId, _content));
        if (!next.Script.Completed || next.Script.Wait is not null || !next.Script.Stack.IsEmpty ||
            battle.Frame.Outcome == WildBattleOutcome.Caught && next.Actors.Values.Any(a => a.SourceLocalId == source.LocalId && a.Active))
            throw new InvalidDataException("event_island_resume_incomplete");
        next = next with { Script = next.Script with { Stack = caller.Stack, Wait = caller.Wait, Completed = caller.Completed, Locked = caller.Locked } };
        return Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content)));
    }

    private bool IsEventIslandHistory(CampaignWildHistory history)
    {
        if (history.EventIslandReceipt is not { } receipt || receipt.Origin is null ||
            history.SevenIsland is not null || history.SevenIslandReceipt is not null || history.CeruleanCaveOrigin is not null || history.MewtwoReceipt is not null ||
            history.CelioRoamer is not null || history.FiveIsland is not null || history.SixIsland is not null || history.FourIsland is not null ||
            history.SeviiRuby is not null || history.VictoryRoad is not null || history.LostelleHypno is not null || history.Safari ||
            history.ScriptedMarowak || history.ScriptedSnorlaxRoute != 0 || !Enum.IsDefined(receipt.Origin.Kind) ||
            receipt.Origin != EventIslandWildBattleContract.Origin(receipt.Origin.Kind) || receipt.InitialPokemon is null ||
            history.SourceSpeciesId != _content.GamePokemon!.Catalog.Species[EventIslandWildBattleContract.Species(receipt.Origin.Kind)].SourceSpeciesId || history.Level != EventIslandWildBattleContract.Level(receipt.Origin.Kind) ||
            history.Outcome == WildBattleOutcome.Ongoing || history.Outcome != WildBattleOutcome.Won && history.ExperienceGained != 0 ||
            receipt.HallEntries < 1 || receipt.RngBefore.AlgorithmId != Gen3Rng.AlgorithmId || receipt.RngBefore.Cursor > ulong.MaxValue - 4)
            return false;
        var made = EventIslandPokemonFactory.Generate(_content.GamePokemon!, receipt.Origin.Kind, receipt.RngBefore, receipt.InitialPokemon.OriginalTrainerId);
        return made.GlobalRng == receipt.RngAfter && SameSourceOwnedPokemon(made.Pokemon, receipt.InitialPokemon);
    }

    private bool IsEventIslandCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.SingleOrDefault(b => b.Id == capture.BattleId);
        if (capture.EventIsland is not { } origin || history is null || !IsEventIslandHistory(history) ||
            history.EventIslandReceipt!.Origin != origin || history.Outcome != WildBattleOutcome.Caught ||
            capture.SevenIsland is not null || capture.CeruleanCaveOrigin is not null || capture.CelioRoamer is not null ||
            capture.FiveIsland is not null || capture.SixIsland is not null || capture.FourIsland is not null ||
            capture.SourceSpeciesId != history.SourceSpeciesId || capture.MetLevel != history.Level ||
            capture.MetLocation != _content.MapDefinition(origin.Map).RegionMapSection) return false;
        var initial = history.EventIslandReceipt.InitialPokemon;
        var owned = OwnedPokemon(state).SingleOrDefault(p => p.InstanceId == capture.InstanceId);
        return owned is not null && owned.ModernFatefulEncounter && initial.OriginalTrainerId == state.TrainerId &&
            capture.Personality == initial.Personality && capture.Ivs == initial.Ivs && SourceOwnedIdentity(owned.Pokemon, initial);
    }

    private string? ValidateEventIslandBattleProgress(CampaignState state)
    {
        if (state.EventIslandPreparation is not null || state.EventIslandReturn is not null) return "save_event_island_battle_active";
        int hall = EventIslandHallEntries(state);
        foreach (var kind in Enum.GetValues<EventIslandEncounterKind>())
        {
            var source = EventLegendary(kind);
            var attempts = (state.Exploration?.Battles ?? []).Where(b => b.EventIslandReceipt?.Origin.Kind == kind).ToArray();
            bool fought = state.Script.Flags.GetValueOrDefault(source.Fought), hidden = state.Script.Flags.GetValueOrDefault(source.Hidden);
            bool flew = state.Script.Flags.GetValueOrDefault(source.FlewAway);
            if (attempts.Any(b => !IsEventIslandHistory(b) || b.EventIslandReceipt!.HallEntries > hall ||
                b.EventIslandReceipt.InitialPokemon.OriginalTrainerId != state.TrainerId || b.EventIslandReceipt.RngAfter.Cursor > state.World.Rng.Cursor))
                return "save_event_island_history";
            for (int i = 1; i < attempts.Length; i++)
            {
                var previous = attempts[i - 1]; var current = attempts[i];
                if (previous.Outcome == WildBattleOutcome.Caught || previous.EventIslandReceipt!.HallEntries > current.EventIslandReceipt!.HallEntries ||
                    previous.Outcome == WildBattleOutcome.Won && previous.EventIslandReceipt.HallEntries == current.EventIslandReceipt.HallEntries)
                    return "save_event_island_reencounter";
            }
            bool caught = attempts.LastOrDefault()?.Outcome == WildBattleOutcome.Caught;
            bool lastDefeat = attempts.LastOrDefault() is { Outcome: WildBattleOutcome.Won } last && last.EventIslandReceipt!.HallEntries == hall;
            if (fought != caught || caught && !hidden || flew != lastDefeat) return "save_event_island_completion";
            if (state.World.Player.MapId == EventIslandWildBattleContract.Origin(kind).Map &&
                state.Actors.Values.Any(a => a.SourceLocalId == source.LocalId && a.Active == hidden)) return "save_event_island_actor";
        }
        return null;
    }
}
