using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed class PretMoveTutors
{
    public const string LockSha256 = "a6e30cf09cd5582613dc8e5e8cd0e915aee4980d0d52fbdfd63a9de4d355542b";
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
        using var document = JsonDocument.Parse(Read(Path.Combine(root, "scripts/move-tutors.lock.json"), 3805, LockSha256));
        var doc = document.RootElement;
        if (doc.GetProperty("schemaVersion").GetInt32() != 1 || doc.GetProperty("commit").GetString() != PokemonFactory.SourceCommit ||
            doc.GetProperty("files").GetArrayLength() != 16) throw new InvalidDataException("move_tutor_source_scope");
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
