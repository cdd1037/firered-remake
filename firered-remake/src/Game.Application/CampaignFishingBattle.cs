using System.Collections.Immutable;
using Game.Domain.Encounters;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private CampaignState BeginFishingWildBattle(CampaignState state,OldRodFishingState fishing)
    {
        if (fishing.Rod is FishingRod.Good or FishingRod.Super) return BeginTypedFishingWildBattle(state, fishing);
        if(!_content.CityServicesEnabled||state.Fishing is null||fishing.Status!=OldRodFishingStatus.EncounterReady||
            fishing.Map!=state.World.Player.MapId||!_content.OldRodEncounters!.TryGetValue(fishing.Map,out var table))
            throw new InvalidDataException("fishing_encounter_context_invalid");
        // Current ownership authenticates the complete inherited Old Rod table set.
        var candidate=SevenIslandOldRodEncounterRules.GenerateCandidate(
            _content.SevenIslandOwnership!,table,fishing,state.World.Rng);
        var seafoamOrigin=IsSeafoamMap(fishing.Map)?new Game.Domain.WildBattle.SeafoamWildOrigin(fishing.Map,Game.Domain.WildBattle.SeafoamEncounterKind.OldRod,candidate.Candidate.SlotIndex):null;
        Route1WildPokemonResult generated;
        if(seafoamOrigin is not null)
            generated=SeafoamPokemonFactory.GenerateSourceSlot(_content.SeafoamOwnership!,seafoamOrigin,candidate.Candidate.Level,candidate.GlobalRng,state.TrainerId);
        else
        {
            var ordinary=SevenIslandOldRodEncounterRules.GeneratePokemon(_content.SevenIslandOwnership!,candidate.Candidate,candidate.GlobalRng,state.TrainerId);
            generated=new(ordinary.Pokemon,ordinary.GlobalRng,ordinary.RandomTrace);
        }
        var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty;
        var next=state with{Fishing=null,ItemMenu=null,Exploration=ResetEncounterModifiers(state.Exploration!),GameStats=stats.SetItem("GAME_STAT_FISHING_CAPTURES",Math.Min(0xffffff,stats.GetValueOrDefault("GAME_STAT_FISHING_CAPTURES")+1))};
        // Shared source battle entry owns the original AI-init, held-item and initial order draws.
        // Battle entry resets land immunity/rate modifiers, but fishing does not consume
        // a land candidate, draw its independent rate RNG, reduce Repel steps, or add a field step.
        return BeginWildBattle(next,new(generated.Pokemon,generated.GlobalRng,[..candidate.RandomTrace,..generated.RandomTrace]),seafoamOrigin:seafoamOrigin,seafoamGenerationRng:candidate.GlobalRng,sevenIslandGenerationRng:candidate.GlobalRng,sevenIslandOrigin:SevenIslandEncounterTables.OldRod.Any(t=>t.Map==fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.SevenIslandEncounterKind.OldRod,candidate.Candidate.SlotIndex):null,ceruleanCaveOrigin:CeruleanCaveEncounterTables.IsCave(fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.CeruleanCaveEncounterKind.OldRod,candidate.Candidate.SlotIndex):null,victoryRoadOrigin:_content.VictoryRoadEnabled&&VictoryRoadEncounterTables.FishingSource.Any(t=>t.Map==fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.VictoryRoadEncounterKind.OldRod,candidate.Candidate.SlotIndex):null,seviiRubyOrigin:_content.SeviiRubyEnabled&&SeviiRubyEncounterTables.FishingSource.Any(t=>t.Map==fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.SeviiRubyEncounterKind.OldRod,candidate.Candidate.SlotIndex):null,fiveIslandOrigin:_content.FiveIslandEnabled&&FiveIslandEncounterTables.FishingSource.Any(t=>t.Map==fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.FiveIslandEncounterKind.OldRod,candidate.Candidate.SlotIndex):null,sixIslandOrigin:_content.SixIslandEnabled&&SixIslandEncounterTables.FishingSource.Any(t=>t.Map==fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.SixIslandEncounterKind.OldRod,candidate.Candidate.SlotIndex):null,fourIslandOrigin:_content.FourIslandEnabled&&FourIslandEncounterTables.FishingSource.Any(t=>t.Map==fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.FourIslandEncounterKind.OldRod,candidate.Candidate.SlotIndex):null);
    }
}
