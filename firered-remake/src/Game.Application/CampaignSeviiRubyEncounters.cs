using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateSeviiRubyLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        SeviiRubyWildPokemonFactory.Generate(_content.SeviiRubyOwnership!,table,encounter,state.World.Rng,state.TrainerId);

    private bool IsSeviiRubyWildHistory(CampaignWildHistory history)
    {
        if (!_content.SeviiRubyEnabled || history.SeviiRuby is not {} origin || history.SevenIsland is not null || history.SixIsland is not null || history.FourIsland is not null || history.VictoryRoad is not null ||
            history.SevenIsland is not null || history.FiveIsland is not null || history.Safari || history.ScriptedMarowak || history.ScriptedSnorlaxRoute!=0 || history.LostelleHypno is not null ||
            origin.Kind is not (SeviiRubyEncounterKind.Land or SeviiRubyEncounterKind.Surf or SeviiRubyEncounterKind.OldRod) ||
            SeviiRubyWildBattleContract.SourceSlot(origin) is not {} slot) return false;
        return _content.SeviiRubyOwnership!.Catalog.Species[slot.Species].SourceSpeciesId==history.SourceSpeciesId &&
            history.Level>=slot.MinimumLevel && history.Level<=slot.MaximumLevel;
    }

    private bool IsSeviiRubyCapture(CampaignState state,CampaignCapturedPokemon capture)
    {
        var history=state.Exploration?.Battles.SingleOrDefault(h=>h.Id==capture.BattleId);
        return capture.SixIsland is null && capture.FourIsland is null && history is not null && IsSeviiRubyWildHistory(history) && history.Outcome==WildBattleOutcome.Caught &&
            history.ExperienceGained==0 && history.SourceSpeciesId==capture.SourceSpeciesId && history.Level==capture.MetLevel &&
            _content.MapDefinition(history.SeviiRuby!.Map).RegionMapSection==capture.MetLocation;
    }
}
