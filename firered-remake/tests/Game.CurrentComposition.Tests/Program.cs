using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.World;

if (args.Length is not (2 or 3)) throw new ArgumentException("ROOT OUTPUT_DIRECTORY [CHECKPOINT]");
string root=Path.GetFullPath(args[0]), output=Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new InvalidDataException(name);checks.Add(name);}
var startup=GameStartupSelection.Resolve([],new Dictionary<string,string?>{{"FIRERED_PROJECT_ROOT",root}});
Check(startup.Mode==GameStartupMode.Current,"default selects current game");
var catalog=PretCampaignCatalog.LoadCurrent(root);
var session=CampaignSession.CreateCurrent(catalog,new(0x11223344));
Check(catalog.ContentVersion=="firered-current-content-v3","explicit content compatibility version");
Check(catalog.Maps.ContainsKey(new("map.saffron-city"))&&catalog.Maps.ContainsKey(PretLostCaveRegion.EntranceMapId),"expanded regions loaded");
Check(catalog.PokemonContent!.Catalog.Moves.Count==354,"all move definitions loaded");
Check(session.ValidatePlayerSave(session.State) is null,"new game is saveable");
var encoded=SaveCodec.Encode(session.State,session.Revision,session.RulesVersion,catalog.ContentVersion);
var saved=SaveCodec.Decode<CampaignState>(encoded,session.RulesVersion,catalog.ContentVersion,validate:session.ValidatePlayerSave);
var continued=CampaignSession.RestoreCurrent(catalog,saved.State,saved.Generation);
Check(continued.Revision==session.Revision&&CanonicalJson.Hash(continued.State)==CanonicalJson.Hash(session.State),"new game Continue preserves state and RNG");
var before=continued.State;
var move=continued.Execute("composition-smoke",continued.Revision,new("move",Direction.East));
Check(move.Status==CommandStatus.Accepted,"public movement command accepted after Continue");
Check(continued.ValidatePlayerSave(continued.State) is null,"movement result remains saveable");
object? checkpoint=null;
if(args.Length==3)
{
    // One-off checkpoint conversion: old bytes stay untouched; only the envelope version changes.
    byte[] source=File.ReadAllBytes(args[2]);
    using var document=JsonDocument.Parse(source);
    string oldVersion=document.RootElement.GetProperty("ContentVersion").GetString()!;
    string oldRules=document.RootElement.GetProperty("RulesVersion").GetString()!;
    var old=SaveCodec.Decode<CampaignState>(source,oldRules,oldVersion,validate:session.ValidatePlayerSave);
    Check(oldRules==session.RulesVersion,"checkpoint uses current gameplay rules");
    byte[] converted=SaveCodec.Encode(old.State,old.Generation,oldRules,catalog.ContentVersion);
    var reread=SaveCodec.Decode<CampaignState>(converted,oldRules,catalog.ContentVersion,validate:session.ValidatePlayerSave);
    var restored=CampaignSession.RestoreCurrent(catalog,reread.State,reread.Generation);
    Check(CanonicalJson.Hash(restored.State)==CanonicalJson.Hash(old.State)&&restored.Revision==old.Generation,"checkpoint Continue preserves entire payload and generation");
    Check(restored.ValidatePlayerSave(restored.State) is null,"continued checkpoint remains valid");
    using var convertedJson=JsonDocument.Parse(converted);
    Check(JsonElement.DeepEquals(document.RootElement.GetProperty("Payload"),convertedJson.RootElement.GetProperty("Payload")),"conversion changes no payload fields");
    string destination=Path.Combine(output,"continued-checkpoint.json");
    using(var file=new FileStream(destination,FileMode.CreateNew,FileAccess.Write))file.Write(converted);
    Check(File.ReadAllBytes(args[2]).AsSpan().SequenceEqual(source),"source checkpoint preserved");
    checkpoint=new{source=args[2],destination,old.Generation,map=restored.State.World.Player.MapId.Value,oldVersion,newVersion=catalog.ContentVersion};
}
Console.WriteLine(JsonSerializer.Serialize(new{passed=checks.Count,maps=catalog.Maps.Count,species=catalog.PokemonContent.Catalog.Species.Count,catalog.ContentVersion,checkpoint,checks},new JsonSerializerOptions{WriteIndented=true}));
