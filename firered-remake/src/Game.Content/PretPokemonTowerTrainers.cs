using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record PokemonTowerTrainerDocument(int SchemaVersion,string Profile,string SourceCommit,bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves,ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames,
    ImmutableArray<string> Limitations);
/// <summary>Exact source teams and move metadata; no campaign activation or battle execution.</summary>
public static class PretPokemonTowerTrainers
{
    public const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile="firered-pokemon-tower-trainers-v1";
    public const int TrainerCount=19,PartyMemberCount=40,RequiredMoveCount=43;
    public static PokemonTowerTrainerDocument LoadDocument(string root, string? dependencyProjectRoot = null)
    {
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/pokemon-tower-trainers/trainers.json");
        var d=ContentJson.ParseStrict<PokemonTowerTrainerDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        PretCeladonApproachSource.Require(d.SchemaVersion==1&&!d.CompleteSourceRoster&&d.Trainers.Length==TrainerCount&&d.Battles.Length==TrainerCount&&
            d.Trainers.Select(x=>x.Id).Distinct().Count()==TrainerCount&&d.Trainers.Sum(x=>x.Party.Length)==PartyMemberCount&&d.ResolvedMoves.GetArrayLength()==TrainerCount&&d.Moves.Length==RequiredMoveCount&&d.MoveNames.Length==RequiredMoveCount&&!d.Limitations.IsDefaultOrEmpty,"Pokemon Tower document scope differs.");
        PokemonTowerTrainerPartyFactory.ValidateCatalog(new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer)));
        for(int i=0;i<TrainerCount;i++)
        {
            var t=d.Trainers[i];var resolved=d.ResolvedMoves[i];
            PretCeladonApproachSource.Require(resolved.GetProperty("trainer").GetString()==t.Id.Value[5..]&&resolved.GetProperty("party").GetArrayLength()==t.Party.Length,"Pokemon Tower resolved party identity differs.");
            for(int j=0;j<t.Party.Length;j++)
            {
                var p=t.Party[j];var r=resolved.GetProperty("party")[j];var moves=r.GetProperty("moves").EnumerateArray().Select(x=>"pret:"+x.GetString()).ToArray();
                PretCeladonApproachSource.Require(p.Species.Value=="pret:"+r.GetProperty("species").GetString()&&p.Level==r.GetProperty("level").GetInt32()&&p.HeldItem=="ITEM_NONE"&&
                    (t.PartyFlags==0?p.CustomMoves.Length==0:p.CustomMoves.Select(x=>x.Value).SequenceEqual(moves)),"Pokemon Tower source party member differs.");
            }
        }
        foreach(var m in d.Moves)
        {
            PretCeladonApproachSource.Require(m.Id.Value!="pret:MOVE_NONE"&&!m.BattleEffectImplemented,"Pokemon Tower move scope differs.");
        }
        return d;
    }
    public static PokemonTowerTrainerCatalog Load(string root, string? dependencyProjectRoot = null)
    {var d=LoadDocument(root, dependencyProjectRoot);return new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
}
