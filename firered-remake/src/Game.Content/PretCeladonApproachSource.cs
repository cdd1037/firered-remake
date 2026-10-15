using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

internal sealed class PretCeladonApproachSource
{
    internal const string Commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal ImmutableArray<PretWorldSourceFile> Files { get; }

    private PretCeladonApproachSource(ImmutableArray<PretWorldSourceFile> files)
    {
        Files = files;
    }

    internal static PretCeladonApproachSource Load(string root, string stem)
    {
        using var document = JsonDocument.Parse(Read(root, "scripts/" + stem + ".lock.json"));
        var source = document.RootElement;
        Require(source.GetProperty("schemaVersion").GetInt32() == 1, "Unsupported Celadon source schema.");
        var files = source.GetProperty("files").EnumerateArray().Select(file =>
        {
            var path = file.GetProperty("path").GetString()!;
            return new PretWorldSourceFile(path,                $"https://raw.githubusercontent.com/pret/pokefirered/{Commit}/{path}");
        }).ToImmutableArray();
        return new(files);
    }

    internal static ImmutableDictionary<string, byte[]> Outputs(string root, string stem)
    {
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/" + stem + "/manifest.json"));
        Require(manifest.RootElement.GetProperty("schemaVersion").GetInt32() == 1, "Unsupported Celadon output schema.");
        return manifest.RootElement.GetProperty("outputs").EnumerateArray().ToImmutableDictionary(
            output => output.GetProperty("path").GetString()!,
            output => Read(root, "content/generated/" + stem + "/" + output.GetProperty("path").GetString()!),
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
