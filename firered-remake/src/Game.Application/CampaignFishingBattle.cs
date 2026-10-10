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
        var candidate=OldRodEncounterRules.GenerateCandidate(table,fishing,state.World.Rng);
        var generated=OldRodPokemonFactory.Generate(BattlePokemon,candidate.Candidate,candidate.GlobalRng,state.TrainerId);
        var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty;
        var next=state with{Fishing=null,ItemMenu=null,Exploration=ResetEncounterModifiers(state.Exploration!),GameStats=stats.SetItem("GAME_STAT_FISHING_CAPTURES",Math.Min(0xffffff,stats.GetValueOrDefault("GAME_STAT_FISHING_CAPTURES")+1))};
        // WildBattleRules.Start owns the original AI-init, held-item and initial order draws.
        // Battle entry resets land immunity/rate modifiers, but fishing does not consume
        // a land candidate, draw its independent rate RNG, reduce Repel steps, or add a field step.
        return BeginWildBattle(next,new(generated.Pokemon,generated.GlobalRng,[..candidate.RandomTrace,..generated.RandomTrace]));
    }
}
