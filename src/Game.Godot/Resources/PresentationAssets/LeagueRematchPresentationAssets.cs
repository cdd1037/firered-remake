using Game.Content;
using System.Text.Json;

internal sealed record LeagueRematchPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class LeagueRematchPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/league-rematch/";

    internal static LeagueRematchPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "assets");
        var manifest = package.Manifest;
        var files = package.Files;
        int[] admitted = [169, 200, 214, 230, 248];
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in admitted from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            !manifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual(admitted))
            throw new InvalidDataException("League rematch presentation inventory differs.");
        var inheritedSprites = manifest.GetProperty("inheritedSprites").EnumerateArray().ToArray();
        var expectedInheritedSprites = (from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (208, side, variant)).ToHashSet();
        if (!inheritedSprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedInheritedSprites))
            throw new InvalidDataException("Inherited Steelix sprite inventory differs.");
        var trainers = manifest.GetProperty("trainers").EnumerateArray().ToArray();
        if (!trainers.Select(t => t.GetProperty("sourceTrainerId").GetInt32()).Order().SequenceEqual(Enumerable.Range(735, 7)))
            throw new InvalidDataException("League rematch trainer inventory differs.");
        return new(manifest.Clone(), files);
    }
}
