using System.Collections.Immutable;

namespace Game.Content;

public static class PretMoveDeleterTexts
{
    public const string RelativePath = "content/generated/maps/regions/move-deleter/texts.json";
    public static ImmutableDictionary<string, string> LoadProject(string root, IReadOnlyDictionary<string, string>? sourceTexts = null)
    {
        var originals = ContentJson.ParseStrict<ImmutableDictionary<string, string>>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        sourceTexts ??= PretBattleText.Load(root);
        return originals.Keys.ToImmutableDictionary(symbol => symbol, symbol => sourceTexts[symbol], StringComparer.Ordinal);
    }
}
