using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record FuchsiaGymTrainerDocument(int SchemaVersion,string Profile,string SourceCommit,bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves,ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames,
    ImmutableArray<string> Limitations);
/// <summary>Source-only loader. No catalog promotion, battle factory or campaign registration is implied.</summary>
public sealed record FuchsiaGymTrainerCatalogContent(string SourceCommit,string Profile,bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId,TrainerDefinition> Trainers,ImmutableDictionary<TrainerId,CeruleanTrainerBattleDefinition> Battles);
public static class PretFuchsiaGymTrainers
{
    public const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile="firered-fuchsia-gym-trainers-v1";
    public const string LockSha256="f2e2a8361f5b4a812613b09a9490fd1bf0265ecc005f93ebd1968ae5a869d068";
    public const string GeneratedSha256="c6a55c46c22ab1d03b5ef8cb52be51c9ecbc95b0bb7865c2664832509f6adc93";
    public const int SourceFileCount=36,TrainerCount=7,PartyMemberCount=18,RequiredMoveCount=24;
    public static FuchsiaGymTrainerDocument LoadDocument(string root)
    {
        using var sourceLock=JsonDocument.Parse(Read(root,"scripts/fuchsia-gym-trainers.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        Require(s.GetProperty("schemaVersion").GetInt32()==1&&s.GetProperty("repository").GetString()=="pret/pokefirered"&&
            s.GetProperty("commit").GetString()==SourceCommit&&s.GetProperty("tree").GetString()=="7515cd4067ea55f8cdbc73624478afd2736c58b9"&&
            s.GetProperty("profile").GetString()==Profile&&s.GetProperty("gameEdition").GetString()=="FIRERED"&&s.GetProperty("romRevision").GetInt32()==0&&
            s.GetProperty("files").GetArrayLength()==SourceFileCount,"Fuchsia Gym source identity differs.");
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!,hash=f.GetProperty("sha256").GetString()!;
            int length=f.GetProperty("byteLength").GetInt32();var b=Read(root,"content/raw/fuchsia-gym-trainers/"+p,hash,length);
            Require(files.TryAdd(p,hash)&&Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))==f.GetProperty("gitBlobSha1").GetString(),"Fuchsia Gym source Git blob differs.");
        }
        var d=ContentJson.ParseStrict<FuchsiaGymTrainerDocument>(Encoding.UTF8.GetString(Read(root,"content/generated/fuchsia-gym-trainers/trainers.json",GeneratedSha256)),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r)=>Require(files.TryGetValue(r.Path,out var h)&&r.Sha256==h&&r.StartLine>0&&r.EndLine>=r.StartLine&&!string.IsNullOrWhiteSpace(r.Symbol),"Fuchsia Gym source reference differs.");
        Require(d.SchemaVersion==1&&d.Profile==Profile&&d.SourceCommit==SourceCommit&&!d.CompleteSourceRoster&&d.Trainers.Length==7&&d.Battles.Length==7&&
            d.Trainers.Select(x=>x.Id).Distinct().Count()==7&&d.Trainers.Sum(x=>x.Party.Length)==18&&d.ResolvedMoves.GetArrayLength()==7&&d.Moves.Length==24&&d.MoveNames.Length==24&&!d.Limitations.IsDefaultOrEmpty,"Fuchsia Gym document scope differs.");
        string[] names=["JUGGLER_KAYDEN","JUGGLER_SHAWN","JUGGLER_KIRK","TAMER_EDGAR","TAMER_PHIL","JUGGLER_NATE","LEADER_KOGA"];
        for(int i=0;i<7;i++)
        {
            var t=d.Trainers[i];var b=d.Battles[i];bool koga=i==6,tamer=i is 3 or 4;
            string[] items=koga?["ITEM_HYPER_POTION","ITEM_HYPER_POTION","ITEM_FULL_HEAL","ITEM_NONE"]:["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"];
            Require(t.Id.Value=="pret:TRAINER_"+names[i]&&t.Id==b.Trainer&&!t.DoubleBattle&&t.PartyFlags==(koga?1:0)&&t.Items.SequenceEqual(items)&&t.AiFlags==(koga?7u:1u)&&
                b.Mode==(koga?1:0)&&b.RivalFlags==0&&b.BattleTypeFlags==8&&b.EntryScript=="EventScript_TryDoNormalTrainerBattle"&&b.Callback=="CB2_EndTrainerBattle"&&b.VictoryText==""&&
                b.PostBattleScript==(koga?"FuchsiaCity_Gym_EventScript_DefeatedKoga":null)&&b.MusicOption==(koga?"NO_MUSIC":null),"Fuchsia Gym trainer contract differs.");
            Source(t.Source);Source(t.MoneySource);Source(b.Source);var resolved=d.ResolvedMoves[i];
            Require(resolved.GetProperty("trainer").GetString()=="TRAINER_"+names[i]&&resolved.GetProperty("party").GetArrayLength()==t.Party.Length,"Fuchsia Gym resolved party differs.");
            for(int j=0;j<t.Party.Length;j++)
            {
                var p=t.Party[j];var r=resolved.GetProperty("party")[j];var moves=r.GetProperty("moves").EnumerateArray().Select(x=>"pret:"+x.GetString()).ToArray();
                Require(p.Species.Value=="pret:"+r.GetProperty("species").GetString()&&p.Level==r.GetProperty("level").GetInt32()&&p.HeldItem=="ITEM_NONE"&&p.SourceIv==(tamer?40:0)&&
                    (t.PartyFlags==0?p.CustomMoves.Length==0:p.CustomMoves.Select(x=>x.Value).SequenceEqual(moves)),"Fuchsia Gym source party member differs.");
                Source(p.Source);Source(p.SpeciesNameSource);Source(ContentJson.ParseStrict<TrainerSourceRef>(r.GetProperty("learnsetSource").GetRawText(),ContentJson.CreateOptions()));
            }
        }
        foreach(var m in d.Moves)Source(new(m.Source.Path,m.Source.Symbol,m.Source.StartLine,m.Source.EndLine,m.Source.Sha256));
        foreach(var m in d.MoveNames)Source(new(m.Source.Path,m.Source.Symbol,m.Source.StartLine,m.Source.EndLine,m.Source.Sha256));
        return d;
    }
    public static FuchsiaGymTrainerCatalogContent Load(string root)
    {var d=LoadDocument(root);return new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    private static byte[] Read(string root,string relative,string hash,int? length=null)
    {
        Require(!string.IsNullOrWhiteSpace(relative)&&!Path.IsPathRooted(relative)&&!relative.Contains(':')&&!relative.Contains('\\')&&!relative.Split('/').Any(p=>p is "" or "." or ".."),"Unsafe Fuchsia Gym source path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));Require(f.LinkTarget is null,"Fuchsia Gym symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent)Require(d.LinkTarget is null,"Fuchsia Gym parent symlink rejected.");
        int maximum=length??2*1024*1024;
        Require(maximum>0&&maximum<=4*1024*1024,"Fuchsia Gym source byte bound differs.");
        var bytes=PretRuntimeMapValidation.ReadBounded(f.FullName,maximum);
        Require((length is null||bytes.Length==length)&&Convert.ToHexStringLower(SHA256.HashData(bytes))==hash,"Fuchsia Gym pinned bytes differ: "+relative);return bytes;
    }
}
