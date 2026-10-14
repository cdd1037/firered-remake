using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed class PretMoveTutors
{
    public const string LockSha256 = "a6f930517515b20ae3b0019152c041edf65d3f3755f8f5b8b902d3ad26e0b3d3";
    public MoveTutorCatalog Catalog { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretMoveTutors(MoveTutorCatalog catalog, ImmutableArray<string> hashes)
    { Catalog = catalog; AdditionalSourceHashes = hashes; }

    public static PretMoveTutors Load(string root, PokemonCatalog owned)
    {
        static byte[] Read(string path, int count, string hash)
        {
            if (count is < 0 or > 1024 * 1024) throw new InvalidDataException("move_tutor_source_limit");
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length != count) throw new InvalidDataException("move_tutor_source_size");
            var bytes = new byte[count]; input.ReadExactly(bytes);
            if (input.Length != count || input.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("move_tutor_source_hash");
            return bytes;
        }
        using var document = JsonDocument.Parse(Read(Path.Combine(root, "scripts/move-tutors.lock.json"), 5898, LockSha256));
        var doc = document.RootElement;
        if (doc.GetProperty("schemaVersion").GetInt32() != 1 || doc.GetProperty("commit").GetString() != PokemonFactory.SourceCommit ||
            doc.GetProperty("files").GetArrayLength() != 25) throw new InvalidDataException("move_tutor_source_scope");
        var hashes = ImmutableArray.CreateBuilder<string>(); hashes.Add("move-tutors-lock:" + LockSha256);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in doc.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!, hash = row.GetProperty("sha256").GetString()!;
            if (Path.IsPathRooted(path) || path.Split('/').Any(p => p is "" or "." or "..") || files.ContainsKey(path))
                throw new InvalidDataException("move_tutor_source_path");
            files.Add(path, Read(Path.Combine(root, "content/raw/move-tutors", path), row.GetProperty("bytes").GetInt32(), hash));
            hashes.Add("move-tutors-source:" + path + ":" + hash);
        }
        return new(MoveTutorCatalog.Authenticate(owned, files["src/data/pokemon/tutor_learnsets.h"], files["src/strings.c"], files["include/constants/moves.h"]), hashes.ToImmutable());
    }
}
