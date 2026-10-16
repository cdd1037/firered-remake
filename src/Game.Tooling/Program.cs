using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Rules;
using Game.Domain.World;
using Game.Tooling;

if (args.Length > 0 && args[0] is "set-known-hatch-count" or "convert-current-checkpoint")
{
    try
    {
        if (args[0] == "set-known-hatch-count")
        {
            if (args.Length != 4 || !int.TryParse(args[3], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int count) || count is < 0 or > 0xffffff)
                throw new ArgumentException("Usage: set-known-hatch-count SOURCE NEW_DESTINATION KNOWN_LIFETIME_COUNT (0..16777215). No automatic estimate is available.");
            return KnownHatchCountConversion.Run(Directory.GetCurrentDirectory(), args[1], args[2], count);
        }
        if (args.Length != 3) throw new ArgumentException("Usage: convert-current-checkpoint SOURCE NEW_DESTINATION");
        return CurrentCheckpointConversion.Run(Directory.GetCurrentDirectory(), args[1], args[2]);
    }
    catch (Exception error) when (error is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(error.Message);
        return 2;
    }
}

if (args.Length >= 2 && args[0] == "save" && args[1].StartsWith("upgrade-", StringComparison.Ordinal))
{
    Console.Error.WriteLine("save_upgrade_retired: only explicit convert-current-checkpoint SOURCE NEW_DESTINATION is supported.");
    return 2;
}

if (args.Length == 4 && args[0] == "content" && args[1] == "inspect-pinned-map")
{
    using var sourceLock=JsonDocument.Parse(File.ReadAllText("scripts/recipes/config/source.json"));
    var path=args[3];
    var file=sourceLock.RootElement.GetProperty("evidenceFiles").EnumerateArray().SingleOrDefault(e=>e.GetProperty("path").GetString()==path);
    if(file.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("source_path_not_locked");
    var json=File.ReadAllText(args[2]);using var mapJson=JsonDocument.Parse(json);
    var source=new SourceRef(SourceKind.Pret,path,mapJson.RootElement.GetProperty("id").GetString()!,"pret/pokefirered",RuleVersions.SourceCommit);
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
if (args.Length == 2 && args[0] == "scenario" && args[1] == "smoke")
{
    var map = new GridMap(new("fixture:lab"), 12, 8, Enumerable.Range(0, 96).Select(i => new TileCell(i%12==0||i%12==11||i/12==0||i/12==7)).ToImmutableArray());
    var content = new TestBattleContent();
    CombatantSnapshot actor(string id, BattleSide side, int attack) => new(new(id),new("fixture:creature"),side,5,new(22,attack,10,10,10,side==BattleSide.Player?12:9),ElementType.Normal,null,22,[new(new("fixture:strike"),35,35)]);
    var battle = BattleSnapshot.Create(new("fixture:battle"),EngineeringSession.Content,actor("fixture:player",BattleSide.Player,13),actor("fixture:opponent",BattleSide.Opponent,10),12345);
    var session = new EngineeringSession(map, content, battle);
    var hashes = new List<string>(); int commands=0;
    void apply(EngineeringCommand c) {var r=session.Execute("scenario:"+(commands++),session.Revision,c);if(r.Status!=CommandStatus.Accepted)throw new InvalidOperationException(r.Error);hashes.Add(CanonicalJson.Hash(r.State));}
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
Console.Error.WriteLine("Usage: Game.Tooling set-known-hatch-count SOURCE NEW_DESTINATION KNOWN_LIFETIME_COUNT; convert-current-checkpoint SOURCE NEW_DESTINATION; scenario smoke; content validate FILE.");return 2;
sealed class TestBattleContent : IBattleContent {public MoveDefinition? FindMove(MoveId id)=>id.Value=="fixture:strike"?new(id,ElementType.Normal,40,100,35):null;}
