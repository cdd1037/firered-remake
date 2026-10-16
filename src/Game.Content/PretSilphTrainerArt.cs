using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

/// <summary>The five original Silph trainer portrait identities, including the later rival portrait.</summary>
public static class PretSilphTrainerArt
{
    public const string GeneratedDirectory = "content/generated/sprites/trainers/silph/";
    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        var bytes = SafariSource.Read(root,GeneratedDirectory + "manifest.json");
        using var json = JsonDocument.Parse(bytes); var doc = json.RootElement;
        SafariSource.Require(doc.GetProperty("schemaVersion").GetInt32() == 1 && doc.GetProperty("portraits").GetArrayLength() == 5 , "Silph portrait manifest scope differs.");
        var files = PretSilphPresentationSource.Outputs(root, GeneratedDirectory,
            SafariSource.Parse<ImmutableArray<SnorlaxArtOutput>>(System.Text.Encoding.UTF8.GetBytes(doc.GetProperty("outputs").GetRawText())));
        var identities = new Dictionary<string, (int Id, string Symbol)>(StringComparer.Ordinal)
        {
            ["rocket-grunt-m-front"] = (109, "ROCKET_GRUNT_M"),["rocket-grunt-f-front"] = (137, "ROCKET_GRUNT_F"),["scientist-front"] = (107, "SCIENTIST"),["rival-late-front"] = (124, "RIVAL_LATE"),["giovanni-front"] = (108, "LEADER_GIOVANNI")
        };
        var result = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        foreach (var row in doc.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!;
            SafariSource.Require(identities.Remove(id, out var expected) && row.GetProperty("sourceTrainerPicId").GetInt32() == expected.Id &&
                row.GetProperty("trainerPic").GetString() == "TRAINER_PIC_" + expected.Symbol && row.GetProperty("width").GetInt32() == 64 &&
                row.GetProperty("height").GetInt32() == 64 && row.GetProperty("sourceFrame").GetInt32() == 0 &&
                row.GetProperty("x").GetInt32() == 144 && row.GetProperty("y").GetInt32() == 8 &&
                row.GetProperty("texturePath").GetString() == id + ".png" && row.GetProperty("texturePath").GetString() == id + ".png", "Silph trainer identity differs.");
            var pngBytes = files[id + ".png"];
            SafariSource.Require(pngBytes.Length > 0, "Silph trainer pixels differ.");
            result.Add(new(id, 144, 8, [.. pngBytes]));
        }
        SafariSource.Require(identities.Count == 0, "Missing Silph trainer identity.");
        return result.ToImmutable();
    }
}
