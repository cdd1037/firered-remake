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
    public const string LockSha256="6e704adaec0bdadc469da32fd7a2fbbf57262dd2a4c9109c38d45271ffd26d9d";
    public const string GeneratedSha256="7f850446fc25c4f424870f89879dd13aa84b5642f8010504dc3cdea1e99bc247";
    public const int SourceFileCount=33,TrainerCount=19,PartyMemberCount=40,RequiredMoveCount=43;
    public static PokemonTowerTrainerDocument LoadDocument(string root, string? dependencyProjectRoot = null)
    {
        var source=ReadSources(root, dependencyProjectRoot ?? root);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/pokemon-tower-trainers/trainers.json",GeneratedSha256);
        var d=ContentJson.ParseStrict<PokemonTowerTrainerDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r)=>PretCeladonApproachSource.Require(source.TryGetValue(r.Path, out var entry) && entry.Hash == r.Sha256 && r.StartLine > 0 && r.EndLine >= r.StartLine && r.EndLine <= entry.Lines && !string.IsNullOrWhiteSpace(r.Symbol), "Tower trainer source reference differs.");
        PretCeladonApproachSource.Require(d.SchemaVersion==1&&d.Profile==Profile&&d.SourceCommit==SourceCommit&&!d.CompleteSourceRoster&&d.Trainers.Length==TrainerCount&&d.Battles.Length==TrainerCount&&
            d.Trainers.Select(x=>x.Id).Distinct().Count()==TrainerCount&&d.Trainers.Sum(x=>x.Party.Length)==PartyMemberCount&&d.ResolvedMoves.GetArrayLength()==TrainerCount&&d.Moves.Length==RequiredMoveCount&&d.MoveNames.Length==RequiredMoveCount&&!d.Limitations.IsDefaultOrEmpty,"Pokemon Tower document scope differs.");
        PokemonTowerTrainerPartyFactory.ValidateCatalog(new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer)));
        for(int i=0;i<TrainerCount;i++)
        {
            var t=d.Trainers[i];var b=d.Battles[i];Source(t.Source);Source(t.MoneySource);Source(b.Source);var resolved=d.ResolvedMoves[i];
            PretCeladonApproachSource.Require(resolved.GetProperty("trainer").GetString()==t.Id.Value[5..]&&resolved.GetProperty("party").GetArrayLength()==t.Party.Length,"Pokemon Tower resolved party identity differs.");
            for(int j=0;j<t.Party.Length;j++)
            {
                var p=t.Party[j];var r=resolved.GetProperty("party")[j];var moves=r.GetProperty("moves").EnumerateArray().Select(x=>"pret:"+x.GetString()).ToArray();
                PretCeladonApproachSource.Require(p.Species.Value=="pret:"+r.GetProperty("species").GetString()&&p.Level==r.GetProperty("level").GetInt32()&&p.HeldItem=="ITEM_NONE"&&
                    (t.PartyFlags==0?p.CustomMoves.Length==0:p.CustomMoves.Select(x=>x.Value).SequenceEqual(moves)),"Pokemon Tower source party member differs.");
                Source(p.Source);Source(p.SpeciesNameSource);Source(ContentJson.ParseStrict<TrainerSourceRef>(r.GetProperty("learnsetSource").GetRawText(),ContentJson.CreateOptions()));
            }
        }
        foreach(var m in d.Moves)
        {
            PretCeladonApproachSource.Require(m.Id.Value!="pret:MOVE_NONE"&&!m.BattleEffectImplemented,"Pokemon Tower move scope differs.");
            Source(new(m.Source.Path,m.Source.Symbol,m.Source.StartLine,m.Source.EndLine,m.Source.Sha256));
        }
        foreach(var m in d.MoveNames)Source(new(m.Source.Path,m.Source.Symbol,m.Source.StartLine,m.Source.EndLine,m.Source.Sha256));
        return d;
    }
    public static PokemonTowerTrainerCatalog Load(string root, string? dependencyProjectRoot = null)
    {var d=LoadDocument(root, dependencyProjectRoot);return new(d.SourceCommit,d.Profile,false,d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
    private static ImmutableDictionary<string, (string Hash, int Lines)> ReadSources(string root, string dependencyRoot)
    {
        using var doc = JsonDocument.Parse(PretCeladonApproachSource.Read(root, "scripts/pokemon-tower-trainers.lock.json", LockSha256));
        var d = doc.RootElement;
        PretCeladonApproachSource.Require(d.GetProperty("schemaVersion").GetInt32() == 1 && d.GetProperty("repository").GetString() == "pret/pokefirered" && d.GetProperty("commit").GetString() == SourceCommit && d.GetProperty("tree").GetString() == PretCeladonApproachSource.Tree && d.GetProperty("profile").GetString() == Profile && d.GetProperty("gameEdition").GetString() == "FIRERED" && d.GetProperty("romRevision").GetInt32() == 0 && d.GetProperty("files").GetArrayLength() == SourceFileCount, "Tower trainer source identity differs.");
        var result = ImmutableDictionary.CreateBuilder<string, (string Hash, int Lines)>(StringComparer.Ordinal);
        foreach (var f in d.GetProperty("files").EnumerateArray())
        {
            string path = f.GetProperty("path").GetString()!, hash = f.GetProperty("sha256").GetString()!;
            byte[] bytes = PretCeladonApproachSource.Read(dependencyRoot, f.GetProperty("storagePath").GetString()!, hash, f.GetProperty("byteLength").GetInt32());
            PretCeladonApproachSource.Require(Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == f.GetProperty("gitBlobSha1").GetString(), "Tower trainer Git blob differs.");
            result.Add(path, (hash, Encoding.UTF8.GetString(bytes).Count(c => c == '\n') + 1));
        }
        return result.ToImmutable();
    }
}
