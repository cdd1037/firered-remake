using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class SourceVerifier
{
    public const string Commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string LockHash = "9b7db84cfe1c823b04ee41908ef1f838c7d71d9d1f40d6a59c24f1871eb1d893";
    public const string TreeHash = "998020ccba7c9c6e49b0ac4b7e127d15bceb39a9040d4329a007873b6f750f31";
    public const string FixtureHash = "bf76afc0f86134f2d1b7566e28544794a0dea451dfad8991f7979dbf28ada5f6";
    public static readonly string[] Paths = ["src/text_printer.c", "src/text.c", "src/new_menu_helpers.c", "include/text.h"];
    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static Dictionary<string, string> Read(string reference, byte[] lockBytes, byte[] treeBytes)
    {
        if (Sha256(lockBytes) != LockHash || Sha256(treeBytes) != TreeHash)
            throw new InvalidDataException("Source lock or independently retained pinned Git tree hash differs.");
        using var locked = JsonDocument.Parse(lockBytes);
        using var tree = JsonDocument.Parse(treeBytes);
        if (tree.RootElement.GetProperty("sha").GetString() != Commit
            || locked.RootElement.GetProperty("sourceCommit").GetString() != Commit
            || locked.RootElement.GetProperty("treeSha256").GetString() != TreeHash)
            throw new InvalidDataException("Source revision differs.");
        var blobs = tree.RootElement.GetProperty("tree").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "blob")
            .ToDictionary(e => e.GetProperty("path").GetString()!, e => e.GetProperty("sha").GetString()!);
        var files = locked.RootElement.GetProperty("files").EnumerateArray().ToArray();
        if (files.Length != Paths.Length || !files.Select(f => f.GetProperty("path").GetString()).SequenceEqual(Paths))
            throw new InvalidDataException("Source inventory is incomplete.");
        var result = new Dictionary<string, string>();
        foreach (var file in files)
        {
            string relative = file.GetProperty("path").GetString()!;
            string path = Path.Combine(reference, relative);
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Missing or nonregular source: " + relative);
            var data = File.ReadAllBytes(path);
            var blobBytes = Encoding.UTF8.GetBytes("blob " + data.Length + "\0").Concat(data).ToArray();
            string blob = Convert.ToHexStringLower(SHA1.HashData(blobBytes));
            if (data.Length != file.GetProperty("byteLength").GetInt32()
                || Sha256(data) != file.GetProperty("sha256").GetString()
                || blob != file.GetProperty("gitBlobSha1").GetString() || blob != blobs[relative])
                throw new InvalidDataException("Actual source SHA-256 / pinned Git blob differs: " + relative);
            result.Add(relative, Encoding.UTF8.GetString(data));
        }
        return result;
    }
}
