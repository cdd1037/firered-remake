using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record RocketHideoutTrainerDocument(int SchemaVersion,string Profile,string SourceCommit,bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves,ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames,
    ImmutableArray<string> Limitations);
/// <summary>Exact source teams and move metadata; no campaign activation or battle execution.</summary>
public static class PretRocketHideoutTrainers
{
    public const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile="firered-rocket-hideout-trainers-v1";
    public const string LockSha256="87fdae0d15a5e7d3ebacc4e824dd77f102d399ca62277f69730c4d5febc8a7c5";
    public const string GeneratedSha256="ce6535b428a86e95ab5ecd9a6fcdf2f3a3dc66a54edff0e440922e1694616548";
    public const int SourceFileCount=73,TrainerCount=13,PartyMemberCount=36,RequiredMoveCount=38;
    public static RocketHideoutTrainerDocument LoadDocument(string root)
    {
        var source=PretCeladonApproachSource.Load(root,"rocket-hideout-trainers",Profile,LockSha256,SourceFileCount);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/rocket-hideout-trainers/trainers.json",GeneratedSha256);
        var d=ContentJson.ParseStrict<RocketHideoutTrainerDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r)=>source.Reference(r.Path,r.Sha256,r.StartLine,r.EndLine,r.Symbol);
        PretCeladonApproachSource.Require(d.SchemaVersion==1&&d.Profile==Profile&&d.SourceCommit==SourceCommit&&!d.CompleteSourceRoster&&d.Trainers.Length==TrainerCount&&d.Battles.Length==TrainerCount&&
            d.Trainers.Select(x=>x.Id).Distinct().Count()==TrainerCount&&d.Trainers.Sum(x=>x.Party.Length)==PartyMemberCount&&d.ResolvedMoves.GetArrayLength()==TrainerCount&&d.Moves.Length==RequiredMoveCount&&d.MoveNames.Length==RequiredMoveCount&&!d.Limitations.IsDefaultOrEmpty,"Rocket Hideout document scope differs.");
        RocketHideoutTrainerPartyFactory.ValidateCatalog(new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer)));
        for(int i=0;i<TrainerCount;i++)
        {
            var t=d.Trainers[i];var b=d.Battles[i];Source(t.Source);Source(t.MoneySource);Source(b.Source);var resolved=d.ResolvedMoves[i];
            PretCeladonApproachSource.Require(resolved.GetProperty("trainer").GetString()==t.Id.Value[5..]&&resolved.GetProperty("party").GetArrayLength()==t.Party.Length,"Rocket Hideout resolved party identity differs.");
            for(int j=0;j<t.Party.Length;j++)
            {
                var p=t.Party[j];var r=resolved.GetProperty("party")[j];var moves=r.GetProperty("moves").EnumerateArray().Select(x=>"pret:"+x.GetString()).ToArray();
                PretCeladonApproachSource.Require(p.Species.Value=="pret:"+r.GetProperty("species").GetString()&&p.Level==r.GetProperty("level").GetInt32()&&p.HeldItem=="ITEM_NONE"&&
                    (t.PartyFlags==0?p.CustomMoves.Length==0:p.CustomMoves.Select(x=>x.Value).SequenceEqual(moves)),"Rocket Hideout source party member differs.");
                Source(p.Source);Source(p.SpeciesNameSource);Source(ContentJson.ParseStrict<TrainerSourceRef>(r.GetProperty("learnsetSource").GetRawText(),ContentJson.CreateOptions()));
            }
        }
        foreach(var m in d.Moves)
        {
            PretCeladonApproachSource.Require(m.Id.Value!="pret:MOVE_NONE"&&!m.BattleEffectImplemented,"Rocket Hideout move scope differs.");
            Source(new(m.Source.Path,m.Source.Symbol,m.Source.StartLine,m.Source.EndLine,m.Source.Sha256));
        }
        foreach(var m in d.MoveNames)Source(new(m.Source.Path,m.Source.Symbol,m.Source.StartLine,m.Source.EndLine,m.Source.Sha256));
        return d;
    }
    public static RocketHideoutTrainerCatalog Load(string root)
    {var d=LoadDocument(root);return new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
}
