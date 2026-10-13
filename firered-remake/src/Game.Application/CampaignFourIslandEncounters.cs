using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateFourIslandLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        FourIslandWildPokemonFactory.Generate(_content.FourIslandOwnership!,table,encounter,state.World.Rng,state.TrainerId);

    private WildBattleResult StartFourIslandCampaignWild(CampaignState state,int active,
        Route1WildPokemonResult generated,FirstSeviiEncounterKind kind,VictoryRoadWildOrigin? victoryRoadOrigin,
        SeviiRubyWildOrigin? seviiRubyOrigin,FourIslandWildOrigin? fourIslandOrigin,CampaignLostelleHypnoReceipt? lostelleHypno,
        PokemonTowerBattleKind? towerKind,bool scriptedSnorlax)
    {
        if (!_content.FourIslandEnabled || _content.FourIslandOwnership is not {} owned)
            throw new InvalidDataException("four_island_wild_catalog");
        // Scripted encounters reuse the supplied-Pokemon parameter of the historical caller.
        // That parameter is not evidence of a fishing cast.
        if (lostelleHypno is not null || scriptedSnorlax || towerKind is PokemonTowerBattleKind.RevealedMarowak)
            kind=FirstSeviiEncounterKind.Land;
        if (fourIslandOrigin is not null)
        {
            if (fourIslandOrigin.Map!=state.World.Player.MapId || victoryRoadOrigin is not null || seviiRubyOrigin is not null || lostelleHypno is not null ||
                towerKind is not null || scriptedSnorlax || fourIslandOrigin.Kind != (kind switch {
                    FirstSeviiEncounterKind.Land=>FourIslandEncounterKind.Land,
                    FirstSeviiEncounterKind.Surf=>FourIslandEncounterKind.Surf,
                    _=>FourIslandEncounterKind.OldRod}))
                throw new InvalidDataException("four_island_wild_field_origin");
            return WildBattleRules.StartFourIslandWild(owned,fourIslandOrigin,SourceTrainerEntryParty(state),active,
                new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state));
        }
        if (FourIslandEncounterTables.Land.Any(t=>t.Map==state.World.Player.MapId) ||
            FourIslandEncounterTables.Water.Any(t=>t.Map==state.World.Player.MapId))
            throw new InvalidDataException("four_island_wild_missing_field_origin");
        return WildBattleRules.StartFourIslandInheritedWild(owned,state.World.Player.MapId,kind,
            SourceTrainerEntryParty(state),active,new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state),
            victoryRoadOrigin,seviiRubyOrigin,lostelleHypno is not null,towerKind,scriptedSnorlax);
    }

    private bool IsFourIslandWildHistory(CampaignWildHistory history)
    {
        if (!_content.FourIslandEnabled || history.FourIsland is not {} origin || history.SixIsland is not null || history.FiveIsland is not null || history.SeviiRuby is not null || history.VictoryRoad is not null ||
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
