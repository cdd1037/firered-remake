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
    private bool WildSpeciesSupported(int id)=>_content.CityServicesEnabled&&id==129||(_content.ForestEnabled
        ?_content.LandEncounters!.Values.SelectMany(t=>t.Slots).Any(s=>_content.PokemonContent!.Catalog.Species[s.Species].SourceSpeciesId==id)
        :id is 16 or 19);
    private bool WildOriginSupported(int id,int level,string? region=null)=>_content.CityServicesEnabled&&id==129&&_content.OldRodEncounters!.Values.Any(t=>(region is null||_content.MapDefinition(t.Map).RegionMapSection==region)&&t.Slots.Any(s=>level>=s.MinimumLevel&&level<=s.MaximumLevel))||(_content.ForestEnabled
        ?_content.LandEncounters!.Values.Any(t=>(region is null||_content.MapDefinition(t.Map).RegionMapSection==region)&&t.Slots.Any(s=>_content.PokemonContent!.Catalog.Species[s.Species].SourceSpeciesId==id&&level>=s.MinimumLevel&&level<=s.MaximumLevel))
        :id is 16 or 19&&level is >=2 and <=5&&(region is null||region=="MAPSEC_ROUTE_1"));
    private CampaignState CheckEarlyLandEncounter(CampaignState state,EncounterCheckTrigger trigger)
    {
        var p=state.World.Player;var cell=_content.ConnectedFieldMaps![p.MapId].CellAt(p.X,p.Y)!;var exploration=state.Exploration!;
        var process=exploration.EarlyEncounter??throw new InvalidDataException("early_encounter_missing");
        if(!_content.LandEncounters!.TryGetValue(p.MapId,out var table))return state with{Exploration=exploration with{EarlyEncounter=process with{PreviousMetatileBehavior=(ushort)cell.SourceBehavior}}};
        if(state.Party.IsEmpty||state.Party.All(p=>p.IsEgg||p.Pokemon.CurrentHp==0))throw new InvalidDataException("wild_encounter_live_party_required");
        var lead=state.Party[0];
        var check=new EarlyLandEncounterCheck(p.MapId,trigger,(byte)cell.SourceBehavior,(_content.MtMoonEnabled?_content.Maps[p.MapId].SourceEncounterTypeAt(p.X,p.Y)==1:cell.SourceBehavior==2)?EncounterTileType.Land:EncounterTileType.None);
        var profile=new EarlyLandEncounterProfile(RulesVersion:_content.CeladonApproachEnabled?EarlyLandEncounterRules.CeladonApproachRulesVersion:_content.RockTunnelEnabled?EarlyLandEncounterRules.RockTunnelRulesVersion:_content.Route11Enabled?EarlyLandEncounterRules.Route11RulesVersion:_content.SsAnneEnabled?EarlyLandEncounterRules.SsAnneRulesVersion:_content.VermilionEnabled?EarlyLandEncounterRules.VermilionRulesVersion:_content.BillEnabled?EarlyLandEncounterRules.BillRulesVersion:_content.MtMoonEnabled?EarlyLandEncounterRules.MtMoonRulesVersion:_content.Route3Enabled?EarlyLandEncounterRules.Route3RulesVersion:_content.Route22Enabled?EarlyLandEncounterRules.Route22RulesVersion:EarlyLandEncounterRules.RulesVersion,LeadAbility:lead.Pokemon.AbilityId,LeadHeldItem:lead.HeldItem);
        var result=_content.RepelEnabled
            ?EarlyRepelRules.CheckLandEncounter(process,table,check,profile with{RepelSteps=checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable))},state.Party.Select(mon=>new EarlyRepelPartyMember(mon.Pokemon.CurrentHp,mon.Pokemon.Level,mon.IsEgg)).ToImmutableArray(),state.World.Rng)
            :EarlyLandEncounterRules.Check(process,table,check,profile,state.World.Rng);
        var next=state with{Exploration=exploration with{EarlyEncounter=result.State},World=state.World with{Rng=result.GlobalRng}};
        return result.Candidate is null?next:BeginWildBattle(next);
    }
}
