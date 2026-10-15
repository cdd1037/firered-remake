using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed class PretMoveTutors
{
    public MoveTutorCatalog Catalog { get; }
    private PretMoveTutors(MoveTutorCatalog catalog)
    { Catalog = catalog; }

    public static PretMoveTutors Load(string root, PokemonCatalog owned)
    {
        static byte[] Read(string path)
        {
var bytes = File.ReadAllBytes(path);
return bytes;
        }
        using var document = JsonDocument.Parse(Read(Path.Combine(root, "scripts/move-tutors.lock.json")));
        var doc = document.RootElement;
        if (doc.GetProperty("schemaVersion").GetInt32() != 1 ) throw new InvalidDataException("move_tutor_source_scope");
var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in doc.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            if (Path.IsPathRooted(path) || path.Split('/').Any(p => p is "" or "." or "..") || files.ContainsKey(path))
                throw new InvalidDataException("move_tutor_source_path");
            files.Add(path, Read(Path.Combine(root, PretSourcePaths.FireRedRawDirectory, path)));
        }
        return new(MoveTutorCatalog.Authenticate(owned, files["src/data/pokemon/tutor_learnsets.h"], files["src/strings.c"], files["include/constants/moves.h"]));
    }
}
