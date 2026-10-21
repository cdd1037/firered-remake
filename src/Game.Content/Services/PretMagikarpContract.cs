using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>Shared story references required by the Route4 Magikarp sale.</summary>
public sealed class PretMagikarpContract
{
    public ImmutableArray<string> RequiredCommonScriptLabels { get; }
    public ImmutableArray<string> RequiredCommonTextLabels { get; }

    private PretMagikarpContract(JsonElement document)
    {
        RequiredCommonScriptLabels = ReadLabels(document, "commonScripts");
        RequiredCommonTextLabels = ReadLabels(document, "commonTexts");
        Require(!RequiredCommonScriptLabels.Intersect(RequiredCommonTextLabels, StringComparer.Ordinal).Any(),
            "Magikarp script and text references overlap.");
    }

    public static PretMagikarpContract Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        using var document = JsonDocument.Parse(ReadGenerated(root,
            "content/generated/data/services/magikarp-gift/magikarp-contract.json"));
        var value = document.RootElement;
        Require(value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("schemaVersion", out var schema) && schema.ValueKind == JsonValueKind.Number &&
            schema.TryGetInt32(out var version) && version == 1, "Unsupported Magikarp reference schema.");
        return new(value);
    }

    private static ImmutableArray<string> ReadLabels(JsonElement document, string property)
    {
        Require(document.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array,
            "Missing Magikarp reference array: " + property);
        var labels = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values.EnumerateArray())
        {
            var label = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            Require(!string.IsNullOrEmpty(label) && (char.IsAsciiLetter(label[0]) || label[0] == '_') &&
                label.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'), "Invalid Magikarp reference: " + property);
            Require(seen.Add(label!), "Duplicate Magikarp reference: " + label);
            labels.Add(label!);
        }
        Require(labels.Count > 0, "Empty Magikarp reference array: " + property);
        return labels.ToImmutable();
    }

    private static byte[] ReadGenerated(string root, string relative)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Split('/').Any(p => p is ".." or "." or ""), "Unsafe Magikarp resource path.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? info = new FileInfo(full); info is not null; info = info switch
        {
            FileInfo file => file.Directory,
            DirectoryInfo directory => directory.Parent,
            _ => null
        })
            Require(info.LinkTarget is null, "Magikarp resource symlink is unsupported: " + info.FullName);
        return File.ReadAllBytes(full);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
