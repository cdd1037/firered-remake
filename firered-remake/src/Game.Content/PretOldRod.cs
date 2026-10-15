using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record OldRodDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<OldRodEncounterTable> Tables, int CastDelayFrames, int DotDelayFrames,
    int FirstRoundMinimumDots, int FirstRoundMaximumDots, int BiteDrawBound, int BiteSuccessRoll,
    bool ReelReactionReachable, bool InFlightCancelSupported, int MinimumRoundDrawBound,
    int HeldItemDrawBound, string HeldItem, ImmutableArray<string> Limitations);

/// <summary>Authenticates both source evidence and the exact bounded Old Rod output.</summary>
public sealed class PretOldRod
{
    public const string Profile = OldRodFishingRules.RulesVersion;
    public ImmutableDictionary<MapId, OldRodEncounterTable> Tables { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    private PretOldRod(ImmutableDictionary<MapId, OldRodEncounterTable> tables,ImmutableDictionary<string, string> texts)
    { Tables = tables; Texts = texts; }

    public static PretOldRod Load(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        using var sourceLock = JsonDocument.Parse(Read("content/sources/old-rod.lock.json"));
        var s = sourceLock.RootElement;
        if (s.GetProperty("schemaVersion").GetInt32() != 1 )
            throw new InvalidDataException("Old Rod source identity differs.");
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var f in s.GetProperty("files").EnumerateArray())
        {
            string p = f.GetProperty("path").GetString()!;
                    var b = Read(PretSourcePaths.FireRedRawDirectory + "/" + p);
            if (p == "src/strings.c")
                        foreach (string symbol in new[] { "gText_PokemonOnHook","gText_NotEvenANibble" })
                {
                    var match = Regex.Match(Encoding.UTF8.GetString(b), "^const u8 " + symbol + @"\[\] = _\(""([^""]*)""\);$", RegexOptions.Multiline);
                    if (!match.Success || !match.Groups[1].Value.EndsWith("{PAUSE_UNTIL_PRESS}", StringComparison.Ordinal))
                        throw new InvalidDataException("Old Rod result text must preserve source pause-for-button control.");
                    texts.Add(symbol, match.Groups[1].Value);
                }
        }
                var document = ContentJson.ParseStrict<OldRodDocument>(Encoding.UTF8.GetString(Read("content/generated/old-rod/old-rod.json")), ContentJson.CreateOptions());
                if (document.SchemaVersion != 1 ||
            document.Tables.IsDefault || document.Tables.Length != 10 || document.CastDelayFrames != 60 || document.DotDelayFrames != 20 ||
            document.FirstRoundMinimumDots != 4 || document.FirstRoundMaximumDots != 10 || document.BiteDrawBound != 2 || document.BiteSuccessRoll != 0 ||
            document.ReelReactionReachable || document.InFlightCancelSupported || document.MinimumRoundDrawBound != 1 ||
            document.HeldItemDrawBound != 100 || document.HeldItem != "ITEM_NONE" || document.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("Old Rod source scope differs.");
        foreach (var table in document.Tables) OldRodEncounterRules.ValidateTable(table);
                return new(document.Tables.ToImmutableDictionary(t => t.Map),texts.ToImmutable());
    }

    private static string SafeFile(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Old Rod path.");
        var file = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
        if (file.LinkTarget is not null) throw new InvalidDataException("Old Rod symlink rejected.");
        for (var d = file.Directory; d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Old Rod parent symlink rejected.");
        return file.FullName;
    }
}
