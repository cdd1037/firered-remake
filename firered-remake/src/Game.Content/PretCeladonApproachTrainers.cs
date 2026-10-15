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
    public const int TrainerCount=11,PartyMemberCount=30;
    public static CeladonApproachTrainerDocument LoadDocument(string root)
    {
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/celadon-approach-trainers/trainers.json");
        var d=ContentJson.ParseStrict<CeladonApproachTrainerDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        void Require(bool b,string e)=>PretCeladonApproachSource.Require(b,e);
        Require(d.SchemaVersion==1&&!d.CompleteSourceRoster&&
            d.Trainers.Select(t=>t.Id).Distinct().Count()==11&&d.Battles.Select(b=>b.Trainer).Distinct().Count()==11&&d.Trainers.Sum(t=>t.Party.Length)==30&&d.ResolvedMoves.GetArrayLength()==11&&!d.Limitations.IsDefaultOrEmpty,"Celadon trainer scope differs.");
        for(int i=0;i<11;i++)
        {
            var t=d.Trainers[i];var b=d.Battles[i];var r=d.ResolvedMoves[i];
            Require(t.Id==b.Trainer&&t.Id.Value=="pret:"+r.GetProperty("trainer").GetString()&&b.ScriptEntry.StartsWith("Route8_EventScript_",StringComparison.Ordinal)&&
                !t.Party.IsDefaultOrEmpty&&t.Party.Length<=6&&t.PartyFlags is 0 or 1&&!t.DoubleBattle&&t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE",4))&&
                t.AiFlags==1&&t.AiFlagSymbols.SequenceEqual(["AI_SCRIPT_CHECK_BAD_MOVE"])&&b.Mode==0&&b.RivalFlags==0&&b.BattleTypeFlags==8&&b.VictoryText==""&&
                b.EntryScript=="EventScript_TryDoNormalTrainerBattle"&&b.Callback=="CB2_EndTrainerBattle"&&b.PostBattleScript is null&&b.MusicOption is null&&r.GetProperty("party").GetArrayLength()==t.Party.Length,"Celadon original trainer contract differs.");
            for(int j=0;j<t.Party.Length;j++)
            {
                var p=t.Party[j];var resolved=r.GetProperty("party")[j];var moves=resolved.GetProperty("moves").EnumerateArray().Select(x=>x.GetString()!).ToArray();
                Require(p.HeldItem=="ITEM_NONE"&&p.Level is >=1 and <=100&&p.SourceIv is >=0 and <=255&&!p.CustomMoves.IsDefault&&p.Species.Value=="pret:"+resolved.GetProperty("species").GetString()&&
                    p.Level==resolved.GetProperty("level").GetInt32()&&moves.Length is >=1 and <=4&&
                    (t.PartyFlags==0?p.CustomMoves.Length==0:p.CustomMoves.Length==4&&p.CustomMoves.Select(x=>x.Value).SequenceEqual(moves.Select(x=>"pret:"+x))),"Celadon trainer source member differs.");
            }
        }
        var doubles=d.DeferredDoubleBattles;
        Require(doubles.GetArrayLength()==1&&doubles[0].GetProperty("trainer").GetString()=="TRAINER_TWINS_ELI_ANNE"&&!doubles[0].GetProperty("executable").GetBoolean(),"Original deferred double contract differs.");
        return d;
    }
    public static CeladonApproachTrainerCatalog Load(string root)
    {var d=LoadDocument(root);return new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(t=>t.Trainer));}
}
