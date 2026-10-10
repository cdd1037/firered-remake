// Reconstructed recovery source. Exact source lock is regenerated from the fixed pret Git tree.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record CeladonApproachTrainerDocument(int SchemaVersion,string Profile,string SourceCommit,bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves,JsonElement DeferredDoubleBattles,ImmutableArray<string> Limitations);
public static class PretCeladonApproachTrainers
{
    public const string SourceCommit=PretCeladonApproachSource.Commit;
    public const string Profile="firered-celadon-approach-trainers-v1";
    public const int SourceFileCount=23,TrainerCount=11,PartyMemberCount=30;
    public const string LockSha256="de82c70926610128f70b0557f0ec1eec0f7b7946bb841b57acc7e88de516513d";
    public const string GeneratedSha256="fb510a963265bd2ade65d4ae93975aa2db05bdb11bb7e0c8306b0cb4034ed91f";
    public static CeladonApproachTrainerDocument LoadDocument(string root)
    {
        var source=PretCeladonApproachSource.Load(root,"celadon-approach-trainers",Profile,LockSha256,23);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/celadon-approach-trainers/trainers.json",GeneratedSha256);
        var d=ContentJson.ParseStrict<CeladonApproachTrainerDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        void Require(bool b,string e)=>PretCeladonApproachSource.Require(b,e);
        void Source(TrainerSourceRef s)=>source.Reference(s.Path,s.Sha256,s.StartLine,s.EndLine,s.Symbol);
        Require(d.SchemaVersion==1&&d.Profile==Profile&&d.SourceCommit==SourceCommit&&!d.CompleteSourceRoster&&d.Trainers.Length==11&&d.Battles.Length==11&&
            d.Trainers.Select(t=>t.Id).Distinct().Count()==11&&d.Battles.Select(b=>b.Trainer).Distinct().Count()==11&&d.Trainers.Sum(t=>t.Party.Length)==30&&d.ResolvedMoves.GetArrayLength()==11&&!d.Limitations.IsDefaultOrEmpty,"Celadon trainer scope differs.");
        for(int i=0;i<11;i++)
        {
            var t=d.Trainers[i];var b=d.Battles[i];var r=d.ResolvedMoves[i];
            Require(t.Id==b.Trainer&&t.Id.Value=="pret:"+r.GetProperty("trainer").GetString()&&b.ScriptEntry.StartsWith("Route8_EventScript_",StringComparison.Ordinal)&&
                !t.Party.IsDefaultOrEmpty&&t.Party.Length<=6&&t.PartyFlags is 0 or 1&&!t.DoubleBattle&&t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE",4))&&
                t.AiFlags==1&&t.AiFlagSymbols.SequenceEqual(["AI_SCRIPT_CHECK_BAD_MOVE"])&&b.Mode==0&&b.RivalFlags==0&&b.BattleTypeFlags==8&&b.VictoryText==""&&
                b.EntryScript=="EventScript_TryDoNormalTrainerBattle"&&b.Callback=="CB2_EndTrainerBattle"&&b.PostBattleScript is null&&b.MusicOption is null&&r.GetProperty("party").GetArrayLength()==t.Party.Length,"Celadon original trainer contract differs.");
            Source(t.Source);Source(t.MoneySource);Source(b.Source);
            for(int j=0;j<t.Party.Length;j++)
            {
                var p=t.Party[j];var resolved=r.GetProperty("party")[j];var moves=resolved.GetProperty("moves").EnumerateArray().Select(x=>x.GetString()!).ToArray();
                Require(p.HeldItem=="ITEM_NONE"&&p.Level is >=1 and <=100&&p.SourceIv is >=0 and <=255&&!p.CustomMoves.IsDefault&&p.Species.Value=="pret:"+resolved.GetProperty("species").GetString()&&
                    p.Level==resolved.GetProperty("level").GetInt32()&&moves.Length is >=1 and <=4&&
                    (t.PartyFlags==0?p.CustomMoves.Length==0:p.CustomMoves.Length==4&&p.CustomMoves.Select(x=>x.Value).SequenceEqual(moves.Select(x=>"pret:"+x))),"Celadon trainer source member differs.");
                Source(p.Source);Source(p.SpeciesNameSource);Source(ContentJson.ParseStrict<TrainerSourceRef>(resolved.GetProperty("learnsetSource").GetRawText(),ContentJson.CreateOptions()));
            }
        }
        var doubles=d.DeferredDoubleBattles;
        Require(doubles.GetArrayLength()==1&&doubles[0].GetProperty("trainer").GetString()=="TRAINER_TWINS_ELI_ANNE"&&!doubles[0].GetProperty("executable").GetBoolean()&&doubles[0].GetProperty("scripts").GetArrayLength()==2,"Original deferred double contract differs.");
        foreach(var item in new[]{doubles[0].GetProperty("trainerDeclaration"),doubles[0].GetProperty("partyDeclaration")}.Concat(doubles[0].GetProperty("scripts").EnumerateArray()))Source(ContentJson.ParseStrict<TrainerSourceRef>(item.GetProperty("source").GetRawText(),ContentJson.CreateOptions()));
        return d;
    }
    public static CeladonApproachTrainerCatalog Load(string root)
    {var d=LoadDocument(root);return new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(t=>t.Trainer));}
}
