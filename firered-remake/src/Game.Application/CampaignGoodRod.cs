using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TypedFishingEnabled(FishingRod rod) => rod switch
    {
        FishingRod.Good => _content.GoodRodEnabled,
        FishingRod.Super => _content.SuperRodEnabled,
        _ => false
    };

    private ImmutableDictionary<MapId, OldRodEncounterTable> TypedFishingTables(FishingRod rod) => rod switch
    {
        FishingRod.Good => _content.GoodRodEncounters,
        FishingRod.Super => _content.SuperRodEncounters,
        _ => ImmutableDictionary<MapId, OldRodEncounterTable>.Empty
    };

    private bool IsTypedFishingOrigin(FishingEncounterOrigin origin) => TypedFishingEnabled(origin.Rod) &&
        (origin.Rod != FishingRod.Good || !IsSeafoamMap(origin.Map)) &&
        TypedFishingTables(origin.Rod).ContainsKey(origin.Map) && FishingBattleContract.SourceSlot(origin) is not null;

    private CampaignState BeginTypedFishingWildBattle(CampaignState state, OldRodFishingState fishing)
    {
        if (!TypedFishingEnabled(fishing.Rod) || state.Fishing is null || fishing.Status != OldRodFishingStatus.EncounterReady ||
            fishing.Map != state.World.Player.MapId || !TypedFishingTables(fishing.Rod).TryGetValue(fishing.Map, out var table))
            throw new InvalidDataException("fishing_encounter_context_invalid");
        var owner = SafariOrdinaryOwnership ?? throw new InvalidDataException("fishing_owned_catalog");
        var candidate = fishing.Rod == FishingRod.Super
            ? SuperRodEncounterRules.GenerateCandidate(owner, table, fishing, state.World.Rng)
            : GoodRodEncounterRules.GenerateCandidate(owner, table, fishing, state.World.Rng);
        var seafoamOrigin = fishing.Rod == FishingRod.Good && IsSeafoamMap(fishing.Map) ? new SeafoamWildOrigin(fishing.Map, SeafoamEncounterKind.GoodRod, candidate.Candidate.SlotIndex) : null;
        Route1WildPokemonResult generated;
        if (seafoamOrigin is not null)
            generated = SeafoamPokemonFactory.GenerateSourceSlot(_content.SeafoamOwnership!, seafoamOrigin, candidate.Candidate.Level, candidate.GlobalRng, state.TrainerId);
        else
        {
            var ordinary = fishing.Rod == FishingRod.Super
                ? SuperRodEncounterRules.GeneratePokemon(owner, candidate.Candidate, candidate.GlobalRng, state.TrainerId)
                : GoodRodEncounterRules.GeneratePokemon(owner, candidate.Candidate, candidate.GlobalRng, state.TrainerId);
            generated = new(ordinary.Pokemon, ordinary.GlobalRng, ordinary.RandomTrace);
        }
        var origin = new FishingEncounterOrigin(fishing.Rod, fishing.Map, candidate.Candidate.SlotIndex,
            candidate.Candidate.Species, candidate.Candidate.Level);
        var stats = state.GameStats ?? ImmutableSortedDictionary<string,int>.Empty;
        var next = state with { Fishing = null, ItemMenu = null, Exploration = ResetEncounterModifiers(state.Exploration!),
            GameStats = stats.SetItem("GAME_STAT_FISHING_CAPTURES", Math.Min(0xffffff, stats.GetValueOrDefault("GAME_STAT_FISHING_CAPTURES") + 1)) };
        return BeginWildBattle(next, new(generated.Pokemon, generated.GlobalRng, [..candidate.RandomTrace, ..generated.RandomTrace]), fishingOrigin: seafoamOrigin is null ? origin : null, seafoamOrigin: seafoamOrigin, seafoamGenerationRng: candidate.GlobalRng);
    }

    private bool IsTypedFishingHistory(CampaignWildHistory history)
    {
        if (history.Fishing is not { } origin || !IsTypedFishingOrigin(origin) ||
            !_content.PokemonContent!.Catalog.Species.TryGetValue(origin.Species, out var species) ||
            species.SourceSpeciesId != history.SourceSpeciesId || origin.Level != history.Level) return false;
        return history.SeafoamReceipt is null && history.PowerPlantReceipt is null && history.EventIslandReceipt is null && history.CelioRoamer is null &&
            history.CeruleanCaveOrigin is null && history.SevenIsland is null && history.SevenIslandReceipt is null &&
            history.MewtwoReceipt is null && history.FiveIsland is null && history.SixIsland is null &&
            history.FourIsland is null && history.SeviiRuby is null && history.VictoryRoad is null &&
            history.LostelleHypno is null && !history.Safari && !history.ScriptedMarowak && history.ScriptedSnorlaxRoute == 0;
    }

    private bool IsTypedFishingCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.SingleOrDefault(h => h.Id == capture.BattleId);
        return capture.Fishing is { } origin && capture.Seafoam is null && capture.PowerPlant is null && capture.EventIsland is null &&
            capture.CeruleanCaveOrigin is null && capture.SevenIsland is null && capture.CelioRoamer is null &&
            capture.FiveIsland is null && capture.SixIsland is null && capture.FourIsland is null &&
            history is not null && history.Fishing == origin && IsTypedFishingHistory(history) &&
            history.Outcome == WildBattleOutcome.Caught && history.ExperienceGained == 0 &&
            history.SourceSpeciesId == capture.SourceSpeciesId && history.Level == capture.MetLevel &&
            _content.MapDefinition(origin.Map).RegionMapSection == capture.MetLocation;
    }
}
