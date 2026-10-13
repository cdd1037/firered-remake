using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateCurrentLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        CeruleanCaveWildPokemonFactory.Generate(_content.CeruleanCaveOwnership!,table,encounter,state.World.Rng,state.TrainerId);

    private bool IsFiveIslandWildHistory(CampaignWildHistory history)
    {
        if (!_content.FiveIslandEnabled || history.FiveIsland is not {} origin || history.SixIsland is not null || history.SeviiRuby is not null || history.FourIsland is not null || history.VictoryRoad is not null ||
            history.Safari || history.ScriptedMarowak || history.ScriptedSnorlaxRoute!=0 || history.LostelleHypno is not null ||
            origin.Kind is not (FiveIslandEncounterKind.Land or FiveIslandEncounterKind.Surf or FiveIslandEncounterKind.OldRod) ||
            FiveIslandWildBattleContract.SourceSlot(origin) is not {} slot) return false;
        return _content.FiveIslandOwnership!.Catalog.Species[slot.Species].SourceSpeciesId==history.SourceSpeciesId &&
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
