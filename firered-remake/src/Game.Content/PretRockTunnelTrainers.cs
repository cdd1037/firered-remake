using System.Collections.Immutable;
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
    public const int TrainerCount = 30;
    public const int PartyMemberCount = 72;
    public static RockTunnelTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var doc=ContentJson.ParseStrict<RockTunnelTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/rocktunnel-trainers/trainers.json")),ContentJson.CreateOptions());
                if(doc.SchemaVersion!=1 || doc.CompleteSourceRoster) throw new InvalidDataException("RockTunnel trainer document identity differs.");
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
}
