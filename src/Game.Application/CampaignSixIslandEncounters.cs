using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateSixIslandLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        SixIslandWildPokemonFactory.Generate(_content.SixIslandOwnership!,table,encounter,state.World.Rng,state.TrainerId);

    private bool IsSixIslandWildHistory(CampaignWildHistory history)
    {
        if (!_content.SixIslandEnabled || history.SixIsland is not {} origin || history.SevenIsland is not null || history.FiveIsland is not null || history.SeviiRuby is not null || history.FourIsland is not null || history.VictoryRoad is not null ||
            history.Safari || history.ScriptedMarowak || history.ScriptedSnorlaxRoute!=0 || history.LostelleHypno is not null ||
            origin.Kind is not (SixIslandEncounterKind.Land or SixIslandEncounterKind.Surf or SixIslandEncounterKind.OldRod) ||
            SixIslandWildBattleContract.SourceSlot(origin) is not {} slot) return false;
        return _content.SixIslandOwnership!.Catalog.Species[slot.Species].SourceSpeciesId==history.SourceSpeciesId &&
            history.Level>=slot.MinimumLevel && history.Level<=slot.MaximumLevel;
    }

    private bool IsSixIslandCapture(CampaignState state,CampaignCapturedPokemon capture)
    {
        var history=state.Exploration?.Battles.SingleOrDefault(h=>h.Id==capture.BattleId);
        return history is not null && capture.FiveIsland is null && capture.FourIsland is null && capture.SixIsland is not null && capture.SixIsland == history.SixIsland && IsSixIslandWildHistory(history) && history.Outcome==WildBattleOutcome.Caught &&
            history.ExperienceGained==0 && history.SourceSpeciesId==capture.SourceSpeciesId && history.Level==capture.MetLevel &&
            _content.MapDefinition(history.SixIsland!.Map).RegionMapSection==capture.MetLocation;
    }
}
