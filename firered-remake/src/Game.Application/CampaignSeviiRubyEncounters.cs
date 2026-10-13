using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private EarlyLandWildPokemonResult GenerateSeviiRubyLand(CampaignState state,
        EarlyLandEncounterTable table,EarlyLandEncounterState encounter) =>
        SeviiRubyWildPokemonFactory.Generate(_content.SeviiRubyOwnership!,table,encounter,state.World.Rng,state.TrainerId);

    private WildBattleResult StartSeviiRubyCampaignWild(CampaignState state,int active,
        Route1WildPokemonResult generated,FirstSeviiEncounterKind kind,VictoryRoadWildOrigin? victoryRoadOrigin,
        SeviiRubyWildOrigin? seviiRubyOrigin,CampaignLostelleHypnoReceipt? lostelleHypno,
        PokemonTowerBattleKind? towerKind,bool scriptedSnorlax)
    {
        if (!_content.SeviiRubyEnabled || _content.SeviiRubyOwnership is not {} owned)
            throw new InvalidDataException("sevii_ruby_wild_catalog");
        // Scripted encounters reuse the supplied-Pokemon parameter of the historical caller.
        // That parameter is not evidence of a fishing cast.
        if (lostelleHypno is not null || scriptedSnorlax || towerKind is PokemonTowerBattleKind.RevealedMarowak)
            kind=FirstSeviiEncounterKind.Land;
        if (seviiRubyOrigin is not null)
        {
            if (seviiRubyOrigin.Map!=state.World.Player.MapId || victoryRoadOrigin is not null || lostelleHypno is not null ||
                towerKind is not null || scriptedSnorlax || seviiRubyOrigin.Kind != (kind switch {
                    FirstSeviiEncounterKind.Land=>SeviiRubyEncounterKind.Land,
                    FirstSeviiEncounterKind.Surf=>SeviiRubyEncounterKind.Surf,
                    _=>SeviiRubyEncounterKind.OldRod}))
                throw new InvalidDataException("sevii_ruby_wild_field_origin");
            return WildBattleRules.StartSeviiRubyWild(owned,seviiRubyOrigin,SourceTrainerEntryParty(state),active,
                new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state));
        }
        if (SeviiRubyEncounterTables.Land.Any(t=>t.Map==state.World.Player.MapId) ||
            SeviiRubyEncounterTables.Water.Any(t=>t.Map==state.World.Player.MapId))
            throw new InvalidDataException("sevii_ruby_wild_missing_field_origin");
        return WildBattleRules.StartSeviiRubyInheritedWild(owned,state.World.Player.MapId,kind,
            SourceTrainerEntryParty(state),active,new(generated.Pokemon),generated.GlobalRng,CampaignBattleTerrain(state),
            victoryRoadOrigin,lostelleHypno is not null,towerKind,scriptedSnorlax);
    }

    private bool IsSeviiRubyWildHistory(CampaignWildHistory history)
    {
        if (!_content.SeviiRubyEnabled || history.SeviiRuby is not {} origin || history.SixIsland is not null || history.FourIsland is not null || history.VictoryRoad is not null ||
            history.Safari || history.ScriptedMarowak || history.ScriptedSnorlaxRoute!=0 || history.LostelleHypno is not null ||
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
