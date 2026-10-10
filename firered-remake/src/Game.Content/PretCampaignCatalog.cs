using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Items;
using Game.Domain.Encounters;
namespace Game.Content;
/// <summary>Fixed five-map source bundle with an explicit authenticated Viridian opt-in. Unknown operations remain faults.</summary>
public sealed partial class PretCampaignCatalog:ICampaignCatalog
{
    private readonly PretStoryCompilation _scripts;
    private readonly ImmutableDictionary<MapId,FieldMapDefinition> _definitions;
    private readonly ImmutableDictionary<string,string> _text;
    public string ContentVersion{get;}
    public CampaignPokemonContent? PokemonContent{get;}
    public CampaignBattleContent? BattleContent{get;}
    public OpeningItemsCatalog? OpeningItems{get;}
    public QuestItemsCatalog? QuestItems{get;}
    public bool CaptureEnabled{get;}
    public PretViridianCenter? ViridianCenter{get;}
    public bool ViridianCenterEnabled=>ViridianCenter is not null;
    public PretOldManTutorialContent? OldManTutorial{get;}
    public bool OldManTutorialEnabled=>OldManTutorial is not null;
    public bool PartyTrainingEnabled{get;}
    public PretForestRegion? ForestRegion{get;}
    public PretForestPokemon? ForestPokemon{get;}
    public bool ForestEnabled=>ForestRegion is not null;
    public bool ForestTrainersEnabled=>ForestTrainers is not null;
    public PretPewterRegion? PewterRegion{get;}
    public bool PewterEnabled=>PewterRegion is not null;
    public bool EarlyStarterEnabled{get;}
    public bool EarlyPartyEnabled{get;}
    public PretPokemonStorageSource? PokemonStorageSource{get;}
    public bool PokemonStorageEnabled=>PokemonStorageSource is not null;
    public PretEarlyPickups? EarlyPickups{get;}
    public ImmutableArray<FieldPickupDefinition> FieldPickups{get;}
    public PretEarlyRepelDocument? EarlyRepel{get;}
    public bool RepelEnabled=>EarlyRepel is not null;
    public PretTownMap? TownMap{get;}
    public bool TownMapEnabled=>TownMap is not null;
    public PretOakRating? OakRating{get;}
    /// <summary>Authenticated immutable metadata shared by Oak, Route22 and presentation in this composition.</summary>
    public PretPokedexMenu? PokedexMenu{get;}
    public bool OakRatingEnabled=>OakRating is not null;
    public PretRoute22Region? Route22Region{get;}
    public PretRoute22Pokemon? Route22Pokemon{get;}
    public Route22TrainerCatalog? Route22Trainers{get;}
    public bool Route22Enabled=>Route22Region is not null;
    public PretRoute3Region? Route3Region{get;}
    public PretRoute3Pokemon? Route3Pokemon{get;}
    public Route3TrainerCatalog? Route3Trainers{get;}
    public bool Route3Enabled=>Route3Region is not null;
    public PretMagikarpContract? MagikarpContract{get;}
    public bool MagikarpEnabled=>MagikarpContract is not null;
    public PretMtMoonRegion? MtMoonRegion{get;}
    public PretMtMoonPokemon? MtMoonPokemon{get;}
    public MtMoonTrainerCatalog? MtMoonTrainers{get;}
    public bool MtMoonEnabled=>MtMoonRegion is not null;
    public PretCeruleanRegion? CeruleanRegion{get;}
    public PretCeruleanPokemon? CeruleanPokemon{get;}
    public CeruleanTrainerCatalog? CeruleanTrainers{get;}
    public bool CeruleanEnabled=>CeruleanRegion is not null;
    public PretCityServices? CityServices{get;}
    public bool CityServicesEnabled=>CityServices is not null;
    public PretOldRod? OldRod{get;}
    public IReadOnlyDictionary<MapId,OldRodEncounterTable>? OldRodEncounters=>OldRod?.Tables;
    public ImmutableArray<PewterMachineItem> CityServiceItems=>CityServices?.Items??[];
    public PretVermilionGymRegion? VermilionGymRegion{get;}
    public VermilionGymTrainerCatalog? VermilionGymTrainers{get;}
    public bool VermilionGymEnabled=>VermilionGymRegion is not null;
    public PretSsAnneRegion? SsAnneRegion{get;}
    public PretSsAnnePokemon? SsAnnePokemon{get;}
    public SsAnneTrainerCatalog? SsAnneTrainers{get;}
    public bool SsAnneEnabled=>SsAnneRegion is not null;
    public PretVermilionRegion? VermilionRegion{get;}
    public PretVermilionPokemon? VermilionPokemon{get;}
    public VermilionTrainerCatalog? VermilionTrainers{get;}
    public bool VermilionEnabled=>VermilionRegion is not null;
    public PretBillRegion? BillRegion{get;}
    public PretBillPokemon? BillPokemon{get;}
    public PretBillItemsDocument? BillItems{get;}
    public BillTrainerCatalog? BillTrainers{get;}
    public bool BillEnabled=>BillRegion is not null;
    public PretMtMoonItemsDocument? MtMoonItems{get;}
    public CampaignOakRatingProjection EvaluateOakRating(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)
    {
        var result=(OakRating??throw new InvalidDataException("oak_rating_not_enabled")).EvaluateSpeciesIds(seenSpecies,caughtSpecies,PokedexMenu!,new(HasPokedex:true));
        return new(result.Kanto.Seen,result.Kanto.Caught,result.Rating.TextSymbol,result.Rating.KantoComplete);
    }
    public PretEarlyShopDocument? EarlyShopDocument{get;}
    public EarlyShopCatalog? EarlyShop=>EarlyShopDocument?.Catalog;
    public ForestTrainerCatalog? ForestTrainers{get;}
    public PewterTrainerCatalog? PewterTrainers{get;}
    public PretPewterPokemon? PewterPokemon{get;}
    public PewterMachineCatalog? PewterMachines{get;}
    public IReadOnlyDictionary<MapId,EarlyLandEncounterTable>? LandEncounters{get;}
    public PretMartMetatiles? MartMetatiles{get;}
    public IReadOnlyDictionary<int,FieldMetatileDefinition>? MetatileDefinitions{get;}
    public IReadOnlyDictionary<MapId,IFieldMap> Maps{get;}
    public IReadOnlyDictionary<MapId,PretWorldMap> SourceMaps{get;}
    public PretStoryCompilation ScriptCompilation=>_scripts;
    public bool RegionEnabled{get;}
    public PretViridianRegion? ViridianRegion{get;}
    public IReadOnlyDictionary<MapId,ConnectedFieldMap>? ConnectedFieldMaps{get;}
    private PretCampaignCatalog(ImmutableDictionary<MapId,PretWorldMap> maps,PretStoryCompilation scripts,ImmutableDictionary<string,string> text,string version,CampaignPokemonContent? pokemonContent,CampaignBattleContent? battleContent,OpeningItemsCatalog? openingItems,PretViridianRegion? region,QuestItemsCatalog? questItems=null,PretMartMetatiles? metatiles=null,bool captureEnabled=false,PretViridianCenter? center=null,PretOldManTutorialContent? oldMan=null,bool partyTraining=false,PretForestRegion? forest=null,PretForestPokemon? forestPokemon=null,bool forestTrainers=false,PretPewterRegion? pewter=null,PretPewterPokemon? pewterPokemon=null,PewterMachineCatalog? machines=null,PretEarlyShopDocument? earlyShop=null,bool earlyStarter=false,bool earlyParty=false,PretPokemonStorageSource? pokemonStorage=null,PretEarlyPickups? fieldPickups=null,PretEarlyRepelDocument? repel=null,PretTownMap? townMap=null,PretOakRating? oakRating=null,PretPokedexMenu? oakPokedex=null,PretRoute22Region? route22=null,PretRoute22Pokemon? route22Pokemon=null,PretRoute3Region? route3=null,PretRoute3Pokemon? route3Pokemon=null,PretMagikarpContract? magikarp=null,PretMtMoonRegion? mtMoon=null,PretMtMoonPokemon? mtMoonPokemon=null,PretMtMoonItemsDocument? mtMoonItems=null,PretCeruleanRegion? cerulean=null,PretCeruleanPokemon? ceruleanPokemon=null,PretBillRegion? bill=null,PretBillPokemon? billPokemon=null,PretBillItemsDocument? billItems=null,BillTrainerCatalog? billTrainers=null,PretVermilionRegion? vermilion=null,PretVermilionPokemon? vermilionPokemon=null,VermilionTrainerCatalog? vermilionTrainers=null,PretSsAnneRegion? ssAnne=null,PretSsAnnePokemon? ssAnnePokemon=null,SsAnneTrainerCatalog? ssAnneTrainers=null,PretSsAnneItemsDocument? ssAnneItems=null,PretVermilionGymRegion? vermilionGym=null,VermilionGymTrainerCatalog? vermilionGymTrainers=null,PretCityServices? cityServices=null,PretOldRod? oldRod=null,PretRoute11Region? route11=null,PretRoute11Pokemon? route11Pokemon=null,PretRoute11ItemsDocument? route11Items=null,PretRoute11Encounters? route11Encounters=null,Route11TrainerCatalog? route11Trainers=null,PretRockTunnelRegion? rockTunnel=null,PretRockTunnelPokemon? rockTunnelPokemon=null,PretRockTunnelItemsDocument? rockTunnelItems=null,PretRockTunnelEncounters? rockTunnelEncounters=null,RockTunnelTrainerCatalog? rockTunnelTrainers=null,PretCeladonApproachRegion? celadonApproach=null,PretCeladonApproachPokemon? celadonApproachPokemon=null,PretCeladonApproachItemsDocument? celadonApproachItems=null,PretCeladonApproachEncounters? celadonApproachEncounters=null,CeladonApproachTrainerCatalog? celadonApproachTrainers=null,PretCeladonGymRegion? celadonGym=null,PretCeladonGymPokemon? celadonGymPokemon=null,PretCeladonGymItemsDocument? celadonGymItems=null,CeladonGymTrainerCatalog? celadonGymTrainers=null,PretRocketHideoutRegion? rocketHideout=null,PretRocketHideoutPokemon? rocketHideoutPokemon=null,PretRocketHideoutItemsDocument? rocketHideoutItems=null,PretRocketHideoutPickupsDocument? rocketHideoutPickups=null,RocketHideoutTrainerCatalog? rocketHideoutTrainers=null,PretPokemonTowerRegion? pokemonTower=null,PretPokemonTowerPokemon? pokemonTowerPokemon=null,PretPokemonTowerItemsDocument? pokemonTowerItems=null,PretPokemonTowerPickupsDocument? pokemonTowerPickups=null,PokemonTowerTrainerCatalog? pokemonTowerTrainers=null,PretSnorlaxRegion? snorlax=null,PretSnorlaxPokemon? snorlaxPokemon=null,PretSnorlaxArt? snorlaxArt=null,bool regionalFlavor=false)
    {
        RegionalFlavorEnabled=regionalFlavor;
        SnorlaxRegion=snorlax;SnorlaxPokemon=snorlaxPokemon;SnorlaxArt=snorlaxArt;
        PokemonTowerRegion=pokemonTower;PokemonTowerPokemon=pokemonTowerPokemon;PokemonTowerItems=pokemonTowerItems;PokemonTowerPickups=pokemonTowerPickups;PokemonTowerTrainers=pokemonTowerTrainers;
        RocketHideoutRegion=rocketHideout;RocketHideoutPokemon=rocketHideoutPokemon;RocketHideoutItems=rocketHideoutItems;RocketHideoutPickups=rocketHideoutPickups;RocketHideoutTrainers=rocketHideoutTrainers;
        CeladonGymRegion=celadonGym;CeladonGymPokemon=celadonGymPokemon;CeladonGymItems=celadonGymItems;CeladonGymTrainers=celadonGymTrainers;
        CeladonApproachRegion=celadonApproach;CeladonApproachPokemon=celadonApproachPokemon;CeladonApproachItems=celadonApproachItems;CeladonApproachEncounters=celadonApproachEncounters;CeladonApproachTrainers=celadonApproachTrainers;
        RockTunnelRegion=rockTunnel;RockTunnelPokemon=rockTunnelPokemon;RockTunnelItems=rockTunnelItems;RockTunnelEncounters=rockTunnelEncounters;RockTunnelTrainers=rockTunnelTrainers;
        Route11Region=route11;Route11Pokemon=route11Pokemon;Route11Items=route11Items;Route11Encounters=route11Encounters;Route11Trainers=route11Trainers;
        CityServices=cityServices;OldRod=oldRod;
        VermilionGymRegion=vermilionGym;VermilionGymTrainers=vermilionGymTrainers;
        SsAnneRegion=ssAnne;SsAnnePokemon=ssAnnePokemon;SsAnneTrainers=ssAnneTrainers;
        VermilionRegion=vermilion;VermilionPokemon=vermilionPokemon;VermilionTrainers=vermilionTrainers;
        BillRegion=bill;BillPokemon=billPokemon;BillItems=billItems;BillTrainers=billTrainers;
        CeruleanPokemon=ceruleanPokemon;CeruleanTrainers=cerulean is null?null:PretCeruleanTrainers.Load(cerulean,ceruleanPokemon!);CeruleanRegion=cerulean;MtMoonItems=mtMoonItems;MtMoonRegion=mtMoon;MtMoonPokemon=mtMoonPokemon;MtMoonTrainers=mtMoon is null?null:PretMtMoonTrainers.Load(mtMoon,mtMoonPokemon!);
        MagikarpContract=magikarp;Route3Region=route3;Route3Pokemon=route3Pokemon;Route3Trainers=route3 is null?null:PretRoute3Trainers.Load(route3,route3Pokemon!);
        _route4WesternPositions=Route4WesternPositions(route3);
        Route22Region=route22;Route22Pokemon=route22Pokemon;Route22Trainers=route22 is null?null:PretRoute22Trainers.Load(route22,route22Pokemon!);
        EarlyRepel=repel;TownMap=townMap;OakRating=oakRating;PokedexMenu=oakPokedex;
        SourceMaps=maps;Maps=maps.ToImmutableDictionary(x=>x.Key,x=>(IFieldMap)x.Value);_scripts=scripts;_text=text;ContentVersion=version;PokemonContent=pokemonContent;BattleContent=battleContent;OpeningItems=openingItems;
        ViridianRegion=region;RegionEnabled=region is not null;QuestItems=questItems;MartMetatiles=metatiles;CaptureEnabled=captureEnabled;ViridianCenter=center;OldManTutorial=oldMan;PartyTrainingEnabled=partyTraining;
        EarlyStarterEnabled=earlyStarter;EarlyPartyEnabled=earlyParty;PokemonStorageSource=pokemonStorage;EarlyPickups=fieldPickups;FieldPickups=fieldPickups is null?[]:fieldPickups.Pickups.Select(p=>new FieldPickupDefinition(p.Id,p.MapSymbol,p.Kind=="hiddenItem",p.SourceLocalId,p.X,p.Y,p.Elevation,p.Script,p.ItemSymbol,p.SourceItemId,new ItemId(p.ItemId),p.ItemName,p.Pocket,p.Quantity,p.Flag,p.SourceFlagId)).ToImmutableArray();EarlyShopDocument=earlyShop;PewterRegion=pewter;PewterPokemon=pewterPokemon;PewterMachines=machines;PewterTrainers=pewter is null?null:PretPewterTrainers.Load(pewter);ForestRegion=forest;ForestPokemon=forestPokemon;ForestTrainers=forestTrainers?PretForestTrainers.Load(forest??throw new InvalidDataException("forest_trainers_region_missing")):null;LandEncounters=forest is null?null:VermilionEncounterTables(forest,route22,route3,mtMoon,bill,vermilion);
        if(mtMoonItems is not null)FieldPickups=FieldPickups.AddRange(MtMoonPickupDefinitions(mtMoonItems,earlyShop!.Catalog,machines!,scripts));
        if(billItems is not null)FieldPickups=FieldPickups.AddRange(BillPickupDefinitions(billItems,machines!,scripts));
        if(ssAnneItems is not null)FieldPickups=FieldPickups.AddRange(SsAnnePickupDefinitions(ssAnneItems,machines!,earlyShop!.Catalog,scripts));
        if(route11Items is not null)FieldPickups=FieldPickups.AddRange(Route11PickupDefinitions(route11Items,scripts));
        if(route11Encounters is not null)LandEncounters=LandEncounters!.ToImmutableDictionary().AddRange(route11Encounters.Tables);
        if(rockTunnelItems is not null)FieldPickups=FieldPickups.AddRange(RockTunnelPickupDefinitions(rockTunnelItems,earlyShop!.Catalog,machines!,scripts));
        if(rockTunnelEncounters is not null)LandEncounters=LandEncounters!.ToImmutableDictionary().AddRange(rockTunnelEncounters.Tables);
        if(celadonApproachItems is not null)FieldPickups=FieldPickups.AddRange(CeladonApproachPickupDefinitions(celadonApproachItems,earlyShop!.Catalog,machines!,scripts));
        if(celadonApproachEncounters is not null)LandEncounters=LandEncounters!.ToImmutableDictionary().AddRange(celadonApproachEncounters.Tables);
        if(rocketHideoutPickups is not null)FieldPickups=FieldPickups.AddRange(RocketHideoutPickupDefinitions(rocketHideoutPickups,earlyShop!.Catalog,machines!,rocketHideout!,scripts));
        if(pokemonTower is not null)LandEncounters=LandEncounters!.ToImmutableDictionary().AddRange(PokemonTowerEncounterTables(pokemonTower));
        if(pokemonTowerPickups is not null)FieldPickups=FieldPickups.AddRange(PokemonTowerPickupDefinitions(pokemonTowerPickups,earlyShop!.Catalog,scripts));
        var metatileDefinitions=metatiles?.Definitions.ToDictionary(x=>x.Key,x=>new FieldMetatileDefinition(x.Key,x.Value.Behavior,x.Value.Terrain,x.Value.EncounterType,x.Value.LayerType));
        if(vermilionGym is not null)
        {
            metatileDefinitions??=[];
            // Source metatile IDs are local to a tileset. Preserve the Mart's 0x2BF
            // definition; the map-aware lookup supplies the Gym's different layer.
            foreach(var (id,tile) in vermilionGym.BeamDefinitions)
                metatileDefinitions.TryAdd(id,new(id,tile.Behavior,tile.Terrain,tile.EncounterType,tile.LayerType));
        }
        MetatileDefinitions=metatileDefinitions;
        ConnectedFieldMaps=RegionEnabled?maps.ToImmutableDictionary(x=>x.Key,x=>snorlax?.NewMaps.ContainsKey(x.Key)==true?PretSnorlaxRegion.ConnectedMap(x.Value):pokemonTower?.NewMaps.ContainsKey(x.Key)==true?PretPokemonTowerRegion.ConnectedMap(x.Value):vermilionGym is not null&&x.Key.Value=="map.vermilion-city-gym"?PretVermilionGymRegion.ConnectedMap(x.Value):rocketHideout is not null?PretRocketHideoutRegion.ConnectedMap(x.Value):celadonGym is not null?PretCeladonGymRegion.ConnectedMap(x.Value):celadonApproach is not null?PretCeladonApproachRegion.ConnectedMap(x.Value):rockTunnel is not null?PretRockTunnelRegion.ConnectedMap(x.Value):route11 is not null?PretRoute11Region.ConnectedMap(x.Value):ssAnne is not null?PretSsAnneRegion.ConnectedMap(x.Value):vermilion is not null?PretVermilionRegion.ConnectedMap(x.Value):bill is not null?PretBillRegion.ConnectedMap(x.Value):cerulean is not null?PretCeruleanRegion.ConnectedMap(x.Value):mtMoon is not null?PretMtMoonRegion.ConnectedMap(x.Value):route3 is not null?PretRoute3Region.ConnectedMap(x.Value):route22 is not null?PretRoute22Region.ConnectedMap(x.Value):pewter is not null?PretPewterRegion.ConnectedMap(x.Value):forest is null?PretViridianRegion.ConnectedMap(x.Value):PretForestRegion.ConnectedMap(x.Value)):null;
        _definitions=maps.ToImmutableDictionary(x=>x.Key,x=>Definition(x.Value));
    }
    public static PretCampaignCatalog Load(string projectRoot,bool includeStarterEvents=false,bool includeFirstBattle=false,bool includeOpeningItems=false,bool includeViridianRegion=false,bool includeParcelQuest=false,bool includeCapture=false,bool includeViridianCenter=false,bool includeOldManTutorial=false,bool includePartyTraining=false,bool includeForest=false,bool includeForestTrainers=false,bool includePewter=false,bool includeEarlyShop=false,bool includeEarlyStarter=false,bool includeEarlyParty=false,bool includePokemonStorage=false,bool includeFieldPickups=false,bool includeRepel=false,bool includeTownMap=false,bool includeOakRating=false,bool includeRoute22=false,bool useCompactMapData=false,bool includeRoute3=false,bool includeMagikarp=false,bool includeMtMoon=false,bool includeCerulean=false,bool includeBill=false,bool includeVermilion=false,bool includeSsAnne=false,bool includeVermilionGym=false,bool includeCityServices=false,bool includeRoute11=false,bool includeRockTunnel=false,bool includeCeladonApproach=false,bool includeCeladonGym=false,bool includeRocketHideout=false,bool includePokemonTower=false,bool includeSnorlax=false,bool includeRegionalFlavor=false)
    {
        if(includeRegionalFlavor&&!includePokemonTower)throw new ArgumentException("Regional flavor requires the complete Pokemon Tower profile.");
        if(includeSnorlax&&!includePokemonTower)throw new ArgumentException("Snorlax requires the complete Pokemon Tower profile.");
        if(includePokemonTower&&!includeRocketHideout)throw new ArgumentException("Pokemon Tower requires the complete Rocket Hideout profile.");
        if(includeRocketHideout&&!includeCeladonGym)throw new ArgumentException("Rocket Hideout requires the complete Celadon Gym profile.");
        if(includeCeladonGym&&!includeCeladonApproach)throw new ArgumentException("Celadon Gym requires the complete Celadon approach profile.");
        if(includeCeladonApproach&&!includeRockTunnel)throw new ArgumentException("Celadon approach requires the complete RockTunnel profile.");
        if(includeRockTunnel&&!includeRoute11)throw new ArgumentException("RockTunnel requires the complete Route11 profile.");
        if(includeRoute11&&!includeCityServices)throw new ArgumentException("Route11 requires the complete City services profile.");
        if(includeCityServices&&!includeVermilionGym)throw new ArgumentException("City services require the complete Vermilion Gym profile.");
        if(includeVermilionGym&&!includeSsAnne)throw new ArgumentException("Vermilion Gym requires the complete S.S. Anne profile.");
        if(includeSsAnne&&!includeVermilion)throw new ArgumentException("S.S. Anne requires the complete Vermilion profile.");
        if(includeVermilion&&!includeBill)throw new ArgumentException("Vermilion requires the complete Bill profile.");
        if(includeBill&&!includeCerulean)throw new ArgumentException("Bill requires the complete Cerulean profile.");
        if(includeCerulean&&!includeMtMoon)throw new ArgumentException("Cerulean requires the complete MtMoon profile.");
        if(includeMtMoon&&!includeMagikarp)throw new ArgumentException("MtMoon requires the complete Magikarp profile.");
        if(includeMagikarp&&!includeRoute3)throw new ArgumentException("Magikarp requires the complete Route3 profile.");
        var magikarp=includeMagikarp?PretMagikarpContract.Load(projectRoot):null;
        if(includeRoute3&&!includeRoute22)throw new ArgumentException("Route3 requires the complete Route22 profile.");
        var runtimeMaps=useCompactMapData?PretRuntimeMapCatalog.Load(Path.Combine(projectRoot,includeRocketHideout?PretRocketHideoutRuntimeMaps.DirectoryName:includeCeladonGym?PretCeladonGymRuntimeMaps.DirectoryName:includeCeladonApproach?PretCeladonApproachRuntimeMaps.DirectoryName:includeRockTunnel?PretRockTunnelRuntimeMaps.DirectoryName:includeRoute11?PretRoute11RegionRuntimeMaps.DirectoryName:includeCityServices?PretCityServicesRuntimeMaps.DirectoryName:includeVermilionGym?PretVermilionGymRuntimeMaps.DirectoryName:includeSsAnne?"content/generated/runtime-ssanne-maps":includeVermilion?"content/generated/runtime-vermilion-maps":includeBill?"content/generated/bill-region/runtime-maps":includeCerulean?"content/generated/cerulean-region/runtime-maps":includeMtMoon?"content/generated/mtmoon-region/runtime-maps":includeRoute3?"content/generated/runtime-maps-route3":"content/generated/runtime-maps"),includeRocketHideout?PretRocketHideoutRuntimeMaps.ManifestSha256:includeCeladonGym?PretCeladonGymRuntimeMaps.ManifestSha256:includeCeladonApproach?PretCeladonApproachRuntimeMaps.ManifestSha256:includeRockTunnel?PretRockTunnelRuntimeMaps.ManifestSha256:includeRoute11?PretRoute11RegionRuntimeMaps.ManifestSha256:includeCityServices?PretCityServicesRuntimeMaps.ManifestSha256:includeVermilionGym?PretVermilionGymRuntimeMaps.ManifestSha256:includeSsAnne?PretSsAnneRuntimeMaps.ManifestSha256:includeVermilion?PretVermilionRegion.RuntimeManifestSha256:includeBill?PretRuntimeMapCatalog.BillManifestSha256:includeCerulean?PretRuntimeMapCatalog.CeruleanManifestSha256:includeMtMoon?PretRuntimeMapCatalog.MtMoonManifestSha256:includeRoute3?PretRuntimeMapCatalog.Route3ManifestSha256:PretRuntimeMapCatalog.PinnedManifestSha256):null;
        if(includeRoute22&&!includeOakRating)throw new ArgumentException("Route22 requires the complete Oak rating profile.");
        if(includeOakRating&&!includeTownMap)throw new ArgumentException("Oak rating requires the complete Town Map profile.");
        var route22=includeRoute22?PretRoute22Region.Load(projectRoot,runtimeMaps):null;
        var route3=includeRoute3?PretRoute3Region.Load(projectRoot,runtimeMaps):null;
        var oakPokedex=includeOakRating?PretPokedexMenu.Load(projectRoot):null;
        var route22Pokemon=includeRoute22?PretRoute22Pokemon.Load(projectRoot,includeSourceCryWaves:false,pokedexMenu:oakPokedex):null;
        var route3Pokemon=includeRoute3?PretRoute3Pokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var ceruleanPokemon=includeCerulean?PretCeruleanPokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var pokemonTower=includePokemonTower?PretPokemonTowerRegion.Load(projectRoot,PretPokemonTowerRuntimeMaps.Load(projectRoot)):null;
        PretPokemonTowerPokemon? pokemonTowerPokemon=null;
        var snorlax=includeSnorlax?PretSnorlaxRegion.Load(projectRoot):null;
        PretSnorlaxPokemon? snorlaxPokemon=null;
        var snorlaxArt=includeSnorlax?PretSnorlaxArt.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var pokemonTowerItems=includePokemonTower?PretPokemonTowerItems.LoadProject(projectRoot):null;
        var pokemonTowerPickups=includePokemonTower?PretPokemonTowerItems.LoadPickupsProject(projectRoot):null;
        var pokemonTowerTrainers=includePokemonTower?PretPokemonTowerTrainers.Load(projectRoot):null;
        var rocketHideout=includeRocketHideout?PretRocketHideoutRegion.Load(projectRoot,runtimeMaps):null;
        var rocketHideoutPokemon=includeRocketHideout?PretRocketHideoutPokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var rocketHideoutItems=includeRocketHideout?PretRocketHideoutItems.LoadProject(projectRoot):null;
        var rocketHideoutPickups=includeRocketHideout?PretRocketHideoutItems.LoadPickupsProject(projectRoot):null;
        var rocketHideoutTrainers=includeRocketHideout?PretRocketHideoutTrainers.Load(projectRoot):null;
        var celadonGym=includeCeladonGym?PretCeladonGymRegion.Load(projectRoot,runtimeMaps):null;
        var celadonGymPokemon=includeCeladonGym?PretCeladonGymPokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var celadonGymItems=includeCeladonGym?PretCeladonGymItems.LoadProject(projectRoot):null;
        var celadonGymTrainers=includeCeladonGym?PretCeladonGymTrainers.Load(projectRoot):null;
        var celadonApproach=includeCeladonApproach?PretCeladonApproachRegion.Load(projectRoot,runtimeMaps):null;
        var celadonApproachPokemon=includeCeladonApproach?PretCeladonApproachPokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var celadonApproachItems=includeCeladonApproach?PretCeladonApproachItems.LoadProject(projectRoot):null;
        var celadonApproachEncounters=includeCeladonApproach?PretCeladonApproachEncounters.Load(projectRoot):null;
        var celadonApproachTrainers=includeCeladonApproach?PretCeladonApproachTrainers.Load(projectRoot):null;
        var rockTunnel=includeRockTunnel?PretRockTunnelRegion.Load(projectRoot,runtimeMaps):null;
        var rockTunnelPokemon=includeRockTunnel?PretRockTunnelPokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var rockTunnelItems=includeRockTunnel?PretRockTunnelItems.LoadProject(projectRoot):null;
        var rockTunnelEncounters=includeRockTunnel?PretRockTunnelEncounters.Load(projectRoot):null;
        var rockTunnelTrainers=includeRockTunnel?PretRockTunnelTrainers.Load(projectRoot):null;
        var route11=includeRoute11?PretRoute11Region.Load(projectRoot,runtimeMaps):null;
        var route11Pokemon=includeRoute11?PretRoute11Pokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var route11Items=includeRoute11?PretRoute11Items.LoadProject(projectRoot):null;
        var route11Encounters=includeRoute11?PretRoute11Encounters.Load(projectRoot):null;
        var route11Trainers=includeRoute11?PretRoute11Trainers.Load(projectRoot):null;
        var cityServices=includeCityServices?PretCityServices.Load(projectRoot,runtimeMaps):null;
        var oldRod=includeCityServices?PretOldRod.Load(projectRoot):null;
        var vermilionGym=includeVermilionGym?PretVermilionGymRegion.Load(projectRoot,runtimeMaps):null;
        var vermilionGymTrainers=includeVermilionGym?PretVermilionGymTrainers.Load(projectRoot):null;
        var vermilionGymAssets=includeVermilionGym?PretVermilionGymAssets.Load(projectRoot):null;
        var ssAnne=includeSsAnne?PretSsAnneRegion.Load(projectRoot,runtimeMaps):null;
        var ssAnnePokemon=includeSsAnne?PretSsAnnePokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var ssAnneTrainers=includeSsAnne?PretSsAnneTrainers.Load(projectRoot):null;
        var ssAnneItems=includeSsAnne?PretSsAnneItems.LoadProject(projectRoot):null;
        var vermilionPokemon=includeVermilion?PretVermilionPokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var vermilion=includeVermilion?PretVermilionRegion.Load(projectRoot,runtimeMaps):null;
        var vermilionTrainers=includeVermilion?PretVermilionTrainers.Load(projectRoot):null;
        var billPokemon=includeBill?PretBillPokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var bill=includeBill?PretBillRegion.Load(projectRoot,runtimeMaps):null;
        var billItems=includeBill?PretBillItems.LoadProject(projectRoot):null;
        var billTrainers=includeBill?PretBillTrainers.Load(projectRoot):null;
        var cerulean=includeCerulean?PretCeruleanRegion.Load(projectRoot,runtimeMaps):null;
        var mtMoon=includeMtMoon?PretMtMoonRegion.Load(projectRoot,runtimeMaps):null;
        var mtMoonPokemon=includeMtMoon?PretMtMoonPokemon.Load(projectRoot,pokedexMenu:oakPokedex):null;
        var oakRating=includeOakRating?PretOakRating.Load(projectRoot):null;
        if(includeTownMap&&!includeRepel)throw new ArgumentException("Town Map requires the complete Repel profile.");
        var townMap=includeTownMap?PretTownMap.Load(projectRoot):null;
        if(includeRepel&&!includeFieldPickups)throw new ArgumentException("Repel requires the complete field-pickup profile.");
        if(includeFieldPickups&&!includePokemonStorage)throw new ArgumentException("Field pickups require the complete storage profile.");
        if(includePokemonStorage&&!includeEarlyParty)throw new ArgumentException("Pokemon storage requires the complete early party profile.");
        if(includeEarlyParty&&!includeEarlyStarter)throw new ArgumentException("Early party moves require the early starter profile.");
        if(includeEarlyStarter&&(!includePewter||!includeEarlyShop))throw new ArgumentException("Early starter moves require the complete Pewter/shop profile.");
        if(includeForest&&!includePartyTraining)throw new ArgumentException("Forest requires the party training profile.");
        if(includePartyTraining&&!includeOldManTutorial)throw new ArgumentException("Party training requires the complete Old Man profile.");
        if(includeFirstBattle&&!includeStarterEvents)throw new ArgumentException("First battle requires starter content.");
        if(includeOldManTutorial&&!includeViridianCenter)throw new ArgumentException("Old man tutorial requires the center/captured-party profile.");
        if(includeViridianCenter&&!includeCapture)throw new ArgumentException("Viridian Center requires the captured-party profile.");
        var maps=PretWorldCatalog.Load(Path.Combine(projectRoot,"content/generated"),runtimeMaps);
        if(includeCapture&&!includeParcelQuest)throw new ArgumentException("Capture requires quest item content.");
        if(includeParcelQuest&&!includeViridianRegion)throw new ArgumentException("Parcel quest requires region content.");
        if(includeViridianRegion&&!includeStarterEvents)throw new ArgumentException("Viridian region requires starter Pokemon content.");
        var region=includeViridianRegion?PretViridianRegion.Load(projectRoot,runtimeMaps):null;
        if(region is not null)maps=maps.AddRange(region.NewMaps);
        var center=includeViridianCenter?PretViridianCenter.Load(projectRoot,runtimeMaps):null;
        if(center is not null)maps=maps.Add(center.Map.Id,center.Map);
        if(includeForestTrainers&&!includeForest)throw new ArgumentException("Forest trainers require the forest profile.");
        var forest=includeForest?PretForestRegion.Load(projectRoot,runtimeMaps):null;
        if(forest is not null)maps=maps.AddRange(forest.NewMaps);
        if(includePewter&&!includeForestTrainers)throw new ArgumentException("Pewter requires the ordinary trainer profile.");
        var pewter=includePewter?PretPewterRegion.Load(projectRoot,runtimeMaps):null;
        if(pewter is not null)maps=maps.AddRange(pewter.NewMaps);
        if(route22 is not null)maps=maps.AddRange(route22.NewMaps);
        if(route3 is not null)maps=maps.AddRange(route3.NewMaps);
        if(mtMoon is not null)maps=maps.AddRange(mtMoon.NewMaps);
        if(cerulean is not null)maps=maps.AddRange(cerulean.NewMaps);
        if(bill is not null)maps=maps.AddRange(bill.NewMaps);
        if(vermilion is not null)maps=maps.AddRange(vermilion.NewMaps);
        if(ssAnne is not null)maps=maps.AddRange(ssAnne.NewMaps);
        if(vermilionGym is not null)maps=maps.AddRange(vermilionGym.NewMaps);
        if(cityServices is not null)maps=maps.AddRange(cityServices.NewMaps);
        if(route11 is not null)maps=maps.AddRange(route11.NewMaps);
        if(rockTunnel is not null)maps=maps.AddRange(rockTunnel.NewMaps);
        if(celadonApproach is not null)maps=maps.AddRange(celadonApproach.NewMaps);
        if(celadonGym is not null)maps=maps.AddRange(celadonGym.NewMaps);
        if(rocketHideout is not null)maps=maps.AddRange(rocketHideout.NewMaps);
        if(pokemonTower is not null)maps=maps.AddRange(pokemonTower.NewMaps);
        if(snorlax is not null)maps=maps.AddRange(snorlax.NewMaps);
        var sources=new List<PretSourceScriptFile>();var textFiles=new List<string>();var constants=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var map in maps.Values.OrderBy(x=>x.Id.Value,StringComparer.Ordinal))
        {
            string path=map.SourceRef.Path.Replace("map.json","scripts.inc",StringComparison.Ordinal);var f=map.Metadata.Files.Single(x=>x.Path==path);
            sources.Add(new(path,map.ScriptSource,new(SourceKind.Pret,path,map.SourceMapSymbol,map.Metadata.Repository,map.Metadata.Commit,f.Sha256)));
            if(map.Metadata.Files.Any(x=>x.Path==map.SourceRef.Path.Replace("map.json","text.inc",StringComparison.Ordinal)))textFiles.Add(map.TextSource);
        }
        if(townMap is not null)
        {
            // The existing map interpreter executes the same authenticated original script;
            // never replace Daisy's program with a hand-written reward shortcut.
            const string daisyPath="data/maps/PalletTown_RivalsHouse/scripts.inc";
            var source=sources.Single(s=>s.Path==daisyPath);
            if(source.SourceRef.Sha256!=townMap.SourceFiles.Single(s=>s.Path==daisyPath).Sha256)
                throw new InvalidDataException("town_map_daisy_source_conflict");
        }
        using var l=JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot,"scripts/fetch-story.lock.json")));
        var hashes=new List<string>{PretWorldMap.FullSourceHash};
        foreach(var e in l.RootElement.GetProperty("files").EnumerateArray())
        {
            var path=e.GetProperty("path").GetString()!;var bytes=File.ReadAllBytes(Path.Combine(projectRoot,"content/raw/story",path));
            var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));var header=Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            if(hash!=e.GetProperty("sha256").GetString()||Convert.ToHexStringLower(SHA1.HashData([..header,..bytes]))!=e.GetProperty("gitBlobSha1").GetString())throw new InvalidDataException("Story source differs from pinned lock: "+path);
            hashes.Add(path+":"+hash);var text=Encoding.UTF8.GetString(bytes);
            if(path is "data/event_scripts.s" or "data/scripts/movement.inc" or "data/scripts/fame_checker.inc" or "data/scripts/std_msgbox.inc" or "data/scripts/obtain_item.inc")
            {sources.Add(new(path,text,new(SourceKind.Pret,path,"common-script-library","pret/pokefirered",PretWorldMap.SourceCommit,hash)));textFiles.Add(text);}
            if(path.StartsWith("include/constants/",StringComparison.Ordinal))
                foreach(Match m in Regex.Matches(text,@"^#define\s+([A-Z][A-Z0-9_]+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$",RegexOptions.Multiline|RegexOptions.CultureInvariant))
                    constants[m.Groups[1].Value]=m.Groups[2].Value.StartsWith("0x",StringComparison.Ordinal)?Convert.ToInt32(m.Groups[2].Value,16):int.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
        }
        if(region is not null)
        {
            hashes.Add("viridian-region:"+PretViridianRegion.ManifestSha256);
            hashes.AddRange(region.AdditionalSourceHashes);
            var paths=sources.Select(x=>x.Path).ToHashSet(StringComparer.Ordinal);
            var textPaths=maps.Values.Select(x=>x.SourceRef.Path.Replace("map.json","text.inc",StringComparison.Ordinal)).Concat(paths).ToHashSet(StringComparer.Ordinal);
            foreach(var source in region.Scripts)if(paths.Add(source.Path)){sources.Add(source);textFiles.Add(source.Text);textPaths.Add(source.Path);}
            foreach(var (path,text) in region.TextSources)if(textPaths.Add(path))textFiles.Add(text);
            foreach(var (symbol,value) in region.Constants)constants[symbol]=value;
        }
        if(center is not null)
        {
            hashes.AddRange(center.AdditionalSourceHashes);
            var byPath=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            var textPaths=maps.Values.Select(x=>x.SourceRef.Path.Replace("map.json","text.inc",StringComparison.Ordinal)).Concat(byPath.Keys).Concat(region!.TextSources.Keys).ToHashSet(StringComparer.Ordinal);
            foreach(var source in center.Scripts)
            {
                if(byPath.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting authenticated center source: "+source.Path);continue;}
                byPath.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);textPaths.Add(source.Path);
            }
            foreach(var (path,text) in center.TextSources)if(textPaths.Add(path))textFiles.Add(text);
            foreach(var (symbol,value) in center.Constants)constants[symbol]=value;
        }
        if(forest is not null)
        {
            hashes.AddRange(forest.AdditionalSourceHashes);
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in forest.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting forest source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in forest.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in forest.Constants)constants[symbol]=value;
        }
        if(pewter is not null)
        {
            hashes.AddRange(pewter.AdditionalSourceHashes);
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in pewter.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Pewter source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in pewter.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in pewter.Constants)constants[symbol]=value;
        }
        var pokemonStorage=includePokemonStorage?PretPokemonStorageSource.Load(projectRoot):null;
        if(pokemonStorage is not null)
        {
            hashes.AddRange(pokemonStorage.AdditionalSourceHashes);
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in pokemonStorage.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting storage source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in pokemonStorage.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in pokemonStorage.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting storage constant: "+symbol);
                constants[symbol]=value;
            }
        }
        var fieldPickups=includeFieldPickups?PretEarlyPickups.Load(projectRoot):null;
        if(fieldPickups is not null)
        {
            hashes.AddRange(fieldPickups.AdditionalSourceHashes);
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in fieldPickups.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting pickup source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in fieldPickups.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in fieldPickups.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting pickup constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(oakRating is not null)
        {
            hashes.AddRange(oakRating.AdditionalSourceHashes);
            hashes.AddRange(oakPokedex!.AdditionalSourceHashes);
            hashes.Add("oak-rating-application:fr-oak-rating-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in oakRating.Scripts.Concat(oakRating.CallerScripts))
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Oak rating source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in oakRating.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in oakRating.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Oak rating constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(route22 is not null)
        {
            hashes.AddRange(route22.AdditionalSourceHashes);
            hashes.AddRange(route22Pokemon!.AdditionalSourceHashes);
            hashes.Add("route22-application:fr-route22-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in route22.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Route22 source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in route22.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in route22.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Route22 constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(route3 is not null)
        {
            hashes.AddRange(route3.AdditionalSourceHashes);
            hashes.AddRange(route3Pokemon!.AdditionalSourceHashes);
            hashes.Add("route3-application:fr-route3-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in route3.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Route3 source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in route3.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in route3.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Route3 constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(mtMoon is not null)
        {
            hashes.AddRange(mtMoon.AdditionalSourceHashes);
            hashes.AddRange(mtMoonPokemon!.AdditionalSourceHashes);
            hashes.Add("mtmoon-application:fr-mtmoon-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in mtMoon.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting MtMoon source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in mtMoon.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in mtMoon.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting MtMoon constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(cerulean is not null)
        {
            hashes.AddRange(cerulean.AdditionalSourceHashes);
            hashes.Add("cerulean-application:fr-cerulean-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in cerulean.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Cerulean source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in cerulean.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in cerulean.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Cerulean constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(bill is not null)
        {
            hashes.AddRange(bill.AdditionalSourceHashes);
            hashes.Add("bill-application:fr-bill-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in bill.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Bill source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in bill.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in bill.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Bill constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(vermilion is not null)
        {
            hashes.AddRange(vermilion.AdditionalSourceHashes);
            hashes.Add("vermilion-application:fr-vermilion-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in vermilion.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Vermilion source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in vermilion.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in vermilion.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Vermilion constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(ssAnne is not null)
        {
            hashes.AddRange(ssAnne.AdditionalSourceHashes);
            hashes.Add("ssanne-application:fr-ssanne-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in ssAnne.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting S.S. Anne source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in ssAnne.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in ssAnne.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting S.S. Anne constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(vermilionGym is not null)
        {
            hashes.AddRange(vermilionGym.AdditionalSourceHashes);
            hashes.AddRange(vermilionGymAssets!.AdditionalSourceHashes);
            hashes.Add("vermilion-gym-application:fr-vermilion-gym-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in vermilionGym.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Vermilion Gym source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in vermilionGym.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in vermilionGym.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Vermilion Gym constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(cityServices is not null)
        {
            hashes.AddRange(cityServices.AdditionalSourceHashes);
            hashes.AddRange(oldRod!.AdditionalSourceHashes);
            hashes.Add("city-services-application:fr-city-services-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in cityServices.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting City services source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in cityServices.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in cityServices.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting City services constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(route11 is not null)
        {
            hashes.AddRange(route11.AdditionalSourceHashes);
            hashes.AddRange(route11Encounters!.AdditionalSourceHashes);
            hashes.Add("route11-application:fr-route11-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in route11.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Route11 source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in route11.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in route11.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Route11 constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(rockTunnel is not null)
        {
            hashes.AddRange(rockTunnel.AdditionalSourceHashes);
            hashes.AddRange(rockTunnelEncounters!.AdditionalSourceHashes);
            hashes.Add("rocktunnel-application:fr-rocktunnel-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in rockTunnel.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting RockTunnel source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in rockTunnel.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in rockTunnel.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting RockTunnel constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(celadonApproach is not null)
        {
            hashes.AddRange(celadonApproach.AdditionalSourceHashes);
            hashes.AddRange(celadonApproachEncounters!.AdditionalSourceHashes);
            hashes.Add("celadon-approach-application:fr-celadon-approach-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in celadonApproach.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting CeladonApproach source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in celadonApproach.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in celadonApproach.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting CeladonApproach constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(celadonGym is not null)
        {
            hashes.AddRange(celadonGym.AdditionalSourceHashes);
            hashes.Add("celadon-gym-application:fr-celadon-gym-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in celadonGym.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting CeladonGym source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in celadonGym.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in celadonGym.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting CeladonGym constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(rocketHideout is not null)
        {
            hashes.AddRange(rocketHideout.AdditionalSourceHashes);
            hashes.Add("rocket-hideout-application:fr-rocket-hideout-campaign-v1");
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in rocketHideout.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Rocket Hideout source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in rocketHideout.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in rocketHideout.Constants)
            {
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Rocket Hideout constant: "+symbol);
                constants[symbol]=value;
            }
            // Metatile labels use mixed case, unlike the source region's uppercase
            // numeric constant inventory. Resolve only the authenticated patch atlas.
            foreach(var tile in rocketHideout.SourceContract.GetProperty("patchAtlas").EnumerateArray())
            {
                string symbol=tile.GetProperty("symbol").GetString()!;
                int value=tile.GetProperty("metatileId").GetInt32();
                if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Rocket Hideout metatile constant: "+symbol);
                constants[symbol]=value;
            }
        }
        if(pokemonTower is not null)
        {
            hashes.AddRange(pokemonTower.AdditionalSourceHashes);
            hashes.Add("pokemon-tower-trainers:"+PretPokemonTowerTrainers.LockSha256+":"+PretPokemonTowerTrainers.GeneratedSha256);
            var paths=sources.ToDictionary(x=>x.Path,StringComparer.Ordinal);
            foreach(var source in pokemonTower.Scripts)
            {
                if(paths.TryGetValue(source.Path,out var prior))
                {if(prior.SourceRef.Sha256!=source.SourceRef.Sha256||prior.Text!=source.Text)throw new InvalidDataException("Conflicting Tower source: "+source.Path);continue;}
                paths.Add(source.Path,source);sources.Add(source);textFiles.Add(source.Text);
            }
            foreach(var text in pokemonTower.TextSources.Values)if(!textFiles.Contains(text,StringComparer.Ordinal))textFiles.Add(text);
            foreach(var (symbol,value) in pokemonTower.Constants)
            {if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Tower constant: "+symbol);constants[symbol]=value;}
        }
        if(snorlax is not null)AppendSnorlaxSources(snorlax,sources,textFiles,constants,hashes);
        if(includeRegionalFlavor)AppendRegionalFlavorSources(projectRoot,sources,textFiles,hashes);
        CampaignPokemonContent? pokemonContent=null;
        if(includeStarterEvents)
        {
            var extra=new Dictionary<string,string>(StringComparer.Ordinal);
            var starterLockBytes=File.ReadAllBytes(Path.Combine(projectRoot,"scripts/fetch-starter-events.lock.json"));
            if(Convert.ToHexStringLower(SHA256.HashData(starterLockBytes))!="9889ffe7ebc66aba632be43c3549cbdbd734f83dcb6deb431e08e7a8d65d5c4a")throw new InvalidDataException("Starter event lock hash mismatch.");
            using var starterLock=JsonDocument.Parse(starterLockBytes);
            if(starterLock.RootElement.GetProperty("commit").GetString()!=PretWorldMap.SourceCommit)throw new InvalidDataException("Starter event source commit mismatch.");
            foreach(var entry in starterLock.RootElement.GetProperty("files").EnumerateArray())
            {
                var path=entry.GetProperty("path").GetString()!;
                if(Path.IsPathRooted(path)||path.Split('/').Contains(".."))throw new InvalidDataException("Starter source path invalid.");
                var bytes=File.ReadAllBytes(Path.Combine(projectRoot,"content/raw/starter-events",path));
                var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));var header=Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
                if(bytes.Length!=entry.GetProperty("byteLength").GetInt32()||hash!=entry.GetProperty("sha256").GetString()||Convert.ToHexStringLower(SHA1.HashData([..header,..bytes]))!=entry.GetProperty("gitBlobSha1").GetString())throw new InvalidDataException("Starter event source mismatch: "+path);
                hashes.Add(path+":"+hash);extra.Add(path,Encoding.UTF8.GetString(bytes));
            }
            foreach(Match m in Regex.Matches(extra["include/constants/species.h"],@"^#define\s+(SPECIES_[A-Z0-9_]+)\s+([0-9]+)\s*$",RegexOptions.Multiline|RegexOptions.CultureInvariant))constants[m.Groups[1].Value]=int.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
            foreach(var constant in new[]{"YES","NO"})
            {
                var m=Regex.Match(extra["asm/macros/event.inc"],@"^\s*"+constant+@"\s*=\s*([0-9]+)\s*$",RegexOptions.Multiline|RegexOptions.CultureInvariant);
                if(!m.Success)throw new InvalidDataException("Missing source yes/no constant.");constants[constant]=int.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
            }
            var pokemonJson=File.ReadAllText(Path.Combine(projectRoot,"content/generated/pokemon/starter-catalog.json"));
            var pokemon=PretPokemonCatalog.Load(pokemonJson);hashes.Add("pokemon:"+PretPokemonCatalog.GeneratedSha256);
            var names=ImmutableSortedDictionary.CreateBuilder<int,string>();
            foreach(Match m in Regex.Matches(extra["src/data/text/species_names.h"],@"\[(SPECIES_[A-Z0-9_]+)\]\s*=\s*_\(""([^""]+)""\)",RegexOptions.CultureInvariant))
                if(constants.TryGetValue(m.Groups[1].Value,out var speciesId)&&pokemon.Species.Values.Any(x=>x.SourceSpeciesId==speciesId))names.Add(speciesId,m.Groups[2].Value);
            if(names.Count!=pokemon.Species.Count)throw new InvalidDataException("Missing source species names.");
            var keyboard=Regex.Match(extra["src/naming_screen.c"],@"static const u8 sKeyboardChars[^=]+=(.*?);",RegexOptions.Singleline|RegexOptions.CultureInvariant);
            if(!keyboard.Success)throw new InvalidDataException("Nickname keyboard missing.");
            var chars=string.Concat(Regex.Matches(keyboard.Groups[1].Value,@"__\(""([^""]*)""\)",RegexOptions.CultureInvariant).Select(m=>m.Groups[1].Value)).Distinct().Order().ToArray();
            if(chars.Length<60)throw new InvalidDataException("Nickname alphabet incomplete.");
            pokemonContent=new(pokemon,names.ToImmutable(),new string(chars));
        }
        PretRoute1PokemonContent? route1Pokemon=null;
        if(region is not null)
        {
            route1Pokemon=PretRoute1PokemonCatalog.LoadContent(File.ReadAllText(Path.Combine(projectRoot,"content/generated/route1-pokemon/catalog.json"),Encoding.UTF8));
            hashes.Add("route1-pokemon:"+PretRoute1PokemonCatalog.GeneratedSha256);
            pokemonContent=pokemonContent! with{Catalog=route1Pokemon.Catalog,SpeciesNames=route1Pokemon.SpeciesNames};
        }
        var oldMan=includeOldManTutorial?PretOldManTutorial.LoadContent(File.ReadAllText(Path.Combine(projectRoot,"content/generated/route1-pokemon/catalog.json"),Encoding.UTF8),File.ReadAllText(Path.Combine(projectRoot,"content/generated/old-man-tutorial/catalog.json"),Encoding.UTF8)):null;
        if(oldMan is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=oldMan.Catalog,SpeciesNames=oldMan.SpeciesNames};
            hashes.Add("old-man-tutorial:"+PretOldManTutorial.GeneratedSha256);
        }
        var forestPokemon=includeForest?PretForestPokemon.Load(projectRoot):null;
        if(forestPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=forestPokemon.Catalog,SpeciesNames=forestPokemon.SpeciesNames};
            hashes.AddRange(forestPokemon.AdditionalSourceHashes);
            foreach(var (symbol,value) in forestPokemon.Constants)constants[symbol]=value;
        }
        var pewterPokemon=includePewter?PretPewterPokemon.Load(projectRoot):null;
        if(pewterPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=pewterPokemon.Catalog,SpeciesNames=pewterPokemon.SpeciesNames};
            hashes.AddRange(pewterPokemon.AdditionalSourceHashes);
            foreach(var (symbol,value) in pewterPokemon.Constants)constants[symbol]=value;
        }
        if(route22Pokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=route22Pokemon.Catalog,SpeciesNames=route22Pokemon.SpeciesNames};
            foreach(var (symbol,value) in route22Pokemon.Constants)constants[symbol]=value;
        }
        if(route3Pokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=route3Pokemon.Catalog,SpeciesNames=route3Pokemon.SpeciesNames};
            foreach(var (symbol,value) in route3Pokemon.Constants)constants[symbol]=value;
        }
        if(mtMoonPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=mtMoonPokemon.Catalog,SpeciesNames=mtMoonPokemon.SpeciesNames};
            foreach(var (symbol,value) in mtMoonPokemon.Constants)constants[symbol]=value;
        }
        if(ceruleanPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=ceruleanPokemon.Catalog,SpeciesNames=ceruleanPokemon.SpeciesNames};
            hashes.AddRange(ceruleanPokemon.AdditionalSourceHashes);
            hashes.Add("cerulean-trainers:"+PretCeruleanTrainers.LockSha256);
            foreach(var (symbol,value) in ceruleanPokemon.Constants)constants[symbol]=value;
        }
        if(billPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=billPokemon.Catalog,SpeciesNames=billPokemon.SpeciesNames};
            hashes.AddRange(billPokemon.AdditionalSourceHashes);
            hashes.Add("bill-trainers:"+PretBillTrainers.LockSha256+":"+PretBillTrainers.GeneratedSha256);
            foreach(var (symbol,value) in billPokemon.Constants)constants[symbol]=value;
        }
        if(vermilionPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=vermilionPokemon.Catalog,SpeciesNames=vermilionPokemon.SpeciesNames};
            hashes.AddRange(vermilionPokemon.AdditionalSourceHashes);
            hashes.Add("vermilion-trainers:"+PretVermilionTrainers.LockSha256+":"+PretVermilionTrainers.GeneratedSha256);
            foreach(var (symbol,value) in vermilionPokemon.Constants)constants[symbol]=value;
        }
        if(ssAnnePokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=ssAnnePokemon.Catalog,SpeciesNames=ssAnnePokemon.SpeciesNames};
            hashes.AddRange(ssAnnePokemon.AdditionalSourceHashes);
            hashes.Add("ssanne-trainers:"+PretSsAnneTrainers.LockSha256+":"+PretSsAnneTrainers.GeneratedSha256);
            foreach(var (symbol,value) in ssAnnePokemon.Constants)constants[symbol]=value;
        }
        if(route11Pokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=route11Pokemon.Catalog,SpeciesNames=route11Pokemon.SpeciesNames,AllowFreezeStatus=true};
            hashes.AddRange(route11Pokemon.AdditionalSourceHashes);
            hashes.Add("route11-trainers:"+PretRoute11Trainers.LockSha256+":"+PretRoute11Trainers.GeneratedSha256);
            foreach(var (symbol,value) in route11Pokemon.Constants)constants[symbol]=value;
        }
        if(includeVermilionGym)
        {
            pokemonContent=pokemonContent! with{Catalog=PretVermilionGymTrainers.ExtendPokemon(projectRoot,route11Pokemon?.Catalog??ssAnnePokemon!.Catalog,route11Rules:includeRoute11)};
            hashes.Add("vermilion-gym-trainers:"+PretVermilionGymTrainers.LockSha256+":"+PretVermilionGymTrainers.GeneratedSha256);
        }
        if(rockTunnelPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=RockTunnelCampaignPokemon(rockTunnelPokemon.Catalog,pokemonContent!.Catalog),SpeciesNames=rockTunnelPokemon.SpeciesNames};
            hashes.AddRange(rockTunnelPokemon.AdditionalSourceHashes);
            hashes.Add("rocktunnel-trainers:"+PretRockTunnelTrainers.LockSha256+":"+PretRockTunnelTrainers.GeneratedSha256);
            foreach(var (symbol,value) in rockTunnelPokemon.Constants)constants[symbol]=value;
        }
        if(celadonApproachPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=CeladonApproachCampaignPokemon(celadonApproachPokemon.Catalog,pokemonContent!.Catalog),SpeciesNames=celadonApproachPokemon.SpeciesNames};
            hashes.AddRange(celadonApproachPokemon.AdditionalSourceHashes);
            hashes.Add("celadon-approach-trainers:"+PretCeladonApproachTrainers.LockSha256+":"+PretCeladonApproachTrainers.GeneratedSha256);
            foreach(var (symbol,value) in celadonApproachPokemon.Constants)constants[symbol]=value;
        }
        if(celadonGymPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=CeladonGymCampaignPokemon(celadonGymPokemon.Catalog,pokemonContent!.Catalog),SpeciesNames=celadonGymPokemon.SpeciesNames};
            hashes.AddRange(celadonGymPokemon.AdditionalSourceHashes);
            hashes.Add("celadon-gym-trainers:"+PretCeladonGymTrainers.LockSha256+":"+PretCeladonGymTrainers.GeneratedSha256);
            foreach(var (symbol,value) in celadonGymPokemon.Constants)constants[symbol]=value;
        }
        if(rocketHideoutPokemon is not null)
        {
            pokemonContent=pokemonContent! with{Catalog=RocketHideoutCampaignPokemon(rocketHideoutPokemon.Catalog,pokemonContent!.Catalog),SpeciesNames=rocketHideoutPokemon.SpeciesNames};
            hashes.AddRange(rocketHideoutPokemon.AdditionalSourceHashes);
            hashes.Add("rocket-hideout-trainers:"+PretRocketHideoutTrainers.LockSha256+":"+PretRocketHideoutTrainers.GeneratedSha256);
            foreach(var (symbol,value) in rocketHideoutPokemon.Constants)constants[symbol]=value;
        }
        if(includePokemonTower)
        {
            pokemonTowerPokemon=PretPokemonTowerPokemon.Load(pokemonContent!.Catalog,projectRoot);
            pokemonContent=pokemonContent with{Catalog=pokemonTowerPokemon.Catalog,SpeciesNames=pokemonContent.SpeciesNames.AddRange(pokemonTowerPokemon.AdditionalSpeciesNames)};
            hashes.AddRange(pokemonTowerPokemon.AdditionalSourceHashes);
            foreach(var (symbol,value) in pokemonTowerPokemon.Constants)constants[symbol]=value;
        }
        if(includeSnorlax)
        {
            snorlaxPokemon=PretSnorlaxPokemon.Load(pokemonContent!.Catalog,projectRoot);
            pokemonContent=pokemonContent with{Catalog=snorlaxPokemon.Catalog,SpeciesNames=pokemonContent.SpeciesNames.AddRange(snorlaxPokemon.AdditionalSpeciesNames)};
            hashes.AddRange(snorlaxPokemon.AdditionalSourceHashes);
            hashes.AddRange(snorlaxArt!.AdditionalSourceHashes);
            hashes.Add("snorlax-application:fr-snorlax-campaign-v1");
            foreach(var (symbol,value) in snorlaxPokemon.Constants)
            {if(constants.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Snorlax Pokemon constant: "+symbol);constants[symbol]=value;}
        }
        var mtMoonItems=includeMtMoon?PretMtMoonItems.LoadProject(projectRoot):null;
        if(mtMoonItems is not null)hashes.Add("mtmoon-items:"+PretMtMoonItems.GeneratedSha256+":"+PretMtMoonItems.LockSha256);
        var machines=includeSsAnne?PretSsAnneMachines.LoadCatalogProject(projectRoot):includeVermilion?PretVermilionMachines.LoadCatalogProject(projectRoot):includeBill?PretBillMachines.LoadCatalogProject(projectRoot):includeCerulean?PretCeruleanMachines.LoadCatalogProject(projectRoot):includeMtMoon?PretPewterMachines.LoadMtMoonProject(projectRoot):includeRoute3?PretPewterMachines.LoadRoute3Project(projectRoot):includeRoute22?PretPewterMachines.LoadRoute22Project(projectRoot):includePewter?PretPewterMachines.LoadProject(projectRoot):null;
        if(vermilionGymAssets is not null)machines=vermilionGymAssets.ExtendMachines(machines!);
        if(includeRoute11){machines=PretRoute11Machines.LoadCatalogProject(projectRoot);hashes.Add("route11-machines:"+PretRoute11Machines.GeneratedSha256+":"+PretRoute11Machines.LockSha256);}
        if(includeRockTunnel){machines=PretRockTunnelMachines.LoadCatalogProject(projectRoot);hashes.Add("rocktunnel-machines:"+PretRockTunnelMachines.GeneratedSha256+":"+PretRockTunnelMachines.LockSha256);}
        if(includeCeladonApproach){machines=PretCeladonApproachMachines.LoadCatalogProject(projectRoot);hashes.Add("celadon-approach-machines:"+PretCeladonApproachMachines.GeneratedSha256+":"+PretCeladonApproachItems.LockSha256);}
        if(includeCeladonGym){machines=PretCeladonGymMachines.LoadCatalogProject(projectRoot);hashes.Add("celadon-gym-machines:"+PretCeladonGymMachines.GeneratedSha256+":"+PretCeladonGymItems.LockSha256);}
        if(includeRocketHideout){machines=PretRocketHideoutMachines.LoadCatalogProject(projectRoot);hashes.Add("rocket-hideout-machines:"+PretRocketHideoutMachines.GeneratedSha256+":"+PretRocketHideoutItems.LockSha256);}
        if(includePokemonTower){machines=PretPokemonTowerMachines.Extend(projectRoot,machines!);hashes.Add("pokemon-tower-machines:"+PokemonTowerMachineRules.Profile+":"+PokemonTowerMachineRules.SourceSha256);}
        if(includeSnorlax){machines=PretSnorlaxMachines.Extend(projectRoot,machines!);hashes.Add("snorlax-machines:"+SnorlaxMachineRules.Profile+":"+SnorlaxMachineRules.SourceSha256);}
        if(includeSsAnne)hashes.Add("ssanne-machines:"+PretSsAnneMachines.GeneratedSha256+":"+PretSsAnneMachines.LockSha256);
        if(includeVermilion)hashes.Add("vermilion-machines:"+PretVermilionMachines.GeneratedSha256+":"+PretVermilionMachines.LockSha256);
        if(includeBill)hashes.Add("bill-machines:"+PretBillMachines.GeneratedSha256+":"+PretBillMachines.LockSha256);
        if(includeCerulean)hashes.Add("cerulean-machines:"+PretCeruleanMachines.GeneratedSha256+":"+PretCeruleanMachines.LockSha256);
        if(includeRoute3)hashes.Add("route3-machines:"+PewterMachineRules.Route3Profile);
        if(includeRoute22)hashes.Add("route22-machines:"+PewterMachineRules.Route22Profile);
        if(machines is not null)hashes.Add("pewter-machines:"+PretPewterMachines.GeneratedSha256+":"+PretPewterMachines.LockSha256);
        CampaignBattleContent? battleContent=null;
        if(includeFirstBattle)
        {
            var trainers=PretLabTrainerCatalog.Load(File.ReadAllText(Path.Combine(projectRoot,"content/generated/lab-trainers/lab-trainers.json"),Encoding.UTF8));
            hashes.Add("trainers:"+PretLabTrainerCatalog.GeneratedSha256);
            var lockBytes=File.ReadAllBytes(Path.Combine(projectRoot,"scripts/fetch-lab-battle-extra.lock.json"));
            if(Convert.ToHexStringLower(SHA256.HashData(lockBytes))!="f514c7854d728c0f58f0b5ea83acabd8bbefddf7f3a4799f2de4304e0838cb6e")throw new InvalidDataException("Battle presentation lock mismatch.");
            using var extraLock=JsonDocument.Parse(lockBytes);string? moveNamesSource=null;
            foreach(var input in extraLock.RootElement.GetProperty("files").EnumerateArray())
            {
                var path=input.GetProperty("path").GetString()!;var bytes=File.ReadAllBytes(Path.Combine(projectRoot,"content/raw/lab-battle-extra",path));
                var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));var header=Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
                if(bytes.Length!=input.GetProperty("byteLength").GetInt32()||hash!=input.GetProperty("sha256").GetString()||Convert.ToHexStringLower(SHA1.HashData([..header,..bytes]))!=input.GetProperty("gitBlobSha1").GetString())throw new InvalidDataException("Battle source differs: "+path);
                hashes.Add(path+":"+hash);if(path=="src/data/text/move_names.h")moveNamesSource=Encoding.UTF8.GetString(bytes);
            }
            var names=ImmutableSortedDictionary.CreateBuilder<string,string>(StringComparer.Ordinal);
            foreach(Match m in Regex.Matches(moveNamesSource??throw new InvalidDataException("Move names source absent."),@"\[(MOVE_[A-Z0-9_]+)\]\s*=\s*_\(""([^""]*)""\)",RegexOptions.CultureInvariant))names.Add("pret:"+m.Groups[1].Value,m.Groups[2].Value);
            foreach(var move in new[]{"TACKLE","SCRATCH","GROWL","TAIL_WHIP","STRUGGLE"})if(!names.ContainsKey("pret:MOVE_"+move))throw new InvalidDataException("Battle move name missing.");
            if(route1Pokemon is not null)foreach(var (move,name) in route1Pokemon.MoveNames)
            {
                if(names.TryGetValue(move.Value,out var prior)&&prior!=name)throw new InvalidDataException("Conflicting source move name: "+move.Value);
                names[move.Value]=name;
            }
            if(oldMan is not null)foreach(var (move,name) in oldMan.MoveNames)names[move.Value]=name;
            if(forestPokemon is not null)foreach(var (move,name) in forestPokemon.MoveNames)names[move.Value]=name;
            if(pewterPokemon is not null)foreach(var (move,name) in pewterPokemon.MoveNames)names[move.Value]=name;
            if(route3Pokemon is not null)foreach(var (move,name) in route3Pokemon.MoveNames)names[move.Value]=name;
            if(mtMoonPokemon is not null)foreach(var (move,name) in mtMoonPokemon.MoveNames)names[move.Value]=name;
            if(route11Pokemon is not null)foreach(var (move,name) in route11Pokemon.MoveNames)
            {
                if(names.TryGetValue(move.Value,out var prior)&&prior!=name)throw new InvalidDataException("Conflicting Route11 move name: "+move.Value);
                names[move.Value]=name;
            }
            if(rockTunnelPokemon is not null)foreach(var (move,name) in rockTunnelPokemon.MoveNames)
            {
                if(names.TryGetValue(move.Value,out var prior)&&prior!=name)throw new InvalidDataException("Conflicting RockTunnel move name: "+move.Value);
                names[move.Value]=name;
            }
            if(celadonApproachPokemon is not null)foreach(var (move,name) in celadonApproachPokemon.MoveNames)
            {
                if(names.TryGetValue(move.Value,out var prior)&&prior!=name)throw new InvalidDataException("Conflicting CeladonApproach move name: "+move.Value);
                names[move.Value]=name;
            }
            if(celadonGymPokemon is not null)foreach(var (move,name) in celadonGymPokemon.MoveNames)
            {
                if(names.TryGetValue(move.Value,out var prior)&&prior!=name)throw new InvalidDataException("Conflicting CeladonGym move name: "+move.Value);
                names[move.Value]=name;
            }
            if(rocketHideoutPokemon is not null)foreach(var (move,name) in rocketHideoutPokemon.MoveNames)
            {
                if(names.TryGetValue(move.Value,out var prior)&&prior!=name)throw new InvalidDataException("Conflicting Rocket Hideout move name: "+move.Value);
                names[move.Value]=name;
            }
            if(pokemonTowerPokemon is not null)foreach(var (move,name) in pokemonTowerPokemon.AdditionalMoveNames)names[move.Value]=name;
            if(snorlaxPokemon is not null)foreach(var (move,name) in snorlaxPokemon.AdditionalMoveNames)
            {
                if(names.TryGetValue(move.Value,out var prior)&&prior!=name)throw new InvalidDataException("Conflicting Snorlax move name: "+move.Value);
                names[move.Value]=name;
            }
            if(includeVermilionGym)foreach(var (move,name) in PretVermilionGymTrainers.LoadMoveNames(projectRoot))
            {
                if(names.TryGetValue(move.Value,out var prior)&&prior!=name)throw new InvalidDataException("Conflicting Vermilion Gym move name: "+move.Value);
                names[move.Value]=name;
            }
            battleContent=new(trainers,names.ToImmutable());
        }
        OpeningItemsCatalog? openingItems=null;
        var openingUiTexts=new Dictionary<string,string>(StringComparer.Ordinal);
        if(includeOpeningItems)
        {
            if(!includeStarterEvents)throw new ArgumentException("Opening inventory requires Pokemon content.");
            openingItems=PretOpeningItems.Load(File.ReadAllText(Path.Combine(projectRoot,"content/generated/opening-items/opening-items.json"),Encoding.UTF8));
            hashes.Add("opening-items:"+PretOpeningItems.GeneratedSha256);
            var lockBytes=File.ReadAllBytes(Path.Combine(projectRoot,"scripts/fetch-opening-ui.lock.json"));
            if(Convert.ToHexStringLower(SHA256.HashData(lockBytes))!="50e94450ab84cf6a965f0056068b1eaf33ed8314b09163bc9795ee6ca35cdb54")throw new InvalidDataException("Opening UI lock mismatch.");
            using var uiLock=JsonDocument.Parse(lockBytes);
            foreach(var input in uiLock.RootElement.GetProperty("files").EnumerateArray())
            {
                var path=input.GetProperty("path").GetString()!;var bytes=File.ReadAllBytes(Path.Combine(projectRoot,"content/raw/opening-ui",path));
                var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));var header=Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
                if(bytes.Length!=input.GetProperty("byteLength").GetInt32()||hash!=input.GetProperty("sha256").GetString()||Convert.ToHexStringLower(SHA1.HashData([..header,..bytes]))!=input.GetProperty("gitBlobSha1").GetString())throw new InvalidDataException("Opening UI source differs: "+path);
                hashes.Add(path+":"+hash);var sourceText=Encoding.UTF8.GetString(bytes);
                if(path=="include/constants/help_system.h")
                    foreach(Match m in Regex.Matches(sourceText,@"^#define\s+([A-Z][A-Z0-9_]+)\s+([0-9]+)\s*(?://[^\n]*)?$",RegexOptions.Multiline|RegexOptions.CultureInvariant))constants[m.Groups[1].Value]=int.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
                else
                    foreach(Match m in Regex.Matches(sourceText,"const u8 ([A-Za-z0-9_]+)\\[\\] = _\\(\"((?:\\\\.|[^\"\\\\])*)\"\\);",RegexOptions.CultureInvariant))openingUiTexts.TryAdd(m.Groups[1].Value,m.Groups[2].Value);
            }
        }
        // Variable and flag identity stays symbolic; numeric IDs are not save identity.
        foreach(var name in constants.Keys.Where(x=>x.StartsWith("VAR_",StringComparison.Ordinal)).ToArray())constants.Remove(name);
        foreach(var map in maps.Values)
        {
            int localId=1;
            foreach(var actor in map.Events.GetProperty("object_events").EnumerateArray()){if(actor.TryGetProperty("local_id",out var symbol))constants[symbol.GetString()!]=localId;localId++;}
        }
        QuestItemsCatalog? questItems=null;PretMartMetatiles? metatiles=null;
        if(includeParcelQuest)
        {
            questItems=PretQuestItems.Load(File.ReadAllText(Path.Combine(projectRoot,"content/generated/quest-items/quest-items.json"),Encoding.UTF8));
            if(oldMan is not null)
            {
                var tv=oldMan.TeachyTv;var r=tv.Source;
                var item=new QuestItemDefinition(QuestItemRules.TeachyTvId,QuestItemRules.TeachyTvSymbol,tv.SourceItemId,tv.Name,tv.Price,tv.Importance,tv.Registrability,tv.Pocket,tv.Type,tv.HoldEffect,tv.HoldEffectParam,tv.FieldUseFunc,tv.BattleUsage,tv.BattleUseFunc,tv.SecondaryId,false,false,false,new(r.Path,r.Symbol,r.StartLine,r.EndLine,r.Sha256));
                questItems=questItems with{Profile=QuestItemRules.TutorialProfile,Items=questItems.Items.Add(item)};QuestItemRules.ValidateCatalog(questItems);
                constants[QuestItemRules.TeachyTvSymbol]=tv.SourceItemId;
            }
            if(townMap is not null)
            {
                questItems=questItems with{Profile=QuestItemRules.TownMapProfile,Items=questItems.Items.Add(townMap.Item)};
                QuestItemRules.ValidateCatalog(questItems);
                constants[QuestItemRules.TownMapSymbol]=townMap.Item.SourceItemId;
            }
            metatiles=PretMartMetatiles.Load(projectRoot);
            constants["METATILE_Mart_CounterMid_Top"]=metatiles.OnLoadPatches.Single(x=>x.Y==3).RawMetatileId;
            constants["METATILE_Mart_CounterMid_Bottom"]=metatiles.OnLoadPatches.Single(x=>x.Y==4).RawMetatileId;
            var parcelSource=PretParcelEventSource.Load(projectRoot);
            foreach(var pair in parcelSource.Constants)if(!pair.Key.StartsWith("VAR_",StringComparison.Ordinal))constants[pair.Key]=pair.Value;
            foreach(var pair in parcelSource.Text)openingUiTexts[pair.Key]=pair.Value;
            hashes.Add("quest-items:"+PretQuestItems.GeneratedSha256);hashes.Add("parcel-events:"+PretParcelEventSource.LockSha256);hashes.Add("mart-metatiles:"+PretMartMetatiles.ManifestSha256);
        }
        if(includeEarlyShop&&(!includePewter||!includeOpeningItems))throw new ArgumentException("Early shops require the Pewter and opening-items profile.");
        var earlyShop=includeEarlyShop?PretEarlyShop.Load(File.ReadAllText(Path.Combine(projectRoot,"content/generated/early-shop/early-shop.json"),Encoding.UTF8)):null;
        if(mtMoonItems is not null)earlyShop=earlyShop! with{Catalog=includeBill?PretBillItems.LoadShopProject(projectRoot):includeCerulean?PretCeruleanItems.LoadShopProject(projectRoot):PretMtMoonItems.LoadShopProject(projectRoot)};
        if(vermilion is not null)earlyShop=earlyShop! with{Catalog=VermilionShop(earlyShop.Catalog,vermilion)};
        if(ssAnne is not null){earlyShop=earlyShop! with{Catalog=PretSsAnneItems.LoadShopProject(projectRoot,earlyShop.Catalog)};hashes.Add("ssanne-items:"+PretSsAnneItems.GeneratedSha256+":"+PretSsAnneItems.LockSha256);}
        if(includeRoute11){earlyShop=earlyShop! with{Catalog=PretRoute11Items.LoadShopProject(projectRoot,earlyShop.Catalog)};hashes.Add("route11-items:"+PretRoute11Items.GeneratedSha256+":"+PretRoute11Items.LockSha256);}
        if(includeRockTunnel){earlyShop=earlyShop! with{Catalog=PretRockTunnelItems.LoadShopProject(projectRoot,earlyShop.Catalog)};hashes.Add("rocktunnel-items:"+PretRockTunnelItems.GeneratedSha256+":"+PretRockTunnelItems.LockSha256);}
        if(includeCeladonApproach){earlyShop=earlyShop! with{Catalog=PretCeladonApproachItems.LoadShopProject(projectRoot,earlyShop.Catalog)};hashes.Add("celadon-approach-items:"+PretCeladonApproachItems.GeneratedSha256+":"+PretCeladonApproachItems.LockSha256);}
        if(includeCeladonGym){earlyShop=earlyShop! with{Catalog=PretCeladonGymItems.LoadShopProject(projectRoot,earlyShop.Catalog)};hashes.Add("celadon-gym-items:"+PretCeladonGymItems.GeneratedSha256+":"+PretCeladonGymItems.LockSha256);}
        if(includeRocketHideout){earlyShop=earlyShop! with{Catalog=PretRocketHideoutItems.LoadShopProject(projectRoot,earlyShop.Catalog)};hashes.Add("rocket-hideout-items:"+PretRocketHideoutItems.GeneratedSha256+":"+PretRocketHideoutItems.LockSha256+":"+PretRocketHideoutItems.ManifestSha256+":"+PretRocketHideoutItems.PickupsSha256);}
        if(includePokemonTower){earlyShop=earlyShop! with{Catalog=PretPokemonTowerItems.LoadShopProject(projectRoot,earlyShop.Catalog)};hashes.AddRange(PretPokemonTowerItems.SourceHashesProject(projectRoot));}
        if(earlyShop is not null)hashes.Add("early-shop:"+PretEarlyShop.GeneratedSha256);
        if(includeCerulean)hashes.Add("cerulean-items:"+PretCeruleanItems.GeneratedSha256+":"+PretCeruleanItems.LockSha256);
        if(includeBill)hashes.Add("bill-items:"+PretBillItems.GeneratedSha256+":"+PretBillItems.LockSha256);
        var compiled=PretStoryCompiler.Compile(sources,constants,includeGiftOpcodes:includeMagikarp,includeRoute3Opcodes:includeRoute3,includeMtMoonOpcodes:includeMtMoon,includeBillOpcodes:includeBill,includeSsAnneOpcodes:includeSsAnne,includeVermilionGymOpcodes:includeVermilionGym,includeRockTunnelOpcodes:includeRockTunnel,includeRocketHideoutOpcodes:includeRocketHideout,includePokemonTowerOpcodes:includePokemonTower,includeSnorlaxOpcodes:includeSnorlax);
        if(pokemonContent is not null)
        {
            var lab=maps.Values.Single(x=>x.SourceMapSymbol=="MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB");
            var objects=lab.Events.GetProperty("object_events").EnumerateArray().ToArray();var choices=ImmutableArray.CreateBuilder<StarterChoiceDefinition>();
            int Literal(string token)=>int.TryParse(token,out var value)?value:constants.TryGetValue(token,out value)?value:throw new InvalidDataException("Starter choice source constant missing: "+token);
            foreach(var actor in objects)
            {
                var program=compiled.FindProgram(actor.GetProperty("script").GetString()!);if(program is null)continue;
                var assignments=program.Instructions.Where(i=>i.Op==StoryOp.SetVar).GroupBy(i=>i.Args[0]).ToDictionary(g=>g.Key,g=>g.First().Args[1],StringComparer.Ordinal);
                if(!new[]{"VAR_TEMP_1","VAR_TEMP_2","VAR_TEMP_3","VAR_TEMP_4"}.All(assignments.ContainsKey))continue;
                var rival=objects[Literal(assignments["VAR_TEMP_4"])-1];
                choices.Add(new(Literal(assignments["VAR_TEMP_1"]),Literal(assignments["VAR_TEMP_2"]),Literal(assignments["VAR_TEMP_3"]),actor.GetProperty("local_id").GetString()!,actor.GetProperty("flag").GetString()!,rival.GetProperty("local_id").GetString()!,rival.GetProperty("flag").GetString()!));
            }
            if(choices.Count!=3||!choices.Select(x=>x.Index).Order().SequenceEqual(new[]{0,1,2})||choices.Select(x=>x.BallId).Distinct().Count()!=3)throw new InvalidDataException("Source starter branch declarations invalid.");
            pokemonContent=pokemonContent with{StarterChoices=choices.OrderBy(x=>x.Index).ToImmutableArray()};
        }
        var bank=ImmutableDictionary.CreateBuilder<string,string>(StringComparer.Ordinal);
        if(region is not null)foreach(var (symbol,text) in region.SourceUiTexts)bank.Add(symbol,text);
        if(center is not null)foreach(var (symbol,text) in center.SourceUiTexts){if(bank.TryGetValue(symbol,out var old)&&old!=text)throw new InvalidDataException("Conflicting center UI text: "+symbol);bank[symbol]=text;}
        foreach(var text in textFiles)
        {
            string? label=null;StringBuilder? value=null;
            foreach(var raw in text.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n'))
            {
                var line=raw.Trim();var m=Regex.Match(line,@"^([A-Za-z_][A-Za-z0-9_]*):{1,2}(?:\s*@.*)?$",RegexOptions.CultureInvariant);
                if(m.Success){label=m.Groups[1].Value;value=null;continue;}
                if(line.StartsWith(".string",StringComparison.Ordinal)&&label is not null)
                {
                    var part=Regex.Match(line,"^\\.string\\s+\"(.*)\"$",RegexOptions.CultureInvariant);if(!part.Success)throw new InvalidDataException("Unsupported text directive: "+label);
                    value??=new();value.Append(part.Groups[1].Value);
                    if(value.ToString().EndsWith('$')){if(!bank.TryAdd(label,value.ToString()))throw new InvalidDataException("Duplicate source text: "+label);label=null;}
                }
            }
        }
        if(openingItems is not null)
        {
            foreach(var symbol in new[]{"gText_TakeOutItemsFromThePC","gText_StoreItemsInThePC","gText_GoBackToThePreviousMenu","gOtherText_Give","gText_WhatWouldYouLikeToDo","gText_ItemStorage","gText_Mailbox","gText_TurnOff","gText_WithdrawItem2","gText_DepositItem2","gFameCheckerText_Cancel","gText_ThereAreNoItems","gText_TheresNoMailHere","gText_WithdrawHowMany","gText_DepositHowManyStrVars1","gText_WithdrewQuantItem","gText_DepositedStrVar2StrVar1s","gText_NoMoreRoomInBag","gText_ThereIsNoPokemon","gText_WontHaveEffect","gText_PkmnHPRestoredByVar2","gOtherText_Use","gText_MenuBag"})
            {
                if(!openingUiTexts.TryGetValue(symbol,out var text))throw new InvalidDataException("Opening menu text missing: "+symbol);
                bank.Add(symbol,text.Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal)+"$");
            }
        }
        if(includePokemonTower)foreach(var symbol in new[]{"gText_PlayedPokeFlute","gText_PlayedPokeFluteCatchy","gText_PokeFluteAwakenedMon"})bank.Add(symbol,openingUiTexts[symbol].Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal)+"$");
        if(includeSsAnne)bank.Add("gText_BerryPouch_2",openingUiTexts["gText_BerryPouch_2"]+"$");
        if(includeMtMoon)bank.Add("gText_PPWasRestored",openingUiTexts["gText_PPWasRestored"].Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal)+"$");
        if(includeParcelQuest)foreach(var symbol in new[]{"gText_ItemsPocket","gText_KeyItemsPocket","gText_PokeBallsPocket"})bank.Add(symbol,openingUiTexts[symbol]+"$");
        // The pinned opening-ui strings.c is byte-identical to item-ui/src/strings.c.
        // Select the English runtime text, not the unused Japanese BootedUpTM literal.
        if(machines is not null)foreach(var symbol in new[]{"gText_TMCase","gText_Close","gText_TMCaseWillBePutAway","gText_TeachWhichPokemon","gText_PkmnLearnedMove3","gText_PkmnCantLearnMove","gText_PkmnNeedsToReplaceMove","gText_WhichMoveToForget","gText_PkmnAlreadyKnows","gText_TeachMoveQues","gText_Able2","gText_NotAble","gText_Learned","gText_Yes","gText_No"})
            bank.Add(symbol,openingUiTexts[symbol].Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal)+"$");
        if(oldMan is not null)foreach(var (symbol,text) in oldMan.Texts)bank.Add(symbol,text.Text+"$");
        if(earlyShop is not null)foreach(var text in earlyShop.Texts)
        {
            var value=text.Text.Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal).Replace("\n", @"\n",StringComparison.Ordinal).TrimEnd('$')+"$";
            if(bank.TryGetValue(text.Symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting shop text: "+text.Symbol);
            bank[text.Symbol]=value;
        }
        if(pokemonStorage is not null)foreach(var (symbol,text) in pokemonStorage.SourceUiTexts)
        {
            var value=text.Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal);
            if(bank.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting storage UI text: "+symbol);
            bank[symbol]=value;
        }
        if(oldRod is not null)foreach(var (symbol,text) in oldRod.Texts)
        {
            var value=text.Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal).Replace("\n",@"\n",StringComparison.Ordinal).TrimEnd('$')+"$";
            if(bank.TryGetValue(symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting fishing text: "+symbol);
            bank[symbol]=value;
        }
        var repel=includeRepel?PretEarlyRepel.Load(File.ReadAllText(Path.Combine(projectRoot,"content/generated/early-repel/early-repel.json"),Encoding.UTF8)):null;
        if(repel is not null)
        {
            foreach(var text in repel.Texts)
            {
                var value=text.Text.Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal).Replace("\n",@"\n",StringComparison.Ordinal).TrimEnd('$')+"$";
                if(bank.TryGetValue(text.Symbol,out var prior)&&prior!=value)throw new InvalidDataException("Conflicting Repel text: "+text.Symbol);
                bank[text.Symbol]=value;
            }
            hashes.Add("early-repel:"+PretEarlyRepel.GeneratedSha256);
            hashes.Add("repel-application:fr-repel-campaign-v1");
        }
        if(townMap is not null)
        {
            hashes.AddRange(townMap.AdditionalSourceHashes);
            hashes.Add("town-map-application:fr-town-map-campaign-v1");
        }
        if(includeCapture)hashes.Add("capture-application:fr-party-capture-v1");
        if(includeEarlyStarter){hashes.Add("early-starter-application:fr-early-starter-campaign-v1");pokemonContent=pokemonContent! with{AllowSleepStatus=true};}
        if(includeEarlyParty)hashes.Add("early-party-application:fr-early-party-campaign-v1");
        if(includePokemonStorage)hashes.Add("pokemon-storage-application:fr-pokemon-storage-campaign-v1");
        if(includeFieldPickups)hashes.Add("field-pickups-application:fr-field-pickups-campaign-v1");
        if(magikarp is not null)
        {
            hashes.AddRange(magikarp.AdditionalSourceHashes);
            foreach(var label in magikarp.RequiredCommonScriptLabels)if(compiled.FindProgram(label) is null)throw new InvalidDataException("magikarp_common_script_missing: "+label);
            foreach(var label in magikarp.RequiredCommonTextLabels)if(!bank.ContainsKey(label))throw new InvalidDataException("magikarp_common_text_missing: "+label);
        }
        if(includeSnorlax)ValidateSnorlaxComposition(maps,pokemonContent!,compiled,bank);
        if(includeRegionalFlavor)ValidateRegionalFlavorComposition(maps,compiled,bank);
        var version=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",hashes))));
        return new(maps,compiled,bank.ToImmutable(),version,pokemonContent,battleContent,openingItems,region,questItems,metatiles,includeCapture,center,oldMan,includePartyTraining,forest,forestPokemon,includeForestTrainers,pewter,pewterPokemon,machines,earlyShop,includeEarlyStarter,includeEarlyParty,pokemonStorage,fieldPickups,repel,townMap,oakRating,oakPokedex,route22,route22Pokemon,route3,route3Pokemon,magikarp,mtMoon,mtMoonPokemon,mtMoonItems,cerulean,ceruleanPokemon,bill,billPokemon,billItems,billTrainers,vermilion,vermilionPokemon,vermilionTrainers,ssAnne,ssAnnePokemon,ssAnneTrainers,ssAnneItems,vermilionGym,vermilionGymTrainers,cityServices,oldRod,route11,route11Pokemon,route11Items,route11Encounters,route11Trainers,rockTunnel,rockTunnelPokemon,rockTunnelItems,rockTunnelEncounters,rockTunnelTrainers,celadonApproach,celadonApproachPokemon,celadonApproachItems,celadonApproachEncounters,celadonApproachTrainers,celadonGym,celadonGymPokemon,celadonGymItems,celadonGymTrainers,rocketHideout,rocketHideoutPokemon,rocketHideoutItems,rocketHideoutPickups,rocketHideoutTrainers,pokemonTower,pokemonTowerPokemon,pokemonTowerItems,pokemonTowerPickups,pokemonTowerTrainers,snorlax,snorlaxPokemon,snorlaxArt,includeRegionalFlavor);
    }
    private FieldMapDefinition Definition(PretWorldMap map)
    {
        var e=map.Events;var root=map.SourceRef.Path.Split('/')[2]+"_MapScripts";
        var header=_scripts.MapHeaders.GetValueOrDefault(root,[]);
        string? Target(string kind)=>header.FirstOrDefault(x=>x.EventKind==kind)?.Target;
        var actors=e.GetProperty("object_events").EnumerateArray().Select((x,index)=>CampaignSourceActor(map,x,index)).ToImmutableArray();
        var coords=e.GetProperty("coord_events").EnumerateArray().Select(x=>new FieldCoordTrigger(x.GetProperty("x").GetInt32(),x.GetProperty("y").GetInt32(),x.GetProperty("elevation").GetInt32(),x.GetProperty("var").GetString()!,int.Parse(x.GetProperty("var_value").GetString()!,System.Globalization.CultureInfo.InvariantCulture),x.GetProperty("script").GetString()!)).ToImmutableArray();
        var signs=e.GetProperty("bg_events").EnumerateArray().Select(x=>RegionalBackgroundTrigger(x,SupportsRegionalFlavor(map.Id))).ToImmutableArray();
        return new(map.Id,map.SourceMapSymbol,actors,coords,signs,new(Target("MAP_SCRIPT_ON_TRANSITION"),Target("MAP_SCRIPT_ON_WARP_INTO_MAP_TABLE"),Target("MAP_SCRIPT_ON_FRAME_TABLE"),Target("MAP_SCRIPT_ON_LOAD"),Target("MAP_SCRIPT_ON_RESUME")),e.GetProperty("music").GetString()!,e.GetProperty("region_map_section").GetString()!){MapType=e.GetProperty("map_type").GetString()!,RequiresFlash=e.GetProperty("requires_flash").GetBoolean()};
    }
    public FieldMetatileDefinition? MetatileDefinition(MapId map,int id)
    {
        if(VermilionGymRegion is not null&&map.Value=="map.vermilion-city-gym")
            return VermilionGymRegion.BeamDefinitions.TryGetValue(id,out var tile)?new(id,tile.Behavior,tile.Terrain,tile.EncounterType,tile.LayerType):null;
        if(RocketHideoutRegion?.NewMaps.ContainsKey(map)==true)
            return RocketHideoutMetatileDefinition(map,id);
        return MetatileDefinitions?.GetValueOrDefault(id);
    }
    public int? TerrainAt(MapId map,int x,int y)=>SourceMaps.TryGetValue(map,out var source)?source.GetSourceCell(x,y)?.Terrain:null;
    public bool AllowsCycling(MapId map)=>SourceMaps.TryGetValue(map,out var source)&&source.Events.GetProperty("allow_cycling").GetBoolean();
    public FieldMapDefinition MapDefinition(MapId id)=>_definitions[id];
    public MapId MapFromSourceSymbol(string symbol)=>SourceMaps.Values.FirstOrDefault(x=>x.SourceMapSymbol==symbol)?.Id
        ?? (SnorlaxEnabled&&(symbol is "MAP_ROUTE12" or "MAP_ROUTE16" or "MAP_ROUTE13" or "MAP_ROUTE17"||symbol.StartsWith("MAP_ROUTE12_",StringComparison.Ordinal)||symbol.StartsWith("MAP_ROUTE16_",StringComparison.Ordinal))?PretSnorlaxRegion.MapIdFromSourceSymbol(symbol):PokemonTowerEnabled&&symbol.StartsWith("MAP_POKEMON_TOWER_",StringComparison.Ordinal)?PretPokemonTowerRegion.MapIdFromSourceSymbol(symbol):VermilionGymEnabled&&symbol=="MAP_VERMILION_CITY_GYM"?new MapId("map.vermilion-city-gym"):RocketHideoutEnabled?PretRocketHideoutRegion.MapIdFromSourceSymbol(symbol):CeladonGymEnabled?PretCeladonGymRegion.MapIdFromSourceSymbol(symbol):CeladonApproachEnabled?PretCeladonApproachRegion.MapIdFromSourceSymbol(symbol):RockTunnelEnabled?PretRockTunnelRegion.MapIdFromSourceSymbol(symbol):Route11Enabled?PretRoute11Region.MapIdFromSourceSymbol(symbol):SsAnneEnabled?PretSsAnneRegion.MapIdFromSourceSymbol(symbol):BillEnabled?PretBillRegion.MapIdFromSourceSymbol(symbol):CeruleanEnabled?PretCeruleanRegion.MapIdFromSourceSymbol(symbol):MtMoonEnabled?PretMtMoonRegion.MapIdFromSourceSymbol(symbol):Route3Enabled?PretRoute3Region.MapIdFromSourceSymbol(symbol):Route22Enabled?PretRoute22Region.MapIdFromSourceSymbol(symbol):PewterEnabled?PretPewterRegion.MapIdFromSourceSymbol(symbol):ForestEnabled?PretForestRegion.MapIdFromSourceSymbol(symbol):ViridianCenterEnabled?PretViridianCenter.MapIdFromSourceSymbol(symbol):RegionEnabled?PretViridianRegion.MapIdFromSourceSymbol(symbol):throw new InvalidDataException("Source map is not loaded: "+symbol));
    public ImmutableArray<FieldProgramCondition> Conditions(string table)=>_scripts.MapConditions.TryGetValue(table,out var list)?list.Select(x=>new FieldProgramCondition(x.Variable,int.Parse(x.Expected,System.Globalization.CultureInfo.InvariantCulture),x.Target)).ToImmutableArray():[];
    public ImmutableArray<string> Movement(string program)=>_scripts.Movements.TryGetValue(program,out var list)?list.Select(x=>x.Opcode).ToImmutableArray():throw new InvalidDataException("Missing movement program: "+program);
    public StoryProgram? FindProgram(string id)=>_scripts.FindProgram(id);
    public bool TryConstant(string symbol,out int value)=>_scripts.TryConstant(symbol,out value);
    public ImmutableArray<string> TextPages(string symbol)
    {
        if(!_text.TryGetValue(symbol,out var raw))throw new InvalidDataException("Missing source text: "+symbol);
        if(Regex.IsMatch(raw,@"\\(?![npl])"))throw new InvalidDataException("Unsupported text escape: "+symbol);
        var pages=ImmutableArray.CreateBuilder<string>();var lines=new List<string>();var line=new StringBuilder();
        for(int i=0;i<raw.Length;i++)
        {
            char c=raw[i];
            if(c=='$'){lines.Add(line.ToString());pages.Add(string.Join("\n",lines));break;}
            if(c=='\\')
            {
                char control=raw[++i];lines.Add(line.ToString());line.Clear();
                if(control=='p'){pages.Add(string.Join("\n",lines));lines.Clear();}
                else if(control=='l'){pages.Add(string.Join("\n",lines));var last=lines[^1];lines.Clear();lines.Add(last);}
            }
            else line.Append(c);
        }
        if(pages.Count==0)throw new InvalidDataException("Missing text terminator: "+symbol);
        return pages.ToImmutable();
    }
}
