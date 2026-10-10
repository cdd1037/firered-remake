using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Rules;
using Game.Domain.World;

if(args.Length==4&&args[0]=="save"&&args[1] is "upgrade-field-pickups" or "upgrade-repel" or "upgrade-town-map" or "upgrade-oak-rating" or "upgrade-route22" or "upgrade-route3" or "upgrade-magikarp" or "upgrade-mtmoon" or "upgrade-cerulean" or "upgrade-bill" or "upgrade-vermilion" or "upgrade-ssanne" or "upgrade-vermilion-gym" or "upgrade-city-services" or "upgrade-route11")
{
    string input=Path.GetFullPath(args[2]),output=Path.GetFullPath(args[3]);
    if(input==output||File.Exists(output)||Directory.Exists(output))throw new InvalidDataException("save_upgrade_requires_new_destination");
    string root=Directory.GetCurrentDirectory();
    bool repel=args[1]=="upgrade-repel",townMap=args[1]=="upgrade-town-map",oakRating=args[1]=="upgrade-oak-rating",route22=args[1]=="upgrade-route22",route3=args[1]=="upgrade-route3",magikarp=args[1]=="upgrade-magikarp",mtMoon=args[1]=="upgrade-mtmoon",cerulean=args[1]=="upgrade-cerulean",bill=args[1]=="upgrade-bill",vermilion=args[1]=="upgrade-vermilion",ssAnne=args[1]=="upgrade-ssanne",vermilionGym=args[1]=="upgrade-vermilion-gym",cityServices=args[1]=="upgrade-city-services",route11=args[1]=="upgrade-route11";
    PretCampaignCatalog Catalog(bool pickups,bool enableRepel=false,bool enableTownMap=false,bool enableOakRating=false,bool enableRoute22=false,bool enableRoute3=false,bool enableMagikarp=false,bool enableMtMoon=false,bool enableCerulean=false,bool enableBill=false,bool enableVermilion=false,bool enableSsAnne=false,bool enableVermilionGym=false,bool enableCityServices=false,bool enableRoute11=false)=>PretCampaignCatalog.Load(root,true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,includeFieldPickups:pickups,includeRepel:enableRepel,includeTownMap:enableTownMap,includeOakRating:enableOakRating,includeRoute22:enableRoute22,includeRoute3:enableRoute3,includeMagikarp:enableMagikarp,includeMtMoon:enableMtMoon,includeCerulean:enableCerulean,includeBill:enableBill,includeVermilion:enableVermilion,includeSsAnne:enableSsAnne,includeVermilionGym:enableVermilionGym,includeCityServices:enableCityServices,includeRoute11:enableRoute11);
    var source=route11?Catalog(true,true,true,true,true,true,true,true,true,true,true,true,true,true):cityServices?Catalog(true,true,true,true,true,true,true,true,true,true,true,true,true):vermilionGym?Catalog(true,true,true,true,true,true,true,true,true,true,true,true):ssAnne?Catalog(true,true,true,true,true,true,true,true,true,true,true):vermilion?Catalog(true,true,true,true,true,true,true,true,true,true):bill?Catalog(true,true,true,true,true,true,true,true,true):cerulean?Catalog(true,true,true,true,true,true,true,true):mtMoon?Catalog(true,true,true,true,true,true,true):Catalog(repel||townMap||oakRating||route22||route3||magikarp,townMap||oakRating||route22||route3||magikarp,oakRating||route22||route3||magikarp,route22||route3||magikarp,route3||magikarp,magikarp);
    var target=route11?Catalog(true,true,true,true,true,true,true,true,true,true,true,true,true,true,true):cityServices?Catalog(true,true,true,true,true,true,true,true,true,true,true,true,true,true):vermilionGym?Catalog(true,true,true,true,true,true,true,true,true,true,true,true,true):ssAnne?Catalog(true,true,true,true,true,true,true,true,true,true,true,true):vermilion?Catalog(true,true,true,true,true,true,true,true,true,true,true):bill?Catalog(true,true,true,true,true,true,true,true,true,true):cerulean?Catalog(true,true,true,true,true,true,true,true,true):mtMoon?Catalog(true,true,true,true,true,true,true,true):Catalog(true,repel||townMap||oakRating||route22||route3||magikarp,townMap||oakRating||route22||route3||magikarp,oakRating||route22||route3||magikarp,route22||route3||magikarp,route3||magikarp,magikarp);
    var bytes=File.ReadAllBytes(input);
    var result=route11?CampaignSaveUpgrade.CityServicesToRoute11(bytes,source,target):cityServices?CampaignSaveUpgrade.VermilionGymToCityServices(bytes,source,target):vermilionGym?CampaignSaveUpgrade.SsAnneToVermilionGym(bytes,source,target):ssAnne?CampaignSaveUpgrade.VermilionToSsAnne(bytes,source,target):vermilion?CampaignSaveUpgrade.BillToVermilion(bytes,source,target):bill?CampaignSaveUpgrade.CeruleanToBill(bytes,source,target):cerulean?CampaignSaveUpgrade.MtMoonToCerulean(bytes,source,target):mtMoon?CampaignSaveUpgrade.MagikarpToMtMoon(bytes,source,target):magikarp?CampaignSaveUpgrade.Route3ToMagikarp(bytes,source,target):route3?CampaignSaveUpgrade.Route22ToRoute3(bytes,source,target):route22?CampaignSaveUpgrade.OakRatingToRoute22(bytes,source,target):oakRating?CampaignSaveUpgrade.TownMapToOakRating(bytes,source,target):townMap?CampaignSaveUpgrade.RepelToTownMap(bytes,source,target):repel?CampaignSaveUpgrade.FieldPickupsToRepel(bytes,source,target):CampaignSaveUpgrade.StorageToFieldPickups(bytes,source,target);
    // Publish only a complete flushed file. Both creation and final move refuse
    // replacement; a write failure cannot leave a partial file at the new slot.
    string staged=Path.Combine(Path.GetDirectoryName(output)!,".firered-upgrade-"+Guid.NewGuid().ToString("N")+".tmp");
    try
    {
        using(var stream=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {stream.Write(result.Bytes);stream.Flush(true);}
        File.Move(staged,output,overwrite:false);
    }
    finally{if(File.Exists(staged))File.Delete(staged);}
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,input,output,result.Generation,result.PayloadHash,result.SourceRules,result.SourceContent,result.TargetRules,result.TargetContent},new JsonSerializerOptions{WriteIndented=true}));return 0;
}

if (args.Length == 3 && args[0] == "scenario" && args[1] == "replay-campaign-window")
    return CampaignWindowReplay.Run(Directory.GetCurrentDirectory(),Path.GetFullPath(args[2]));

if (args.Length == 4 && args[0] == "content" && args[1] == "inspect-pinned-map")
{
    using var sourceLock=JsonDocument.Parse(File.ReadAllText("content/source-lock.json"));
    var path=args[3];
    var file=sourceLock.RootElement.GetProperty("evidenceFiles").EnumerateArray().SingleOrDefault(e=>e.GetProperty("path").GetString()==path);
    if(file.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("source_path_not_locked");
    var json=File.ReadAllText(args[2]);using var mapJson=JsonDocument.Parse(json);
    var source=new SourceRef(SourceKind.Pret,path,mapJson.RootElement.GetProperty("id").GetString()!,"pret/pokefirered",RuleVersions.SourceCommit,file.GetProperty("sha256").GetString());
    var structure=PretMapImporter.ImportStructure(json,source);
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,structure.IsPlayable,structure.LayoutResolved,structure.ScriptConversionAttempted,structure.SourceRef,mapId=structure.Map.Id,scriptReferences=structure.ReferencedScriptSymbols,structure.Limitations},new JsonSerializerOptions{WriteIndented=true}));return 0;
}
if (args.Length == 3 && args[0] == "content")
{
    try
    {
        var json=File.ReadAllText(args[2]);
        if(args[1]=="validate")
        {
            var bundle=ContentJson.ParseAndValidate(json);
            Console.WriteLine(JsonSerializer.Serialize(new{passed=true,bundle.StableId,bundle.IsOriginalFixture,maps=bundle.Maps.Length,scripts=bundle.Scripts.Length,sha256=ContentJson.Sha256(bundle)},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }
        if(args[1]=="import-pret-map-fixture")
        {
            var source=new SourceRef(SourceKind.OriginalFixture,Path.GetFileName(args[2]),"original-structure-fixture");
            var structure=PretMapImporter.ImportStructure(json,source);
            Console.WriteLine(JsonSerializer.Serialize(structure,new JsonSerializerOptions{WriteIndented=true}));return 0;
        }
    }
    catch(ContentValidationException ex){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,diagnostics=ex.Diagnostics}));return 1;}
}
if (args.Length == 2 && args[0] == "scenario" && args[1] == "pallet")
{
    var map=PretWorldMap.Load("content/generated/pallet");
    SourceRef source(string path){var file=map.Metadata.Files.Single(f=>f.Path==path);return new(SourceKind.Pret,path,"PalletTown",map.Metadata.Repository,map.Metadata.Commit,file.Sha256);}
    const string scripts="data/maps/PalletTown/scripts.inc",texts="data/maps/PalletTown/text.inc";
    var dialogues=PretDialogueImporter.Import(File.ReadAllText("content/raw/pallet/"+scripts),File.ReadAllText("content/raw/pallet/"+texts),source(scripts),source(texts));
    string? interaction(TilePosition p)
    {
        var (dx,dy)=p.Facing switch{Direction.North=>(0,-1),Direction.South=>(0,1),Direction.East=>(1,0),_=>(-1,0)};
        return map.Events.GetProperty("bg_events").EnumerateArray().Where(e=>e.GetProperty("x").GetInt32()==p.X+dx&&e.GetProperty("y").GetInt32()==p.Y+dy).Select(e=>e.GetProperty("script").GetString()).FirstOrDefault();
    }
    var session=new ReferenceWorldSession(map,new(map.Id,6,8,3,Direction.South),map.Metadata.FullSourceHash,interaction,dialogues.Programs);
    var sequence=new[]{new ReferenceWorldCommand("move",Direction.West),new("move",Direction.West),new("move",Direction.North),new("interact")};
    var hashes=new List<string>();int i=0;
    foreach(var command in sequence){var result=session.Execute("pallet-test:"+(i++),session.Revision,command);if(result.Status==CommandStatus.Fault)throw new InvalidDataException(result.Error);hashes.Add(result.StateHash);}
    if(session.VisibleText!="RED's house")throw new InvalidDataException("source_sign_text_or_position_mismatch");
    if(session.ValidateSave(session.State)!="save_event_active")throw new InvalidDataException("unsafe_save_allowed");
    session.Execute("close",session.Revision,new("interact"));
    var bytes=SaveCodec.Encode(session.State,1,ReferenceWorldSession.Rules,map.Metadata.FullSourceHash);
    var loaded=SaveCodec.Decode<ReferenceWorldState>(bytes,ReferenceWorldSession.Rules,map.Metadata.FullSourceHash,validate:session.ValidateSave);
    if(CanonicalJson.Hash(loaded.State)!=CanonicalJson.Hash(session.State))throw new InvalidDataException("reference_save_hash_mismatch");
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,scenario="pallet-source-sign",map=map.Id.Value,map.Width,map.Height,sourceHash=map.Metadata.FullSourceHash,supportedReadOnlyScripts=dialogues.Programs.Count,unsupportedScripts=dialogues.UnsupportedScripts.Length,hashes,finalPosition=session.State.World.Player,scope="real map + read-only sign + save boundary; not main story"},new JsonSerializerOptions{WriteIndented=true}));return 0;
}

if (args.Length == 2 && args[0] == "scenario" && args[1] == "smoke")
{
    var map = new GridMap(new("fixture:lab"), 12, 8, Enumerable.Range(0, 96).Select(i => new TileCell(i%12==0||i%12==11||i/12==0||i/12==7)).ToImmutableArray());
    var content = new TestBattleContent();
    CombatantSnapshot actor(string id, BattleSide side, int attack) => new(new(id),new("fixture:creature"),side,5,new(22,attack,10,10,10,side==BattleSide.Player?12:9),ElementType.Normal,null,22,[new(new("fixture:strike"),35,35)]);
    var battle = BattleSnapshot.Create(new("fixture:battle"),EngineeringSession.Content,actor("fixture:player",BattleSide.Player,13),actor("fixture:opponent",BattleSide.Opponent,10),12345);
    var session = new EngineeringSession(map, content, battle);
    var hashes = new List<string>(); int commands=0;
    void apply(EngineeringCommand c) {var r=session.Execute("scenario:"+(commands++),session.Revision,c);if(r.Status!=CommandStatus.Accepted)throw new InvalidOperationException(r.Error);hashes.Add(r.StateHash);}
    apply(new("move",Direction.East)); apply(new("reward"));apply(new("reward"));apply(new("battle"));
    for(var i=0;session.State.Battle is not null && i<100;i++)apply(new("attack"));
    if(session.State.Battle is not null || session.State.Money!=100)throw new InvalidOperationException("scenario_invariant");
    var bytes=SaveCodec.Encode(session.State,1,EngineeringSession.Rules,EngineeringSession.Content);
    var loaded=SaveCodec.Decode<EngineeringState>(bytes,EngineeringSession.Rules,EngineeringSession.Content);
    if(CanonicalJson.Hash(loaded.State)!=CanonicalJson.Hash(session.State))throw new InvalidOperationException("save_hash_mismatch");
    var restored=new EngineeringSession(map,content,battle,loaded.State);var repeat=restored.Execute("after-load",restored.Revision,new("reward"));
    if(repeat.State.Money!=100)throw new InvalidOperationException("reward_duplicated_after_load");
    Console.WriteLine(JsonSerializer.Serialize(new{scenario="smoke",passed=true,fixture="original engineering fixture, not FireRed content",commands,hashes,finalHash=CanonicalJson.Hash(session.State),money=session.State.Money,battleResult=session.State.Message,saveBytes=bytes.Length,rewardIdempotentAfterLoad=true},new JsonSerializerOptions{WriteIndented=true}));return 0;
}
Console.Error.WriteLine("Usage: Game.Tooling scenario smoke; content commands are added with the validated Content module.");return 2;
sealed class TestBattleContent : IBattleContent {public MoveDefinition? FindMove(MoveId id)=>id.Value=="fixture:strike"?new(id,ElementType.Normal,40,100,35):null;}
