using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

internal sealed class PretCeladonApproachSource
{
    internal const string Commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal static ImmutableDictionary<string, byte[]> Outputs(string root, string stem, params string[] excludedOutputs)
    {
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/" + stem + "/manifest.json"));
        Require(manifest.RootElement.GetProperty("schemaVersion").GetInt32() == 1, "Unsupported Celadon output schema.");
        return manifest.RootElement.GetProperty("outputs").EnumerateArray().Where(output => !excludedOutputs.Contains(output.GetProperty("path").GetString(), StringComparer.Ordinal)).ToImmutableDictionary(
            output => output.GetProperty("path").GetString()!,
            output => Read(root, output.GetProperty("path").GetString()!.StartsWith("content/generated/", StringComparison.Ordinal)
                ? output.GetProperty("path").GetString()! : "content/generated/" + stem + "/" + output.GetProperty("path").GetString()!),
            StringComparer.Ordinal);
    }

    internal static byte[] Read(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\') &&
            !relative.Split('/').Any(part => part is "" or "." or ".."), "Unsafe Celadon path.");
        return File.ReadAllBytes(Path.Combine(root, relative));
    }

    internal static void Require(bool condition, string error)
    {
        if (!condition) throw new InvalidDataException(error);
    }
}
