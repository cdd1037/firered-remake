using Game.Content;
using System.Text.Json;

internal sealed record PokemonLeaguePresentationData(JsonElement Manifest,Dictionary<string,byte[]> Files);
internal static class PokemonLeaguePresentationAssets
{
    internal const string Base="content/generated/sprites/pokemon/pokemon-league/";

    internal static PokemonLeaguePresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, relativeOutputKeys: true);
        return new(package.Manifest, package.Files);
    }
}
