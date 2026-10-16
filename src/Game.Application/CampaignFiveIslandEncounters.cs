using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateCurrentLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        OrdinaryLandDefinition(table.Map) is { } ordinary ? GenerateOrdinaryLand(state, ordinary, encounter) :
        IsSourceTableMap(table.Map) ? EarlyLandWildPokemonFactory.Generate(_binding.Ownership,table,encounter,state.World.Rng,state.TrainerId) :
        IsSeafoamMap(table.Map) ? GenerateSeafoamLand(state,encounter) :
        table.Map==PowerPlantEncounterTables.Land.Map
            ? GeneratePowerPlantLand(state,encounter)
            : SevenIslandWildPokemonFactory.Generate(_content.GamePokemon!,table,encounter,state.World.Rng,state.TrainerId);
    private EarlyLandWildPokemonResult GeneratePowerPlantLand(CampaignState state,EarlyLandEncounterState encounter)
    {
        var made=PowerPlantPokemonFactory.GenerateLand(_content.GamePokemon!,encounter.Pending!,state.World.Rng,state.TrainerId);
        return new(made.Pokemon,made.GlobalRng,made.RandomTrace);
    }

    private bool IsFiveIslandWildHistory(CampaignWildHistory history)
    {
        if (history.FiveIsland is not {} origin || history.SevenIsland is not null || history.SixIsland is not null || history.SeviiRuby is not null || history.FourIsland is not null || history.VictoryRoad is not null ||
            history.Safari || history.ScriptedMarowak || history.ScriptedSnorlaxRoute!=0 || history.LostelleHypno is not null ||
            origin.Kind is not (FiveIslandEncounterKind.Land or FiveIslandEncounterKind.Surf or FiveIslandEncounterKind.OldRod) ||
            FiveIslandWildBattleContract.SourceSlot(origin) is not {} slot) return false;
        return _content.GamePokemon!.Catalog.Species[slot.Species].SourceSpeciesId==history.SourceSpeciesId &&
            history.Level>=slot.MinimumLevel && history.Level<=slot.MaximumLevel;
    }

    private bool IsFiveIslandCapture(CampaignState state,CampaignCapturedPokemon capture)
    {
        var history=state.Exploration?.Battles.SingleOrDefault(h=>h.Id==capture.BattleId);
        return history is not null && capture.SixIsland is null && capture.FourIsland is null && capture.FiveIsland is not null && capture.FiveIsland == history.FiveIsland && IsFiveIslandWildHistory(history) && history.Outcome==WildBattleOutcome.Caught &&
            history.ExperienceGained==0 && history.SourceSpeciesId==capture.SourceSpeciesId && history.Level==capture.MetLevel &&
            _content.MapDefinition(history.FiveIsland!.Map).RegionMapSection==capture.MetLocation;
    }
}
