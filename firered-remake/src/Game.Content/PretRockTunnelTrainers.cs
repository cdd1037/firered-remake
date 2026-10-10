using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record RockTunnelTrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<string> Limitations);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretRockTunnelTrainers
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile = "firered-rocktunnel-trainers-v1";
    public const int SourceFileCount = 29;
    public const int TrainerCount = 30;
    public const int PartyMemberCount = 72;
    public const string LockSha256 = "369db76f2a3e9833547f86345dfe78764a155feb434770744034efd2b59224b2";
    public const string GeneratedSha256 = "585e9b998bca091752cdcb8011f0158d157fcfe6b382bc041428a159b2c31a9d";
    public static RockTunnelTrainerCatalog Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("RockTunnel trainer pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/rocktunnel-trainers.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=SourceCommit || s.GetProperty("profile").GetString()!=Profile ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("tree").GetString()!="7515cd4067ea55f8cdbc73624478afd2736c58b9" ||
            s.GetProperty("gameEdition").GetString()!="FIRERED" || s.GetProperty("romRevision").GetInt32()!=0 || s.GetProperty("schemaVersion").GetInt32()!=1 || s.GetProperty("files").GetArrayLength()!=SourceFileCount) throw new InvalidDataException("RockTunnel trainer source identity differs.");
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        var lineCounts=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/rocktunnel-trainers/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(!files.TryAdd(p,hash) || Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("RockTunnel trainer Git blob differs.");
            lineCounts.Add(p,Encoding.UTF8.GetString(b).Count(c=>c=='\n')+1);
        }
        var doc=ContentJson.ParseStrict<RockTunnelTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/rocktunnel-trainers/trainers.json",GeneratedSha256)),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r) { if(!files.TryGetValue(r.Path,out var h)||h!=r.Sha256||r.StartLine<=0||r.EndLine<r.StartLine||r.EndLine>lineCounts[r.Path]||string.IsNullOrWhiteSpace(r.Symbol)) throw new InvalidDataException("RockTunnel trainer source reference differs."); }
        if(doc.SchemaVersion!=1 || doc.Profile!=Profile || doc.SourceCommit!=SourceCommit || doc.CompleteSourceRoster) throw new InvalidDataException("RockTunnel trainer document identity differs.");
        if (doc.Trainers.IsDefault || doc.Battles.IsDefault || doc.Limitations.IsDefaultOrEmpty ||
            doc.Trainers.Length != TrainerCount || doc.Battles.Length != TrainerCount ||
            doc.Trainers.Select(t => t.Id).Distinct().Count() != TrainerCount ||
            doc.Battles.Select(b => b.Trainer).Distinct().Count() != TrainerCount ||
            doc.Trainers.Sum(t => t.Party.Length) != PartyMemberCount ||
            doc.ResolvedMoves.ValueKind != JsonValueKind.Array || doc.ResolvedMoves.GetArrayLength() != TrainerCount)
            throw new InvalidDataException("RockTunnel trainer document roster differs.");
        for (int i = 0; i < TrainerCount; i++)
        {
            var t = doc.Trainers[i]; var b = doc.Battles[i]; var r = doc.ResolvedMoves[i];
            string map = i < 9 ? "Route9" : i < 15 ? "Route10" : i < 22 ? "RockTunnel_1F" : "RockTunnel_B1F";
            if (t.Id != b.Trainer || t.Id.Value != "pret:" + r.GetProperty("trainer").GetString() ||
                !b.ScriptEntry.StartsWith(map + "_EventScript_", StringComparison.Ordinal) ||
                t.Party.IsDefaultOrEmpty || t.Party.Length > 6 || t.PartyFlags is not (0 or 1) || t.DoubleBattle ||
                t.Items.IsDefault || !t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE", 4)) ||
                t.AiFlags != 1 || t.AiFlagSymbols.IsDefault || !t.AiFlagSymbols.SequenceEqual(["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                b.Mode != 0 || b.RivalFlags != 0 || b.BattleTypeFlags != 8 || b.VictoryText != "" ||
                b.EntryScript != "EventScript_TryDoNormalTrainerBattle" || b.Callback != "CB2_EndTrainerBattle" ||
                b.PostBattleScript is not null || b.MusicOption is not null || r.GetProperty("party").GetArrayLength() != t.Party.Length)
                throw new InvalidDataException("RockTunnel trainer party or initial battle differs.");
            for (int j = 0; j < t.Party.Length; j++)
            {
                var p = t.Party[j]; var resolved = r.GetProperty("party")[j];
                var moves = resolved.GetProperty("moves").EnumerateArray().Select(x => x.GetString()!).ToArray();
                if (p.HeldItem != "ITEM_NONE" || p.Level is < 1 or > 100 || p.SourceIv is < 0 or > 255 ||
                    p.CustomMoves.IsDefault || p.Species.Value != "pret:" + resolved.GetProperty("species").GetString() ||
                    p.Level != resolved.GetProperty("level").GetInt32() || moves.Length is < 1 or > 4 ||
                    (t.PartyFlags == 0 ? p.CustomMoves.Length != 0 :
                        p.CustomMoves.Length != 4 || !p.CustomMoves.Select(x => x.Value).SequenceEqual(moves.Select(x => "pret:" + x))))
                    throw new InvalidDataException("RockTunnel source party moves differ.");
            }
        }
        foreach(var t in doc.Trainers) { Source(t.Source);Source(t.MoneySource);foreach(var p in t.Party){Source(p.Source);Source(p.SpeciesNameSource);} }
        foreach(var b in doc.Battles) Source(b.Source);
        foreach(var trainer in doc.ResolvedMoves.EnumerateArray())
            foreach(var member in trainer.GetProperty("party").EnumerateArray())
                Source(ContentJson.ParseStrict<TrainerSourceRef>(member.GetProperty("learnsetSource").GetRawText(),ContentJson.CreateOptions()));
        var catalog=new RockTunnelTrainerCatalog(doc.SourceCommit,doc.Profile,false,doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        return catalog;
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe RockTunnel trainer path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("RockTunnel trainer symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("RockTunnel trainer parent symlink rejected.");
        return f.FullName;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
