using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateFiveIslandLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        FiveIslandWildPokemonFactory.Generate(_content.FiveIslandOwnership!,table,encounter,state.World.Rng,state.TrainerId);

    private WildBattleResult StartFiveIslandCampaignWild(CampaignState state,int active,
        Route1WildPokemonResult generated,FirstSeviiEncounterKind kind,VictoryRoadWildOrigin? victoryRoadOrigin,
        SeviiRubyWildOrigin? seviiRubyOrigin,FourIslandWildOrigin? fourIslandOrigin,SixIslandWildOrigin? sixIslandOrigin,FiveIslandWildOrigin? fiveIslandOrigin,CampaignLostelleHypnoReceipt? lostelleHypno,
        PokemonTowerBattleKind? towerKind,bool scriptedSnorlax)
    {
        if (!_content.FiveIslandEnabled || _content.FiveIslandOwnership is not {} owned)
            throw new InvalidDataException("five_island_wild_catalog");
        // Scripted encounters reuse the supplied-Pokemon parameter of the historical caller.
        // That parameter is not evidence of a fishing cast.
        if (lostelleHypno is not null || scriptedSnorlax || towerKind is PokemonTowerBattleKind.RevealedMarowak)
            kind=FirstSeviiEncounterKind.Land;
        if (fiveIslandOrigin is not null)
        {
            if (fiveIslandOrigin.Map!=state.World.Player.MapId || victoryRoadOrigin is not null || seviiRubyOrigin is not null || fourIslandOrigin is not null || sixIslandOrigin is not null || lostelleHypno is not null ||
                towerKind is not null || scriptedSnorlax || fiveIslandOrigin.Kind != (kind switch {
                    FirstSeviiEncounterKind.Land=>FiveIslandEncounterKind.Land,
                    FirstSeviiEncounterKind.Surf=>FiveIslandEncounterKind.Surf,
                    _=>FiveIslandEncounterKind.OldRod}))
                throw new InvalidDataException("five_island_wild_field_origin");
            return WildBattleRules.StartFiveIslandWild(owned,fiveIslandOrigin,SourceTrainerEntryParty(state),active,
                new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state));
        }
        if (FiveIslandEncounterTables.Land.Any(t=>t.Map==state.World.Player.MapId) ||
            FiveIslandEncounterTables.Water.Any(t=>t.Map==state.World.Player.MapId))
            throw new InvalidDataException("five_island_wild_missing_field_origin");
        return WildBattleRules.StartFiveIslandInheritedWild(owned,state.World.Player.MapId,kind,
            SourceTrainerEntryParty(state),active,new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state),
            victoryRoadOrigin,seviiRubyOrigin,fourIslandOrigin,sixIslandOrigin,lostelleHypno is not null,towerKind,scriptedSnorlax);
    }

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
