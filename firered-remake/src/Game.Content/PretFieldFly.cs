using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Reads the compact authenticated Fly tables and HM02 rows, reusing existing private sources and TM40 art.</summary>
public static class PretFieldFly
{
    public const string ContractSha256 = "8c348ffd506fa2680a99b538bf6cd2b09536508e527009cd8ebbb5a36603b565";
    public const string FieldSha256 = "7757ace5772c86f2d486a4e511d140a35fe1a5b086a839d7a28dfc7a38a4213f";
    public const string Hm02Sha256 = "3c5f91a9fd351f8e8881bcbebae1de1a6b4368f6ce39ea97a8c3afa0cc6b3968";
    public static ItemId ReusedHm02Icon { get; } = new("pret:ITEM_TM40");
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = ["field-fly-contract:" + ContractSha256,
        "field-fly-tables:" + FieldSha256, "field-fly-hm02:" + Hm02Sha256];
    private static byte[] Read(string root, string path, string hash, int maximum = 1048576)
    {
        if (Path.IsPathRooted(path) || path.Split('/').Any(part => part is "" or "." or "..")) throw new InvalidDataException("field_fly_path");
        using var stream = File.OpenRead(Path.Combine(root, path));
        if (stream.Length > maximum) throw new InvalidDataException("field_fly_file_size");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("field_fly_source_bytes:" + path);
        return bytes;
    }
    public static FieldFlySourceDocument LoadDocument(string root) => ContentJson.ParseStrict<FieldFlySourceDocument>(
        Encoding.UTF8.GetString(Read(root, "content/generated/field-fly/field.json", FieldSha256)), ContentJson.CreateOptions());
    public static PewterAdditionalMachine LoadHm02(string root) => ContentJson.ParseStrict<PewterAdditionalMachine>(
        Encoding.UTF8.GetString(Read(root, "content/generated/field-fly/hm02.json", Hm02Sha256)), ContentJson.CreateOptions());
    public static FieldFlyCatalog Load(string root) => FieldFlyCatalog.Authenticate(LoadDocument(root));
    public static FieldFlyMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor) =>
        FieldFlyMachineCatalog.Authenticate(predecessor, LoadHm02(root));
    public static void ValidateSourceEvidence(string root, string? sharedRoot = null)
    {
        using var doc = JsonDocument.Parse(Read(root, "scripts/field-fly.contract.json", ContractSha256));
        var contract = doc.RootElement;
        if (contract.GetProperty("sourceCommit").GetString() != FieldFlySourceRules.SourceCommit ||
            contract.GetProperty("sourceTree").GetString() != "7515cd4067ea55f8cdbc73624478afd2736c58b9" ||
            contract.GetProperty("upstreamScriptsExecuted").GetBoolean() || contract.GetProperty("romDownloaded").GetBoolean())
            throw new InvalidDataException("field_fly_source_contract");
        foreach (var input in contract.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read(input.GetProperty("shared").GetBoolean() ? sharedRoot ?? root : root,
                input.GetProperty("localPath").GetString()!, input.GetProperty("sha256").GetString()!, maximum: 2097152);
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("field_fly_git_blob");
        }
        foreach (var input in contract.GetProperty("dependencies").EnumerateArray())
            _ = Read(sharedRoot ?? root, input.GetProperty("path").GetString()!, input.GetProperty("sha256").GetString()!);
        _ = Load(root);
        if (ContentJson.SourceRecordSha256(LoadHm02(root)) != FieldFlyMachineCatalog.Hm02SourceSha256)
            throw new InvalidDataException("field_fly_hm02_source");
    }
}
