using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateFourIslandLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        FourIslandWildPokemonFactory.Generate(_content.FourIslandOwnership!,table,encounter,state.World.Rng,state.TrainerId);

    private bool IsFourIslandWildHistory(CampaignWildHistory history)
    {
        if (!_content.FourIslandEnabled || history.FourIsland is not {} origin || history.SevenIsland is not null || history.SixIsland is not null || history.SevenIsland is not null || history.FiveIsland is not null || history.SeviiRuby is not null || history.VictoryRoad is not null ||
            history.Safari || history.ScriptedMarowak || history.ScriptedSnorlaxRoute!=0 || history.LostelleHypno is not null ||
            origin.Kind is not (FourIslandEncounterKind.Land or FourIslandEncounterKind.Surf or FourIslandEncounterKind.OldRod) ||
            FourIslandWildBattleContract.SourceSlot(origin) is not {} slot) return false;
        return _content.FourIslandOwnership!.Catalog.Species[slot.Species].SourceSpeciesId==history.SourceSpeciesId &&
            history.Level>=slot.MinimumLevel && history.Level<=slot.MaximumLevel;
    }

    private bool IsFourIslandCapture(CampaignState state,CampaignCapturedPokemon capture)
    {
        var history=state.Exploration?.Battles.SingleOrDefault(h=>h.Id==capture.BattleId);
        return history is not null && capture.FiveIsland is null && capture.SixIsland is null && capture.FourIsland is not null && capture.FourIsland == history.FourIsland && IsFourIslandWildHistory(history) && history.Outcome==WildBattleOutcome.Caught &&
            history.ExperienceGained==0 && history.SourceSpeciesId==capture.SourceSpeciesId && history.Level==capture.MetLevel &&
            _content.MapDefinition(history.FourIsland!.Map).RegionMapSection==capture.MetLocation;
    }
}
