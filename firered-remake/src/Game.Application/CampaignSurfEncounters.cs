using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Randomness;
namespace Game.Application;

public sealed partial class CampaignSession
{
    // The source owns ONE encounter-rate RNG, previous behavior, cooldown and rate buff.
    // Water uses a distinct authenticated five-slot table, with a transient adapter only.
    private WaterEncounterState WaterProcess(EarlyLandEncounterState shared)
    {
        if(shared.Pending is not null)throw new InvalidDataException("surf_land_candidate_pending");
        return new(shared.RateRng,shared.PreviousMetatileBehavior,shared.EncounterRateBuff,shared.StepsSinceLastEncounter){RulesVersion=WaterEncounterRules.FiveIslandRulesVersion};
    }
    private static EarlyLandEncounterState SharedWaterProcess(EarlyLandEncounterState shared,WaterEncounterState water)=>
        shared with{RateRng=water.RateRng,PreviousMetatileBehavior=water.PreviousMetatileBehavior,EncounterRateBuff=water.EncounterRateBuff,StepsSinceLastEncounter=water.StepsSinceLastEncounter,Pending=null};
    private CampaignState CheckSurfWaterEncounter(CampaignState state,EncounterCheckTrigger trigger)
    {
        if(!_content.SurfEnabled||_content.SurfOwnership is null||_content.WaterEncounters is not {} tables||!state.IsSurfing||!IsSurfWater(state.World.Player))
            throw new InvalidDataException("surf_water_encounter_context");
        var p=state.World.Player;var exploration=state.Exploration??throw new InvalidDataException("exploration_missing");
        var shared=exploration.EarlyEncounter??throw new InvalidDataException("early_encounter_missing");
        byte behavior=checked((byte)_content.ConnectedFieldMaps![p.MapId].CellAt(p.X,p.Y)!.SourceBehavior);
        // All source water tables within the loaded region are authenticated by Content.
        // A source map without a water table retains the previous-behavior callback only.
        if(!tables.TryGetValue(p.MapId,out var table))return state with{Exploration=exploration with{EarlyEncounter=shared with{PreviousMetatileBehavior=behavior}}};
        if(state.Party.IsEmpty||state.Party.All(p=>p.IsEgg||p.Pokemon.CurrentHp==0))throw new InvalidDataException("wild_encounter_live_party_required");
        var result=WaterEncounterRules.Check(_content.SevenIslandOwnership!,WaterProcess(shared),table,
            new(p.MapId,trigger,behavior,_content.Maps[p.MapId].SourceEncounterTypeAt(p.X,p.Y)==2?EncounterTileType.Water:EncounterTileType.None),
            state.Party,checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)),state.World.Rng,state.Roamer??Game.Domain.Roaming.RoamerState.Empty,CelioSourceMap(p.MapId),saffronShop:SaffronGymShopProof);
        var next=state with{Exploration=exploration with{EarlyEncounter=SharedWaterProcess(shared,result.State)},World=state.World with{Rng=result.GlobalRng}};
        if(result.RoamerCandidate is {} roamer)
        {
            next=BeginCelioRoamerBattle(next,roamer,Game.Domain.Roaming.RoamerEncounterMethod.Surfing,result.RandomTrace);
            return next with{Exploration=next.Exploration! with{EarlyEncounter=SharedWaterProcess(shared,CelioEncounterRules.CompleteRoamerCandidate(result,table))}};
        }
        if(result.Candidate is null)return next;
        var generated=WaterEncounterRules.Generate(
            _content.SevenIslandOwnership!,result.State,table,next.World.Rng,next.TrainerId);
        if(state.SafariVisit is {Active:true})
        {
            var rng=generated.GlobalRng;var trace=generated.RandomTrace.ToBuilder();
            for(int slot=0;slot<4;slot++){var draw=Gen3Rng.Next(rng,"safari.entry.ai-setup.slot-"+slot,16);rng=draw.State;trace.Add(draw.Trace);}
            var held=WaterEncounterRules.AssignHeldItem(result.State,table,rng);trace.AddRange(held.RandomTrace);
            return BeginSafariBattle(next,generated.Pokemon,held.GlobalRng,trace.ToImmutable(),held.HeldItem,result.State);
        }
        next=next with{Exploration=next.Exploration! with{EarlyEncounter=SharedWaterProcess(shared,WaterEncounterRules.CompleteCandidate(result.State,table))}};
        return BeginWildBattle(next,surfPokemon:generated,sevenIslandOrigin:SevenIslandEncounterTables.Water.Any(t=>t.Map==p.MapId)?new(p.MapId,Game.Domain.WildBattle.SevenIslandEncounterKind.Surf,result.Candidate.Slot):null,ceruleanCaveOrigin:CeruleanCaveEncounterTables.IsCave(p.MapId)?new(p.MapId,Game.Domain.WildBattle.CeruleanCaveEncounterKind.Surf,result.Candidate.Slot):null,victoryRoadOrigin:_content.VictoryRoadEnabled&&VictoryRoadEncounterTables.Water.Any(t=>t.Map==p.MapId)?new(p.MapId,Game.Domain.WildBattle.VictoryRoadEncounterKind.Surf,result.Candidate.Slot):null,seviiRubyOrigin:_content.SeviiRubyEnabled&&SeviiRubyEncounterTables.Water.Any(t=>t.Map==p.MapId)?new(p.MapId,Game.Domain.WildBattle.SeviiRubyEncounterKind.Surf,result.Candidate.Slot):null,fiveIslandOrigin:_content.FiveIslandEnabled&&FiveIslandEncounterTables.Water.Any(t=>t.Map==p.MapId)?new(p.MapId,Game.Domain.WildBattle.FiveIslandEncounterKind.Surf,result.Candidate.Slot):null,sixIslandOrigin:_content.SixIslandEnabled&&SixIslandEncounterTables.Water.Any(t=>t.Map==p.MapId)?new(p.MapId,Game.Domain.WildBattle.SixIslandEncounterKind.Surf,result.Candidate.Slot):null,fourIslandOrigin:_content.FourIslandEnabled&&FourIslandEncounterTables.Water.Any(t=>t.Map==p.MapId)?new(p.MapId,Game.Domain.WildBattle.FourIslandEncounterKind.Surf,result.Candidate.Slot):null);
    }
    private bool SurfOriginSupported(int species,int level,string? region=null,bool safariOnly=false)=>_content.SurfEnabled&&
        _content.WaterEncounters!.Values.Any(t=>(!SevenIslandMap(t.Map))&&(!CeruleanCaveEncounterTables.IsCave(t.Map))&&(!_content.FiveIslandEnabled||!FiveIslandEncounterTables.Water.Any(r=>r.Map==t.Map))&&(!_content.SixIslandEnabled||!SixIslandEncounterTables.Water.Any(r=>r.Map==t.Map))&&(!_content.FourIslandEnabled||!FourIslandEncounterTables.Water.Any(r=>r.Map==t.Map))&&(!_content.SeviiRubyEnabled||!SeviiRubyEncounterTables.Water.Any(r=>r.Map==t.Map))&&(!safariOnly||t.SourceMapSymbol.StartsWith("MAP_SAFARI_ZONE_",StringComparison.Ordinal))&&
            (region is null||_content.MapDefinition(t.Map).RegionMapSection==region)&&
            t.Slots.Any(slot=>_content.PokemonContent!.Catalog.Species[slot.Species].SourceSpeciesId==species&&level>=slot.MinimumLevel&&level<=slot.MaximumLevel));
}
