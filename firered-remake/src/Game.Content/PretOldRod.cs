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
    public const string LockSha256 = "564703fe1d398879cec0e55fd79dd26acfe88671f58ee564df2fefcdd3fa4f99";
    public const string GeneratedSha256 = "6fe7713934632324e5e6706f37bf3d91c8796e3f3d082d0f374a7f4911cbf447";
    public ImmutableDictionary<MapId, OldRodEncounterTable> Tables { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    private PretOldRod(ImmutableDictionary<MapId, OldRodEncounterTable> tables, ImmutableArray<string> hashes, ImmutableDictionary<string, string> texts)
    { Tables = tables; AdditionalSourceHashes = hashes; Texts = texts; }

    public static PretOldRod Load(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        byte[] Read(string path, string hash, int? length = null)
        {
            var b = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), length ?? 2 * 1024 * 1024);
            if (Hash(b) != hash || length is {} n && b.Length != n) throw new InvalidDataException("Old Rod pinned bytes differ: " + path);
            return b;
        }
        using var sourceLock = JsonDocument.Parse(Read("content/sources/old-rod.lock.json", LockSha256));
        var s = sourceLock.RootElement;
        if (s.GetProperty("schemaVersion").GetInt32() != 1 || s.GetProperty("commit").GetString() != OldRodFishingRules.SourceCommit ||
            s.GetProperty("tree").GetString() != "7515cd4067ea55f8cdbc73624478afd2736c58b9" || s.GetProperty("profile").GetString() != Profile ||
            s.GetProperty("repository").GetString() != "pret/pokefirered" || s.GetProperty("files").GetArrayLength() != 14)
            throw new InvalidDataException("Old Rod source identity differs.");
        var hashes = ImmutableArray.CreateBuilder<string>();
        hashes.Add("old-rod-lock:" + LockSha256); hashes.Add("old-rod:" + GeneratedSha256);
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var f in s.GetProperty("files").EnumerateArray())
        {
            string p = f.GetProperty("path").GetString()!, hash = f.GetProperty("sha256").GetString()!;
            var b = Read("content/raw/old-rod/" + p, hash, f.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"), ..b])) != f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Old Rod Git blob differs.");
            hashes.Add("old-rod:" + p + ":" + hash);
            if (p == "src/strings.c")
                foreach (string symbol in new[] { "gText_PokemonOnHook", "gText_NotEvenANibble" })
                {
                    var match = Regex.Match(Encoding.UTF8.GetString(b), "^const u8 " + symbol + @"\[\] = _\(""([^""]*)""\);$", RegexOptions.Multiline);
                    if (!match.Success || !match.Groups[1].Value.EndsWith("{PAUSE_UNTIL_PRESS}", StringComparison.Ordinal))
                        throw new InvalidDataException("Old Rod result text must preserve source pause-for-button control.");
                    texts.Add(symbol, match.Groups[1].Value);
                }
        }
        var document = ContentJson.ParseStrict<OldRodDocument>(Encoding.UTF8.GetString(Read("content/generated/old-rod/old-rod.json", GeneratedSha256)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.Profile != Profile || document.SourceCommit != OldRodFishingRules.SourceCommit ||
            document.Tables.IsDefault || document.Tables.Length != 10 || document.CastDelayFrames != 60 || document.DotDelayFrames != 20 ||
            document.FirstRoundMinimumDots != 4 || document.FirstRoundMaximumDots != 10 || document.BiteDrawBound != 2 || document.BiteSuccessRoll != 0 ||
            document.ReelReactionReachable || document.InFlightCancelSupported || document.MinimumRoundDrawBound != 1 ||
            document.HeldItemDrawBound != 100 || document.HeldItem != "ITEM_NONE" || document.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("Old Rod source scope differs.");
        foreach (var table in document.Tables) OldRodEncounterRules.ValidateTable(table);
        return new(document.Tables.ToImmutableDictionary(t => t.Map), hashes.ToImmutable(), texts.ToImmutable());
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
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
