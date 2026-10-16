using Game.Domain.Encounters;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static bool TypedFishingEnabled(FishingRod rod) =>
        rod is FishingRod.Old or FishingRod.Good or FishingRod.Super;

    private bool IsTypedFishingOrigin(FishingEncounterOrigin origin) => TypedFishingEnabled(origin.Rod) &&
        _content.FishingTables?.SourceSlot(origin) is not null;

    private bool IsTypedFishingHistory(CampaignWildHistory history)
    {
        if (history.Safari) return IsSafariFishingHistory(history);
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
        if (capture.Ball == "ITEM_SAFARI_BALL") return IsSafariFishingCapture(state, capture);
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
