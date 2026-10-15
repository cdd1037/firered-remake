using System.Collections.Immutable;

namespace Game.Content;

public static class PretMoveDeleterTexts
{
    public const string RelativePath = "content/generated/move-deleter/texts.json";
    public static ImmutableDictionary<string, string> LoadProject(string root) =>
        ContentJson.ParseStrict<ImmutableDictionary<string, string>>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
}
