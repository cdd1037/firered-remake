using Game.Content;
using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed record PokemonLeaguePresentationData(JsonElement Manifest,Dictionary<string,byte[]> Files);
internal static class PokemonLeaguePresentationAssets
{
    internal const string Base="content/generated/sprites/pokemon/pokemon-league/";

    internal static PokemonLeaguePresentationData Load(string root, IReadOnlyDictionary<string, string>? sourceTexts = null)
    {
        var package = PresentationPackage.Load(root, Base, relativeOutputKeys: true);
        sourceTexts ??= PretBattleText.Load(root);
        var manifest = JsonNode.Parse(package.Manifest.GetRawText())!;
        string Text(string symbol) => sourceTexts[symbol].Replace("\\n", "\n", StringComparison.Ordinal);
        var texts = new JsonObject();
        foreach (var row in manifest["textSymbols"]!.AsArray())
        {
            string symbol = row!.GetValue<string>();
            texts.Add(symbol, Text(symbol));
        }
        manifest["texts"] = texts;
        var credits = manifest["credits"]!;
        credits["title"] = Text(credits["titleSymbol"]!.GetValue<string>());
        foreach (var row in credits["texts"]!.AsObject())
        {
            var block = row.Value!.AsObject();
            foreach (string field in new[] { "title", "names" })
            {
                if (!block.ContainsKey(field + "Symbol")) throw new InvalidDataException("Credits source text binding missing: " + row.Key);
                string symbol = block[field + "Symbol"]!.GetValue<string>();
                block[field] = Text(symbol);
            }
        }
        return new(JsonSerializer.SerializeToElement(manifest), package.Files);
    }
}
