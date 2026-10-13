using System.Collections.Immutable;
using Game.Domain.Encounters;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private CampaignState BeginFishingWildBattle(CampaignState state,OldRodFishingState fishing)
    {
        if(!_content.CityServicesEnabled||state.Fishing is null||fishing.Status!=OldRodFishingStatus.EncounterReady||
            fishing.Map!=state.World.Player.MapId||!_content.OldRodEncounters!.TryGetValue(fishing.Map,out var table))
            throw new InvalidDataException("fishing_encounter_context_invalid");
        var candidate=_content.SeviiRubyEnabled?SeviiRubyOldRodEncounterRules.GenerateCandidate(_content.SeviiRubyOwnership!,table,fishing,state.World.Rng):_content.VictoryRoadEnabled?VictoryRoadOldRodEncounterRules.GenerateCandidate(_content.CinnabarOwnership!,table,fishing,state.World.Rng):_content.FirstSeviiEnabled?FirstSeviiOldRodEncounterRules.GenerateCandidate(_content.CinnabarOwnership!,table,fishing,state.World.Rng):_content.CinnabarEnabled?CinnabarOldRodEncounterRules.GenerateCandidate(_content.CinnabarOwnership!,table,fishing,state.World.Rng):OldRodEncounterRules.GenerateCandidate(table,fishing,state.World.Rng);
        var generated=_content.SeviiRubyEnabled?SeviiRubyOldRodEncounterRules.GeneratePokemon(_content.SeviiRubyOwnership!,candidate.Candidate,candidate.GlobalRng,state.TrainerId):_content.VictoryRoadEnabled?VictoryRoadOldRodEncounterRules.GeneratePokemon(_content.CinnabarOwnership!,candidate.Candidate,candidate.GlobalRng,state.TrainerId):_content.FirstSeviiEnabled?FirstSeviiOldRodEncounterRules.GeneratePokemon(_content.CinnabarOwnership!,candidate.Candidate,candidate.GlobalRng,state.TrainerId):_content.CinnabarEnabled?CinnabarOldRodEncounterRules.GeneratePokemon(_content.CinnabarOwnership!,candidate.Candidate,candidate.GlobalRng,state.TrainerId):OldRodPokemonFactory.Generate(OrdinaryEncounterPokemon,candidate.Candidate,candidate.GlobalRng,state.TrainerId,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,snorlaxRules:_content.SnorlaxEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled);
        var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty;
        var next=state with{Fishing=null,ItemMenu=null,Exploration=ResetEncounterModifiers(state.Exploration!),GameStats=stats.SetItem("GAME_STAT_FISHING_CAPTURES",Math.Min(0xffffff,stats.GetValueOrDefault("GAME_STAT_FISHING_CAPTURES")+1))};
        // WildBattleRules.Start owns the original AI-init, held-item and initial order draws.
        // Battle entry resets land immunity/rate modifiers, but fishing does not consume
        // a land candidate, draw its independent rate RNG, reduce Repel steps, or add a field step.
        return BeginWildBattle(next,new(generated.Pokemon,generated.GlobalRng,[..candidate.RandomTrace,..generated.RandomTrace]),victoryRoadOrigin:_content.VictoryRoadEnabled&&VictoryRoadEncounterTables.FishingSource.Any(t=>t.Map==fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.VictoryRoadEncounterKind.OldRod,candidate.Candidate.SlotIndex):null,seviiRubyOrigin:_content.SeviiRubyEnabled&&SeviiRubyEncounterTables.FishingSource.Any(t=>t.Map==fishing.Map)?new(fishing.Map,Game.Domain.WildBattle.SeviiRubyEncounterKind.OldRod,candidate.Candidate.SlotIndex):null);
    }
}
