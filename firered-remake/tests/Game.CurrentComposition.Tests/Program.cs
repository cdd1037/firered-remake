using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

if(args.Length is not (2 or 4))throw new ArgumentException("ROOT [HANDOFF_SAVE BILL_SAVE] OUTPUT_DIRECTORY");
using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"baseline-a52074b.json")));
var reference=baseline.RootElement;
string predecessorContentVersion=reference.GetProperty("ContentVersion").GetString()!;
string output=args[^1];
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new InvalidDataException(name);checks.Add(name);}
void Refuse(Action action,string fault,string name)
{
    try{action();}catch(Exception ex)when(ex is InvalidDataException or InvalidOperationException)
    {Check(ex.Message.Contains(fault,StringComparison.Ordinal),name+": "+ex.Message);return;}
    throw new InvalidDataException("accepted invalid case: "+name);
}
var environment=new Dictionary<string,string?>{{"FIRERED_PROJECT_ROOT",args[0]}};
GameStartupSelection Resolve(string[] flags,params (string Key,string Value)[] overrides)
{
    var env=new Dictionary<string,string?>(environment);
    foreach(var item in overrides)env[item.Key]=item.Value;
    return GameStartupSelection.Resolve(flags,env);
}
Check(Resolve([], ("FIRERED_EVIDENCE","evidence.json")).Mode==GameStartupMode.Current,"default is current with ordinary evidence settings");
Check(Resolve(["--profile=current"]).Mode==GameStartupMode.Current,"explicit current CLI");
Check(Resolve([], ("FIRERED_PROFILE","current")).Mode==GameStartupMode.Current,"explicit current environment");
Check(Resolve(["--profile","current"],("FIRERED_PROFILE","current")).Mode==GameStartupMode.Current,"matching CLI and environment");
Check(Resolve(["--story"]).Mode==GameStartupMode.Current,"generic story alias selects current");
Check(Resolve(["--story-current"]).Mode==GameStartupMode.Current,"current story alias selects current");
Check(Resolve(["--profile=current","--profile","current"]).Mode==GameStartupMode.Current,"matching repeated selectors agree");
Refuse(()=>Resolve(["--probe"],("FIRERED_CAMPAIGN_CHECKPOINT","save.json")),"startup_checkpoint_requires_current","checkpoint cannot enter diagnostic mode");
Refuse(()=>Resolve(["--profile=current"],("FIRERED_PROFILE","celio")),"startup_profile_conflict","CLI and env conflict is explicit");
Refuse(()=>Resolve(["--profile=current","--profile=celio"]),"startup_profile_conflict","duplicate conflicting CLI");
Refuse(()=>Resolve(["--profile"]),"startup_profile_missing","missing CLI value");
Refuse(()=>Resolve(["--profile="]),"startup_profile_missing","empty CLI value");
Refuse(()=>Resolve(["--profile=unknown"]),"startup_profile_unsupported","unknown profile refusal");
Refuse(()=>Resolve(["--story-celio"]),"startup_profile_retired","old Celio launch refusal");
Refuse(()=>Resolve(["--story-five-island"]),"startup_profile_retired","old regional launch refusal");
Refuse(()=>Resolve([], ("FIRERED_CELIO","1")),"startup_selector_retired","old current environment selector refusal");
Refuse(()=>Resolve([], ("FIRERED_CAPTURE","0")),"startup_selector_retired","old disabled feature switch refusal");
Refuse(()=>Resolve([], ("FIRERED_SURF_CONTENT_ROOT","another-root")),"startup_selector_retired","old alternate content root refusal");
Refuse(()=>Resolve(["--harness","--profile=current"]),"startup_mode_conflict","diagnostic and current conflict");
Refuse(()=>Resolve(["--harness","--probe"]),"startup_mode_conflict","two diagnostic modes conflict");
Refuse(()=>Resolve([], ("FIRERED_COMPACT_MAPS","0")),"startup_compact_maps_required","unsupported representation override");
Refuse(()=>Resolve([], ("FIRERED_PROJECT_ROOT","")),"FIRERED_PROJECT_ROOT","missing root refusal");
Check(Resolve(["--probe"],("FIRERED_PROJECT_ROOT","")).Mode==GameStartupMode.Probe,"explicit engineering probe remains isolated");
Check(Resolve([], ("FIRERED_CAMPAIGN_CHECKPOINT","checkpoint.json")).Checkpoint=="checkpoint.json","checkpoint resolved once");

var catalog=PretCampaignCatalog.LoadCurrent(args[0]);
var current=CampaignSession.CreateCurrent(catalog,new(0x11223344));
// a52074b records the pre-Cave payload/refactor evidence. It is not the source fingerprint of this expanded game.
Check(catalog.ContentVersion.Length==64&&catalog.ContentVersion.All(c=>char.IsAsciiHexDigitLower(c)),"current content fingerprint is SHA256");
Check(catalog.ContentVersion!=predecessorContentVersion,"Cave source extension has a distinct content/save fingerprint");
Check(catalog.CeruleanCaveEnabled&&catalog.Maps.Count==257&&catalog.FieldPickups.Length==244,"current composition includes 257 maps and 244 field pickups");
Check(catalog.CeruleanCaveRegion.NewMaps.Keys.Select(id=>id.Value).ToHashSet(StringComparer.Ordinal).SetEquals(
    new[]{"map.cerulean-cave-1f","map.cerulean-cave-2f","map.cerulean-cave-b1f"})&&
    catalog.CeruleanCaveRegion.NewMaps.All(pair=>ReferenceEquals(catalog.SourceMaps[pair.Key],pair.Value)),"three authenticated Cave source maps belong to the current composition");
Check(catalog.PokemonContent!.Catalog.Species.Count==139&&catalog.CeruleanCaveOpponents.Catalog.Species.Count==163&&
    ReferenceEquals(catalog.PokemonContent.Catalog,catalog.CeruleanCaveOwnership.Catalog)&&
    ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(catalog.PokemonContent.Catalog),catalog.CeruleanCaveOwnership)&&
    ReferenceEquals(catalog.CeruleanCaveOwnership.Predecessor,catalog.CelioOwnership),"current player catalog has the authenticated 139-species Cave ownership proof");
Check(ReferenceEquals(CeruleanCaveOpponentPokemonCatalog.For(catalog.CeruleanCaveOwnership),catalog.CeruleanCaveOpponents)&&
    ReferenceEquals(catalog.CeruleanCaveOpponents.Predecessor,catalog.CelioOpponents)&&
    !ReferenceEquals(catalog.CeruleanCaveOpponents.Catalog,catalog.PokemonContent.Catalog),"Cave predecessor opponent catalog remains distinct and authenticated");
Check(ReferenceEquals(CeruleanCaveMachineCatalog.For(catalog.PewterMachines),catalog.CeruleanCaveMachines)&&
    ReferenceEquals(catalog.CeruleanCaveMachines.Pokemon,catalog.CeruleanCaveOwnership)&&
    ReferenceEquals(catalog.CeruleanCaveMachines.Predecessor,catalog.CelioMachines),"current machines share the Cave ownership and Celio predecessor proofs");
Check(catalog.LeagueRematchEnabled&&catalog.LeagueRematchOpponents!.Catalog.Species.Count==168&&
    ReferenceEquals(LeagueRematchOpponentPokemonCatalog.For(catalog.CeruleanCaveOwnership),catalog.LeagueRematchOpponents)&&
    ReferenceEquals(catalog.LeagueRematchOpponents.Predecessor,catalog.CeruleanCaveOpponents)&&
    ReferenceEquals(catalog.LeagueRematchOpponents.Owned,catalog.CeruleanCaveOwnership),"current rematch opponents extend Cave163 to authenticated168 without changing owned139");
Check(catalog.LeagueRematchTrainerParties!.Trainers.Count==7&&catalog.PokemonLeagueTrainerParties!.Trainers.Count==7,
    "current composition retains both seven-team source rosters");
foreach(var definition in catalog.PokemonLeagueTrainerParties!.Trainers.Values.Concat(catalog.LeagueRematchTrainerParties!.Trainers.Values)
    .Concat(catalog.ForestTrainers!.Trainers.Values).Concat(catalog.SilphTrainers!.Trainers.Values).Where(t=>!t.DoubleBattle))
    Check(ReferenceEquals(current.GetOrdinaryTrainerDefinition(definition.Id),definition),"read-only trainer lookup shares authenticated entry definition: "+definition.Id.Value);
var reloaded=PretCampaignCatalog.LoadCurrent(args[0]);
Check(!ReferenceEquals(catalog,reloaded)&&catalog.ContentVersion==reloaded.ContentVersion,"independent authenticated source loads produce the same current fingerprint");
object PokemonSnapshot(PokemonCatalog value)=>new{
    value.SourceCommit,
    species=value.Species.OrderBy(pair=>pair.Key.Value,StringComparer.Ordinal).Select(pair=>new{id=pair.Key.Value,definition=pair.Value}).ToArray(),
    moves=value.Moves.OrderBy(pair=>pair.Key.Value,StringComparer.Ordinal).Select(pair=>new{id=pair.Key.Value,definition=pair.Value}).ToArray()
};
string CompositionHash(PretCampaignCatalog value)=>CanonicalJson.Hash(new{
    maps=value.SourceMaps.OrderBy(pair=>pair.Key.Value,StringComparer.Ordinal).Select(pair=>new{
        id=pair.Key.Value,pair.Value.Width,pair.Value.Height,pair.Value.SourceRef,pair.Value.Metadata,pair.Value.Events,pair.Value.Artifacts
    }).ToArray(),
    scripts=value.ScriptCompilation,
    owned=PokemonSnapshot(value.PokemonContent!.Catalog),
    opponents=PokemonSnapshot(value.LeagueRematchOpponents!.Catalog),
    machines=value.PewterMachines,
    pickups=value.FieldPickups
});
string compositionHash=CompositionHash(catalog);
Check(compositionHash==CompositionHash(reloaded),"current maps, scripts, Pokemon, machines and pickups load deterministically");
Check(current.IsCurrentGame&&current.RulesVersion==CurrentGameProfile.RulesVersion,"normal factory uses current rules");
Check(typeof(CampaignSession).GetMethod("CreateDiagnostic") is null&&typeof(CampaignSession).GetMethod("RestoreDiagnostic") is null,"historical factories removed from Application API");
Check(CanonicalJson.Hash(current.State)==reference.GetProperty("newGameHash").GetString(),"new game payload and both RNG streams match fixed a52074b baseline");
Check(CanonicalJson.Hash(CampaignSession.CreateCurrent(reloaded,new(0x11223344)).State)==CanonicalJson.Hash(current.State),"independent current compositions produce the same New Game payload");
var convenience=new CampaignSession(catalog,new(0x11223344));
Check(convenience.IsCurrentGame&&convenience.RulesVersion==current.RulesVersion&&CanonicalJson.Hash(convenience.State)==CanonicalJson.Hash(current.State),"constructor has current-only semantics");
Check(current.ValidatePlayerSave(current.State) is null,"current New Game is strictly saveable");
Directory.CreateDirectory(output);
var savePath=Path.Combine(output,"new-current.json");
var storage=new TestFileStorage();
new SaveService(storage).Save(savePath,current.State,current.Revision,current.RulesVersion,catalog.ContentVersion,current.ValidatePlayerSave);
var saved=new SaveService(storage).Load<CampaignState>(savePath,current.RulesVersion,catalog.ContentVersion,current.ValidatePlayerSave);
var continued=CampaignSession.RestoreCurrent(catalog,saved.State,saved.Generation);
Check(continued.IsCurrentGame&&CanonicalJson.Hash(continued.State)==CanonicalJson.Hash(current.State),"new current save strictly Continues");
foreach(string oldRules in new[]{CampaignSession.CelioRulesVersion,CampaignSession.FiveIslandRulesVersion,"unknown"})
{
    var bytes=SaveCodec.Encode(current.State,0,oldRules,catalog.ContentVersion);
    Refuse(()=>SaveCodec.Decode<CampaignState>(bytes,current.RulesVersion,catalog.ContentVersion,validate:current.ValidatePlayerSave),"save_version_mismatch","obsolete rules rejected: "+oldRules);
}
var converted=new List<object>();
void WriteNewCheckpoint(string source,string destination,byte[] bytes)
{
    if(Path.GetFullPath(source)==Path.GetFullPath(destination)||File.Exists(destination)||Directory.Exists(destination))
        throw new InvalidDataException("checkpoint_conversion_requires_new_destination");
    using var stream=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None);
    stream.Write(bytes);stream.Flush(true);
}
foreach(string path in args.Length==4?args.Skip(1).Take(2):[])
{
    var original=File.ReadAllBytes(path);var sha=Convert.ToHexStringLower(SHA256.HashData(original));
    var frozen=reference.GetProperty("checkpoints").EnumerateArray().SingleOrDefault(x=>x.GetProperty("sourceSha256").GetString()==sha);
    Check(frozen.ValueKind==JsonValueKind.Object,"selected source bytes match an immutable a52074b checkpoint");
    using var oldJson=JsonDocument.Parse(original);
    string sourceContentVersion=oldJson.RootElement.GetProperty("ContentVersion").GetString()!;
    Check(sourceContentVersion==predecessorContentVersion,"selected source envelope explicitly names the authenticated predecessor content");
    var before=SaveCodec.Decode<CampaignState>(original,CampaignSession.CelioRulesVersion,sourceContentVersion,validate:current.ValidatePlayerSave);
    Check(before.Generation==frozen.GetProperty("generation").GetInt64(),"source generation matches fixed baseline: "+before.Generation);
    Check(CanonicalJson.Hash(before.State)==frozen.GetProperty("payloadHash").GetString(),"source payload matches fixed baseline: "+before.Generation);
    Refuse(()=>SaveCodec.Decode<CampaignState>(original,current.RulesVersion,catalog.ContentVersion,validate:current.ValidatePlayerSave),"save_version_mismatch","original checkpoint rejected as current: "+before.Generation);
    Refuse(()=>SaveCodec.Decode<CampaignState>(original,CampaignSession.CelioRulesVersion,catalog.ContentVersion,validate:current.ValidatePlayerSave),"save_version_mismatch","predecessor content is not silently treated as current: "+before.Generation);
    var oldContent=SaveCodec.Encode(before.State,before.Generation,current.RulesVersion,sourceContentVersion);
    Refuse(()=>SaveCodec.Decode<CampaignState>(oldContent,current.RulesVersion,catalog.ContentVersion,validate:current.ValidatePlayerSave),"save_version_mismatch","current rules alone cannot bypass predecessor content rejection: "+before.Generation);
    var encoded=SaveCodec.Encode(before.State,before.Generation,current.RulesVersion,catalog.ContentVersion);
    var after=SaveCodec.Decode<CampaignState>(encoded,current.RulesVersion,catalog.ContentVersion,validate:current.ValidatePlayerSave);
    var restored=CampaignSession.RestoreCurrent(catalog,after.State,after.Generation);
    using var newJson=JsonDocument.Parse(encoded);
    Check(CanonicalJson.Hash(oldJson.RootElement.GetProperty("Payload"))==CanonicalJson.Hash(newJson.RootElement.GetProperty("Payload")),"entire checkpoint payload unchanged: "+before.Generation);
    Check(restored.Revision==before.Generation&&CanonicalJson.Hash(restored.State)==CanonicalJson.Hash(before.State),"Restore has no entry or RNG side effect: "+before.Generation);
    var newStep=restored.Execute("composition-smoke",restored.Revision,new("move",Direction.East));
    Check(newStep.Status==CommandStatus.Accepted,"public field move accepted: "+before.Generation);
    Check(CanonicalJson.Hash(newStep)==frozen.GetProperty("moveOutcomeHash").GetString(),"complete command result, state, events and RNG match fixed baseline: "+before.Generation);
    string destination=Path.Combine(output,"converted-"+before.Generation+".json");
    Refuse(()=>WriteNewCheckpoint(path,path,encoded),"checkpoint_conversion_requires_new_destination","conversion refuses the source as destination: "+before.Generation);
    WriteNewCheckpoint(path,destination,encoded);
    Refuse(()=>WriteNewCheckpoint(path,destination,encoded),"checkpoint_conversion_requires_new_destination","conversion refuses an existing destination: "+before.Generation);
    Check(File.ReadAllBytes(destination).AsSpan().SequenceEqual(encoded),"verified conversion bytes published without replacement: "+before.Generation);
    Check(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))==sha,"original checkpoint bytes preserved: "+before.Generation);
    converted.Add(new{path,destination,generation=before.Generation,sourceContentVersion,targetContentVersion=catalog.ContentVersion,sourceSha256=sha,payloadHash=CanonicalJson.Hash(before.State),currentSha256=Convert.ToHexStringLower(SHA256.HashData(encoded))});
}
ICampaignCatalog Tamper(string member,object? value)
{
    var proxy=DispatchProxy.Create<ICampaignCatalog,CatalogProxy>();
    ((CatalogProxy)proxy).Target=catalog;((CatalogProxy)proxy).Member=member;((CatalogProxy)proxy).Value=value;
    return proxy;
}
foreach(var tamper in new (string Member,object? Value,string Fault)[]{
    ("LeagueRematchEnabled",false,"current_requires_league_rematch_catalog"),
    ("LeagueRematchTrainerParties",null,"pokemon_league_rematch_catalog_scope"),
    ("LeagueRematchOpponents",null,"pokemon_league_rematch_catalog_scope"),
    ("CeruleanCaveEnabled",false,"current_capabilities_missing"),
    ("CelioEnabled",false,"current_capabilities_missing"),
    ("Route11Enabled",false,"current_capabilities_missing"),
    ("PokemonContent",catalog.PokemonContent! with{Catalog=catalog.CeruleanCaveOpponents.Catalog},"cerulean_cave_source_binding"),
    ("PokemonContent",catalog.PokemonContent! with{Catalog=catalog.CeruleanCaveOwnership.Catalog with{}},"cerulean_cave_source_binding"),
    ("PokemonContent",catalog.PokemonContent! with{Catalog=catalog.CelioOwnership!.Catalog},"cerulean_cave_source_binding"),
    ("CeruleanCaveOwnership",null,"cerulean_cave_source_binding"),
    ("CeruleanCaveOpponents",null,"cerulean_cave_source_binding"),
    ("CeruleanCaveMachines",null,"cerulean_cave_source_binding"),
    ("CelioOpponents",null,"cerulean_cave_source_binding"),
    ("CelioRoamerMaps",null,"celio_source_profile_missing"),
    ("PewterMachines",catalog.CelioMachines!.Catalog,"cerulean_cave_source_binding"),
    ("EarlyShop",catalog.SixIslandShop!.Catalog,"five_island_source_profile_missing")})
{
    var bad=Tamper(tamper.Member,tamper.Value);
    Refuse(()=>CampaignSession.CreateCurrent(bad),tamper.Fault,"New Game rejects tampered "+tamper.Member);
    Refuse(()=>CampaignSession.RestoreCurrent(bad,current.State,0),tamper.Fault,"Restore rejects tampered "+tamper.Member);
}
var unsupported=Tamper("LeagueRematchEnabled",false);
Refuse(()=>new CampaignSession(unsupported),"current_requires_league_rematch_catalog","constructor refuses predecessor-only composition");
Refuse(()=>CampaignSession.Restore(unsupported,current.State,0),"current_requires_league_rematch_catalog","Restore convenience refuses predecessor-only composition");
Check(checks.Count>=(args.Length==4?100:70),"nonempty focused composition suite");
Console.WriteLine(JsonSerializer.Serialize(new{passed=checks.Count,runtime=Environment.Version.ToString(),rules=current.RulesVersion,catalog.ContentVersion,
    currentComposition=new{maps=catalog.Maps.Count,ownedSpecies=catalog.PokemonContent!.Catalog.Species.Count,opponentSpecies=catalog.LeagueRematchOpponents!.Catalog.Species.Count,fieldPickups=catalog.FieldPickups.Length,compositionHash},
    baselineCommit=reference.GetProperty("sourceCommit").GetString(),predecessorContentVersion,
    baselineScope="Immutable pre-Cave evidence: New Game payload and the named checkpoint payloads/commands only; the expanded source fingerprint is tested independently.",
    conversionScope="Test-local explicit conversion of the two byte-pinned Celio checkpoints; not a product CLI conversion or automatic save migration.",
    converted,checks},new JsonSerializerOptions{WriteIndented=true}));

public class CatalogProxy:DispatchProxy
{
    public ICampaignCatalog Target=null!;public string Member="";public object? Value;
    protected override object? Invoke(MethodInfo? method,object?[]? args)
    {
        if(method?.Name=="get_"+Member)return Value;
        try{return method!.Invoke(Target,args);}catch(TargetInvocationException ex)when(ex.InnerException is not null){throw ex.InnerException;}
    }
}

public sealed class TestFileStorage:ISaveStorage
{
    public byte[]? Read(string slot)=>File.Exists(slot)?File.ReadAllBytes(slot):null;
    public void WriteStaged(string slot,byte[] bytes)=>File.WriteAllBytes(slot+".tmp",bytes);
    public void Commit(string slot,bool preservePrimary)
    {
        if(preservePrimary&&File.Exists(slot))File.Copy(slot,slot+".bak",overwrite:true);
        File.Move(slot+".tmp",slot,overwrite:true);
    }
}
