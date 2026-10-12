using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

/// <summary>The five original Silph trainer portrait identities, including the later rival portrait.</summary>
public static class PretSilphTrainerArt
{
    public const string ManifestSha256 = "f0e491c0546063a649b37f56ea11d813b5bc7b54b5f533540370779fe916a3e4";
    public const string GeneratedDirectory = "content/generated/silph-trainer-art/";
    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        _ = PretSilphPresentationSource.Load(root);
        var bytes = SafariSource.Read(root, GeneratedDirectory + "manifest.json", ManifestSha256, 27834);
        using var json = JsonDocument.Parse(bytes); var doc = json.RootElement;
        SafariSource.Require(doc.GetProperty("schemaVersion").GetInt32() == 1 && doc.GetProperty("profile").GetString() == "firered-silph-trainer-art-v1" &&
            doc.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && doc.GetProperty("lockSha256").GetString() == PretSilphPresentationSource.LockSha256 &&
            doc.GetProperty("inputs").GetArrayLength() == PretSilphPresentationSource.SourceCount && doc.GetProperty("portraits").GetArrayLength() == 5 &&
            doc.GetProperty("outputs").GetArrayLength() == 10, "Silph portrait manifest scope differs.");
        var files = PretSilphPresentationSource.Outputs(root, GeneratedDirectory,
            SafariSource.Parse<ImmutableArray<SnorlaxArtOutput>>(System.Text.Encoding.UTF8.GetBytes(doc.GetProperty("outputs").GetRawText())));
        var identities = new Dictionary<string, (int Id, string Symbol)>(StringComparer.Ordinal)
        {
            ["rocket-grunt-m-front"] = (109, "ROCKET_GRUNT_M"), ["rocket-grunt-f-front"] = (137, "ROCKET_GRUNT_F"),
            ["scientist-front"] = (107, "SCIENTIST"), ["rival-late-front"] = (124, "RIVAL_LATE"), ["giovanni-front"] = (108, "LEADER_GIOVANNI")
        };
        var result = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        foreach (var row in doc.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!;
            SafariSource.Require(identities.Remove(id, out var expected) && row.GetProperty("sourceTrainerPicId").GetInt32() == expected.Id &&
                row.GetProperty("trainerPic").GetString() == "TRAINER_PIC_" + expected.Symbol && row.GetProperty("width").GetInt32() == 64 &&
                row.GetProperty("height").GetInt32() == 64 && row.GetProperty("sourceFrame").GetInt32() == 0 &&
                row.GetProperty("x").GetInt32() == 144 && row.GetProperty("y").GetInt32() == 8 &&
                row.GetProperty("texturePath").GetString() == id + ".png" && row.GetProperty("rgbaPath").GetString() == id + ".rgba", "Silph trainer identity differs.");
            var rgba = files[id + ".rgba"];
            SafariSource.Require(rgba.Length == 64 * 64 * 4 && SafariSource.Hash(rgba) == row.GetProperty("rgbaSha256").GetString() &&
                SafariSource.Hash(files[id + ".png"]) == row.GetProperty("sha256").GetString(), "Silph trainer pixels differ.");
            result.Add(new(id, 144, 8, [.. rgba]));
        }
        SafariSource.Require(identities.Count == 0, "Missing Silph trainer identity.");
        return result.ToImmutable();
    }
}
