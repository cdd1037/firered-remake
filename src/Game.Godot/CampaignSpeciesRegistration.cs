using System.Text.Json;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    // Inherited art must reuse the scene-owned texture, never decode or replace it.
    private static void VerifySpeciesTexture(Texture2D texture, JsonElement row, string package)
    {
        if (texture.GetWidth() != row.GetProperty("width").GetInt32() ||
            texture.GetHeight() != row.GetProperty("height").GetInt32())
            throw new InvalidDataException($"{package} inherited pixels differ.");
    }

    private void RegisterOwnedBattler(JsonElement row, IReadOnlyDictionary<string, byte[]> files,
        string package, bool inherited, bool promotePortrait = false)
    {
        int species = row.GetProperty("sourceSpeciesId").GetInt32();
        string side = row.GetProperty("side").GetString()!;
        bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
        var key = (species, side, shiny);
        var position = new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32());
        Texture2D texture;
        if (inherited)
        {
            if (!_battleArt.TryGetValue(key, out var existing) || existing.Position != position)
                throw new InvalidDataException($"{package} inherited battler binding differs.");
            texture = existing.Texture;
            VerifySpeciesTexture(texture, row, package);
        }
        else
        {
            texture = PresentationTexture(files, row);
            _battleArt.Add(key, new(texture, position));
        }
        if (side != "front" || shiny) return;
        if (!inherited || promotePortrait) _portraits.Add(species, texture);
        else if (!_portraits.TryGetValue(species, out var portrait) || !ReferenceEquals(portrait, texture))
            throw new InvalidDataException($"{package} inherited portrait binding differs.");
    }

    private void RegisterOwnedIcons(JsonElement rows, IReadOnlyDictionary<string, byte[]> files, string package)
    {
        foreach (var row in rows.EnumerateArray())
        {
            var species = new SpeciesId(row.GetProperty("speciesId").GetString()!);
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_storageIcons.TryGetValue(species, out var existing))
                    throw new InvalidDataException($"{package} inherited icon missing.");
                VerifySpeciesTexture(existing, row, package);
            }
            else _storageIcons.Add(species, PresentationTexture(files, row));
        }
    }

    private void RegisterOwnedFootprints(JsonElement rows, IReadOnlyDictionary<string, byte[]> files, string package)
    {
        foreach (var row in rows.EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_route22Footprints.TryGetValue(species, out var existing))
                    throw new InvalidDataException($"{package} inherited footprint missing.");
                VerifySpeciesTexture(existing, row, package);
            }
            else _route22Footprints.Add(species, PresentationTexture(files, row));
        }
    }

    private void VerifyInheritedMoveGeometry(JsonElement manifest, string package)
    {
        foreach (var row in manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException($"{package} inherited move geometry differs.");
        }
    }
}
