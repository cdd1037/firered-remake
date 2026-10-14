using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private CampaignState BeginGoodRodWildBattle(CampaignState state, OldRodFishingState fishing)
    {
        if (!_content.GoodRodEnabled || state.Fishing is null || fishing.Status != OldRodFishingStatus.EncounterReady ||
            fishing.Map != state.World.Player.MapId || !_content.GoodRodEncounters!.TryGetValue(fishing.Map, out var table))
            throw new InvalidDataException("good_rod_encounter_context_invalid");
        var owner = SafariOrdinaryOwnership ?? throw new InvalidDataException("good_rod_owned_catalog");
        var candidate = GoodRodEncounterRules.GenerateCandidate(owner, table, fishing, state.World.Rng);
        var generated = GoodRodEncounterRules.GeneratePokemon(owner, candidate.Candidate, candidate.GlobalRng, state.TrainerId);
        var origin = new FishingEncounterOrigin(FishingRod.Good, fishing.Map, candidate.Candidate.SlotIndex,
            candidate.Candidate.Species, candidate.Candidate.Level);
        var stats = state.GameStats ?? ImmutableSortedDictionary<string,int>.Empty;
        var next = state with { Fishing = null, ItemMenu = null, Exploration = ResetEncounterModifiers(state.Exploration!),
            GameStats = stats.SetItem("GAME_STAT_FISHING_CAPTURES", Math.Min(0xffffff, stats.GetValueOrDefault("GAME_STAT_FISHING_CAPTURES") + 1)) };
        return BeginWildBattle(next, new(generated.Pokemon, generated.GlobalRng, [..candidate.RandomTrace, ..generated.RandomTrace]), fishingOrigin: origin);
    }

    private bool IsGoodRodHistory(CampaignWildHistory history)
    {
        if (!_content.GoodRodEnabled || history.Fishing is not { Rod: FishingRod.Good } origin ||
            !_content.GoodRodEncounters!.ContainsKey(origin.Map) || FishingBattleContract.SourceSlot(origin) is null ||
            !_content.PokemonContent!.Catalog.Species.TryGetValue(origin.Species, out var species) ||
            species.SourceSpeciesId != history.SourceSpeciesId || origin.Level != history.Level) return false;
        return history.PowerPlantReceipt is null && history.EventIslandReceipt is null && history.CelioRoamer is null &&
            history.CeruleanCaveOrigin is null && history.SevenIsland is null && history.SevenIslandReceipt is null &&
            history.MewtwoReceipt is null && history.FiveIsland is null && history.SixIsland is null &&
            history.FourIsland is null && history.SeviiRuby is null && history.VictoryRoad is null &&
            history.LostelleHypno is null && !history.Safari && !history.ScriptedMarowak && history.ScriptedSnorlaxRoute == 0;
    }

    private bool IsGoodRodCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.SingleOrDefault(h => h.Id == capture.BattleId);
        return capture.Fishing is { } origin && capture.PowerPlant is null && capture.EventIsland is null &&
            capture.CeruleanCaveOrigin is null && capture.SevenIsland is null && capture.CelioRoamer is null &&
            capture.FiveIsland is null && capture.SixIsland is null && capture.FourIsland is null &&
            history is not null && history.Fishing == origin && IsGoodRodHistory(history) &&
            history.Outcome == WildBattleOutcome.Caught && history.ExperienceGained == 0 &&
            history.SourceSpeciesId == capture.SourceSpeciesId && history.Level == capture.MetLevel &&
            _content.MapDefinition(origin.Map).RegionMapSection == capture.MetLocation;
    }
}
