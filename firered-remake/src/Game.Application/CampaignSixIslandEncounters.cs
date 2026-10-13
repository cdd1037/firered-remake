using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateSixIslandLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        SixIslandWildPokemonFactory.Generate(_content.SixIslandOwnership!,table,encounter,state.World.Rng,state.TrainerId);

    private WildBattleResult StartSixIslandCampaignWild(CampaignState state,int active,
        Route1WildPokemonResult generated,FirstSeviiEncounterKind kind,VictoryRoadWildOrigin? victoryRoadOrigin,
        SeviiRubyWildOrigin? seviiRubyOrigin,FourIslandWildOrigin? fourIslandOrigin,SixIslandWildOrigin? sixIslandOrigin,CampaignLostelleHypnoReceipt? lostelleHypno,
        PokemonTowerBattleKind? towerKind,bool scriptedSnorlax)
    {
        if (!_content.SixIslandEnabled || _content.SixIslandOwnership is not {} owned)
            throw new InvalidDataException("six_island_wild_catalog");
        // Scripted encounters reuse the supplied-Pokemon parameter of the historical caller.
        // That parameter is not evidence of a fishing cast.
        if (lostelleHypno is not null || scriptedSnorlax || towerKind is PokemonTowerBattleKind.RevealedMarowak)
            kind=FirstSeviiEncounterKind.Land;
        if (sixIslandOrigin is not null)
        {
            if (sixIslandOrigin.Map!=state.World.Player.MapId || victoryRoadOrigin is not null || seviiRubyOrigin is not null || fourIslandOrigin is not null || lostelleHypno is not null ||
                towerKind is not null || scriptedSnorlax || sixIslandOrigin.Kind != (kind switch {
                    FirstSeviiEncounterKind.Land=>SixIslandEncounterKind.Land,
                    FirstSeviiEncounterKind.Surf=>SixIslandEncounterKind.Surf,
                    _=>SixIslandEncounterKind.OldRod}))
                throw new InvalidDataException("six_island_wild_field_origin");
            return WildBattleRules.StartSixIslandWild(owned,sixIslandOrigin,SourceTrainerEntryParty(state),active,
                new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state));
        }
        if (SixIslandEncounterTables.Land.Any(t=>t.Map==state.World.Player.MapId) ||
            SixIslandEncounterTables.Water.Any(t=>t.Map==state.World.Player.MapId))
            throw new InvalidDataException("six_island_wild_missing_field_origin");
        return WildBattleRules.StartSixIslandInheritedWild(owned,state.World.Player.MapId,kind,
            SourceTrainerEntryParty(state),active,new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state),
            victoryRoadOrigin,seviiRubyOrigin,fourIslandOrigin,lostelleHypno is not null,towerKind,scriptedSnorlax);
    }

    private bool IsSixIslandWildHistory(CampaignWildHistory history)
    {
        if (!_content.SixIslandEnabled || history.SixIsland is not {} origin || history.SeviiRuby is not null || history.FourIsland is not null || history.VictoryRoad is not null ||
            history.Safari || history.ScriptedMarowak || history.ScriptedSnorlaxRoute!=0 || history.LostelleHypno is not null ||
            origin.Kind is not (SixIslandEncounterKind.Land or SixIslandEncounterKind.Surf or SixIslandEncounterKind.OldRod) ||
            SixIslandWildBattleContract.SourceSlot(origin) is not {} slot) return false;
        return _content.SixIslandOwnership!.Catalog.Species[slot.Species].SourceSpeciesId==history.SourceSpeciesId &&
            history.Level>=slot.MinimumLevel && history.Level<=slot.MaximumLevel;
    }

    private bool IsSixIslandCapture(CampaignState state,CampaignCapturedPokemon capture)
    {
        var history=state.Exploration?.Battles.SingleOrDefault(h=>h.Id==capture.BattleId);
        return history is not null && capture.FourIsland is null && capture.SixIsland is not null && capture.SixIsland == history.SixIsland && IsSixIslandWildHistory(history) && history.Outcome==WildBattleOutcome.Caught &&
            history.ExperienceGained==0 && history.SourceSpeciesId==capture.SourceSpeciesId && history.Level==capture.MetLevel &&
            _content.MapDefinition(history.SixIsland!.Map).RegionMapSection==capture.MetLocation;
    }
}
