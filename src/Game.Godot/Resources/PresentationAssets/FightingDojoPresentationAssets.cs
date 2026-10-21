using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Content;

internal sealed record FightingDojoDexDocument(int SchemaVersion,
    ImmutableArray<PokedexMenuEntry> Entries);
internal sealed record FightingDojoPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Two source dex footprints plus authenticated bindings to existing League and Black Belt art.</summary>
internal static class FightingDojoPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/fighting-dojo/";

    internal static FightingDojoPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "sprites", "pokemonIcons", "footprints", "portraits");
        var manifest = package.Manifest;
        var files = package.Files;
        var dex = JsonSerializer.Deserialize<FightingDojoDexDocument>(files[Base + "dex-entries.json"], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Fighting Dojo dex document missing.");
        if (dex.SchemaVersion != 1 ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 106, 107 }) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(new[] { 106, 107 }) ||
            !manifest.GetProperty("trainers").EnumerateArray().Select(e => e.GetProperty("sourceTrainerId").GetInt32()).SequenceEqual(Enumerable.Range(317, 5)))
            throw new InvalidDataException("Fighting Dojo presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!, e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png))
                throw new InvalidDataException("Fighting Dojo dex texture differs.");
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
