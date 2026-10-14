using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Items;
namespace Game.Application;

public sealed partial class CampaignSession
{
    public static CampaignState UpgradePartyTrainingToForest(CampaignState verifiedState)
    {
        var exploration=verifiedState.Exploration??throw new InvalidDataException("exploration_missing");
        var old=exploration.Encounter??throw new InvalidDataException("legacy_encounter_missing");
        Route1EncounterRules.ValidateState(old);
        if(exploration.EarlyEncounter is not null||old.Pending is not null||verifiedState.WildBattle is not null)throw new InvalidDataException("forest_upgrade_unstable");
        return verifiedState with{Exploration=exploration with{Encounter=null,EarlyEncounter=new(old.RateRng,old.PreviousMetatileBehavior,old.EncounterRateBuff,old.StepsSinceLastEncounter)}};
    }
    private CampaignExploration ResetEncounterModifiers(CampaignExploration state)=>_content.ForestEnabled
        ?state with{EarlyEncounter=EarlyLandEncounterRules.ResetRateModifiers(state.EarlyEncounter??throw new InvalidDataException("early_encounter_missing"))}
        :state with{Encounter=Route1EncounterRules.ResetRateModifiers(state.Encounter!)};
    private bool WildSpeciesSupported(int id)=>_content.SuperRodEnabled&&_content.SuperRodEncounters.Values.SelectMany(t=>t.Slots).Any(s=>_content.PokemonContent!.Catalog.Species[s.Species].SourceSpeciesId==id)||_content.GoodRodEnabled&&_content.GoodRodEncounters!.Values.SelectMany(t=>t.Slots).Any(s=>_content.PokemonContent!.Catalog.Species[s.Species].SourceSpeciesId==id)||_content.SurfEnabled&&_content.WaterEncounters!.Values.SelectMany(t=>t.Slots).Any(s=>_content.PokemonContent!.Catalog.Species[s.Species].SourceSpeciesId==id)||_content.CityServicesEnabled&&id==129||(_content.ForestEnabled
        ?_content.LandEncounters!.Values.SelectMany(t=>t.Slots).Any(s=>_content.PokemonContent!.Catalog.Species[s.Species].SourceSpeciesId==id)
        :id is 16 or 19);
    private bool WildOriginSupported(int id,int level,string? region=null)=>SurfOriginSupported(id,level,region)||_content.CityServicesEnabled&&id==129&&_content.OldRodEncounters!.Values.Any(t=>!IsSourceTableMap(t.Map)&&!IsSeafoamMap(t.Map)&&t.Map.Value!=PowerPlantMap&&(!SevenIslandMap(t.Map))&&(!CeruleanCaveEncounterTables.IsCave(t.Map))&&(!_content.FiveIslandEnabled||!FiveIslandEncounterTables.FishingSource.Any(r=>r.Map==t.Map))&&(!_content.SixIslandEnabled||!SixIslandEncounterTables.FishingSource.Any(r=>r.Map==t.Map))&&(!_content.FourIslandEnabled||!FourIslandEncounterTables.FishingSource.Any(r=>r.Map==t.Map))&&(!_content.SeviiRubyEnabled||!SeviiRubyEncounterTables.FishingSource.Any(r=>r.Map==t.Map))&&(region is null||_content.MapDefinition(t.Map).RegionMapSection==region)&&t.Slots.Any(s=>level>=s.MinimumLevel&&level<=s.MaximumLevel))||(_content.ForestEnabled
        ?_content.LandEncounters!.Values.Any(t=>!IsSourceTableMap(t.Map)&&!IsSeafoamMap(t.Map)&&t.Map.Value!=PowerPlantMap&&(!SevenIslandMap(t.Map))&&(!CeruleanCaveEncounterTables.IsCave(t.Map))&&(!_content.FiveIslandEnabled||!FiveIslandEncounterTables.Land.Any(r=>r.Map==t.Map))&&(!_content.SixIslandEnabled||!SixIslandEncounterTables.Land.Any(r=>r.Map==t.Map))&&(!_content.FourIslandEnabled||!FourIslandEncounterTables.Land.Any(r=>r.Map==t.Map))&&(!_content.SeviiRubyEnabled||!SeviiRubyEncounterTables.Land.Any(r=>r.Map==t.Map))&&(region is null||_content.MapDefinition(t.Map).RegionMapSection==region)&&t.Slots.Any(s=>_content.PokemonContent!.Catalog.Species[s.Species].SourceSpeciesId==id&&level>=s.MinimumLevel&&level<=s.MaximumLevel))
        :id is 16 or 19&&level is >=2 and <=5&&(region is null||region=="MAPSEC_ROUTE_1"));
    private CampaignState CheckEarlyLandEncounter(CampaignState state,EncounterCheckTrigger trigger)
    {
        var p=state.World.Player;var cell=_content.ConnectedFieldMaps![p.MapId].CellAt(p.X,p.Y)!;var exploration=state.Exploration!;
        var process=exploration.EarlyEncounter??throw new InvalidDataException("early_encounter_missing");
        if(SevenIslandEncounterTables.IsChamber(p.MapId)&&!state.Script.Flags.GetValueOrDefault("FLAG_SYS_UNLOCKED_TANOBY_RUINS")||!_content.LandEncounters!.TryGetValue(p.MapId,out var table))return state with{Exploration=exploration with{EarlyEncounter=process with{PreviousMetatileBehavior=(ushort)cell.SourceBehavior}}};
        if(state.Party.IsEmpty||state.Party.All(p=>p.IsEgg||p.Pokemon.CurrentHp==0))throw new InvalidDataException("wild_encounter_live_party_required");
        var check=new EarlyLandEncounterCheck(p.MapId,trigger,(byte)cell.SourceBehavior,(_content.Maps[p.MapId].SourceEncounterTypeAt(p.X,p.Y)==1)?EncounterTileType.Land:EncounterTileType.None);
        var result=EarlyLandEncounterRules.Check(_content.PowerPlantOwnership!,process,table,check,state.Party,
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)),state.World.Rng,
            state.Roamer??Game.Domain.Roaming.RoamerState.Empty,CelioSourceMap(p.MapId),saffronShop:SaffronGymShopProof,currentOwned:_binding.Ownership);
        var next=state with{Exploration=exploration with{EarlyEncounter=result.State},World=state.World with{Rng=result.GlobalRng}};
        if(result.RoamerCandidate is {} roamer)
        {
            next=BeginCelioRoamerBattle(next,roamer,Game.Domain.Roaming.RoamerEncounterMethod.Land,result.RandomTrace);
            return next with{Exploration=next.Exploration! with{EarlyEncounter=CelioEncounterRules.CompleteRoamerCandidate(result,table)}};
        }
        return result.Candidate is null?next:state.SafariVisit is {Active:true}?GenerateSafariBattle(next):BeginWildBattle(next);
    }
}
