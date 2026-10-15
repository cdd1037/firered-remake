using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record CeladonGymTrainerDocument(int SchemaVersion,string Profile,string SourceCommit,bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves,ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames,
    ImmutableArray<string> Limitations);
/// <summary>Source-only loader. No catalog promotion, battle factory or campaign registration is implied.</summary>
public static class PretCeladonGymTrainers
{
    public const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile="firered-celadon-gym-trainers-v1";
    public const int TrainerCount=8,PartyMemberCount=21,RequiredMoveCount=17;
    public static CeladonGymTrainerDocument LoadDocument(string root)
    {
        var d=ContentJson.ParseStrict<CeladonGymTrainerDocument>(Encoding.UTF8.GetString(Read(root,"content/generated/celadon-gym-trainers/trainers.json")),ContentJson.CreateOptions());
        Require(d.SchemaVersion==1&&!d.CompleteSourceRoster&&
            d.Trainers.Select(x=>x.Id).Distinct().Count()==8&&d.Trainers.Sum(x=>x.Party.Length)==21&&d.ResolvedMoves.GetArrayLength()==8&&!d.Limitations.IsDefaultOrEmpty,"Celadon Gym document scope differs.");
        string[] names=["LASS_KAY","BEAUTY_BRIDGET","PICNICKER_TINA","BEAUTY_TAMIA","BEAUTY_LORI","LASS_LISA","COOLTRAINER_MARY","LEADER_ERIKA"];
        for(int i=0;i<8;i++)
        {
            var t=d.Trainers[i];var b=d.Battles[i];bool erika=i==7,mary=i==6;
            string[] items=erika?["ITEM_HYPER_POTION","ITEM_FULL_HEAL","ITEM_NONE","ITEM_NONE"]:mary?["ITEM_SUPER_POTION","ITEM_NONE","ITEM_NONE","ITEM_NONE"]:["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"];
            Require(t.Id.Value=="pret:TRAINER_"+names[i]&&t.Id==b.Trainer&&!t.DoubleBattle&&t.PartyFlags==(erika||mary?1:0)&&t.Items.SequenceEqual(items)&&t.AiFlags==(erika||mary?7u:1u)&&
                b.Mode==(erika?1:0)&&b.RivalFlags==0&&b.BattleTypeFlags==8&&b.EntryScript=="EventScript_TryDoNormalTrainerBattle"&&b.Callback=="CB2_EndTrainerBattle"&&b.VictoryText==""&&
                b.PostBattleScript==(erika?"CeladonCity_Gym_EventScript_DefeatedErika":null)&&b.MusicOption==(erika?"NO_MUSIC":null),"Celadon Gym trainer contract differs.");
var resolved=d.ResolvedMoves[i];
            Require(resolved.GetProperty("trainer").GetString()=="TRAINER_"+names[i]&&resolved.GetProperty("party").GetArrayLength()==t.Party.Length,"Celadon Gym resolved party differs.");
            for(int j=0;j<t.Party.Length;j++)
            {
                var p=t.Party[j];var r=resolved.GetProperty("party")[j];var moves=r.GetProperty("moves").EnumerateArray().Select(x=>"pret:"+x.GetString()).ToArray();
                Require(p.Species.Value=="pret:"+r.GetProperty("species").GetString()&&p.Level==r.GetProperty("level").GetInt32()&&p.HeldItem=="ITEM_NONE"&&p.SourceIv==(mary?100:0)&&
                    (t.PartyFlags==0?p.CustomMoves.Length==0:p.CustomMoves.Select(x=>x.Value).SequenceEqual(moves)),"Celadon Gym source party member differs.");
            }
        }
        return d;
    }
    public static CeladonGymTrainerCatalog Load(string root)
    {var d=LoadDocument(root);return new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    private static byte[] Read(string root,string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative)&&!Path.IsPathRooted(relative)&&!relative.Contains(':')&&!relative.Contains('\\')&&!relative.Split('/').Any(p=>p is "" or "." or ".."),"Unsafe Celadon Gym source path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));Require(f.LinkTarget is null,"Celadon Gym symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent)Require(d.LinkTarget is null,"Celadon Gym parent symlink rejected.");
        var bytes=File.ReadAllBytes(f.FullName);
return bytes;
    }
}
