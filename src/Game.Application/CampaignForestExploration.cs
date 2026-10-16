using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Items;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private CampaignExploration ResetEncounterModifiers(CampaignExploration state)=>state with{EarlyEncounter=EarlyLandEncounterRules.ResetRateModifiers(state.EarlyEncounter??throw new InvalidDataException("early_encounter_missing"))};
    private bool WildOriginSupported(int id,int level,string? region=null)=>SurfOriginSupported(id,level,region)||id==129&&_content.OldRodEncounters!.Values.Any(t=>!IsSourceTableMap(t.Map)&&!IsSeafoamMap(t.Map)&&t.Map.Value!=PowerPlantMap&&(!SevenIslandMap(t.Map))&&(!CeruleanCaveEncounterTables.IsCave(t.Map))&&(!FiveIslandEncounterTables.FishingSource.Any(r=>r.Map==t.Map))&&(!SixIslandEncounterTables.FishingSource.Any(r=>r.Map==t.Map))&&(!FourIslandEncounterTables.FishingSource.Any(r=>r.Map==t.Map))&&(!SeviiRubyEncounterTables.FishingSource.Any(r=>r.Map==t.Map))&&(region is null||_content.MapDefinition(t.Map).RegionMapSection==region)&&t.Slots.Any(s=>level>=s.MinimumLevel&&level<=s.MaximumLevel))||(_content.LandEncounters!.Values.Any(t=>!IsSourceTableMap(t.Map)&&!IsSeafoamMap(t.Map)&&t.Map.Value!=PowerPlantMap&&(!SevenIslandMap(t.Map))&&(!CeruleanCaveEncounterTables.IsCave(t.Map))&&(!FiveIslandEncounterTables.Land.Any(r=>r.Map==t.Map))&&(!SixIslandEncounterTables.Land.Any(r=>r.Map==t.Map))&&(!FourIslandEncounterTables.Land.Any(r=>r.Map==t.Map))&&(!SeviiRubyEncounterTables.Land.Any(r=>r.Map==t.Map))&&(region is null||_content.MapDefinition(t.Map).RegionMapSection==region)&&t.Slots.Any(s=>_content.PokemonContent!.Catalog.Species[s.Species].SourceSpeciesId==id&&level>=s.MinimumLevel&&level<=s.MaximumLevel)));
    private CampaignState CheckEarlyLandEncounter(CampaignState state,EncounterCheckTrigger trigger)
    {
        var p=state.World.Player;var cell=EditedFieldMaps(state)[p.MapId].CellAt(p.X,p.Y)!;var exploration=state.Exploration!;
        var process=exploration.EarlyEncounter??throw new InvalidDataException("early_encounter_missing");
        if(SevenIslandEncounterTables.IsChamber(p.MapId)&&!state.Script.Flags.GetValueOrDefault("FLAG_SYS_UNLOCKED_TANOBY_RUINS")||!_content.LandEncounters!.TryGetValue(p.MapId,out var table))return state with{Exploration=exploration with{EarlyEncounter=process with{PreviousMetatileBehavior=(ushort)cell.SourceBehavior}}};
        if(state.Party.IsEmpty||state.Party.All(p=>p.IsEgg||p.Pokemon.CurrentHp==0))throw new InvalidDataException("wild_encounter_live_party_required");
        var check=new EarlyLandEncounterCheck(p.MapId,trigger,(byte)cell.SourceBehavior,(FieldEncounterTypeAt(state,p.X,p.Y)==1)?EncounterTileType.Land:EncounterTileType.None);
        var result=EarlyLandEncounterRules.Check(_content.GamePokemon!,process,table,check,state.Party,
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)),state.World.Rng,
            state.Roamer??Game.Domain.Roaming.RoamerState.Empty,CelioSourceMap(p.MapId),extendedRepel:true,bicycle:state.IsCycling,ordinary:OrdinaryLandDefinition(p.MapId));
        var next=state with{Exploration=exploration with{EarlyEncounter=result.State},World=state.World with{Rng=result.GlobalRng}};
        if(result.RoamerCandidate is {} roamer)
        {
            next=BeginCelioRoamerBattle(next,roamer,Game.Domain.Roaming.RoamerEncounterMethod.Land,result.RandomTrace);
            return next with{Exploration=next.Exploration! with{EarlyEncounter=CelioEncounterRules.CompleteRoamerCandidate(result,table,OrdinaryLandDefinition(p.MapId))}};
        }
        return result.Candidate is null?next:state.SafariVisit is {Active:true}?GenerateSafariBattle(next):BeginWildBattle(next);
    }
}
