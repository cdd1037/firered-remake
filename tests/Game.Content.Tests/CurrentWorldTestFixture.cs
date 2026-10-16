using System.Text.Json;

namespace FireRed.Tests.Content;

internal static class CurrentWorldTestFixture
{
    // A world is a closed runtime input: selective regional payloads alone are no
    // longer sufficient to resolve cross-region endpoints and layout variants.
    public static IEnumerable<string> RelativeFiles()
    {
        yield return "maps/world.json";
        yield return "maps/tilesets.json";
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/maps/world.json")));
        foreach (var entry in document.RootElement.GetProperty("maps").EnumerateArray())
            yield return entry.GetProperty("path").GetString()!;
    }
}
