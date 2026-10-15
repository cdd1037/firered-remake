using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Events;
namespace Game.Content;

/// <summary>Authenticated, finite inventory of static original script menu definitions.</summary>
public static class PretSourceGridMenus
{
    public const string Profile = "firered-source-grid-menus-v1";

    public static ImmutableDictionary<string, StoryMenuDefinition> Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe source grid menu path.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null)
                throw new InvalidDataException("Source grid menu size/type differs.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null)
                    throw new InvalidDataException("Source grid menu symlink rejected.");
            return File.ReadAllBytes(file.FullName);
        }
        using var locked = JsonDocument.Parse(Read("scripts/source-grid-menus.lock.json"));
        using var document = JsonDocument.Parse(Read("content/generated/source-grid-menus/manifest.json"));
        var source = locked.RootElement;
        var manifest = document.RootElement;
        if (source.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("schemaVersion").GetInt32() != 1 || manifest.GetProperty("menus").GetArrayLength() != 1)
            throw new InvalidDataException("Source grid menu identity differs.");
        var options = new JsonSerializerOptions
        {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
        };
        var menus = manifest.GetProperty("menus").Deserialize<ImmutableArray<StoryMenuDefinition>>(options);
        if (menus.IsDefaultOrEmpty || menus.Any(m => m.Options.IsDefaultOrEmpty)) throw new InvalidDataException("Source grid menu missing.");
        return menus.ToImmutableDictionary(m => m.Id, StringComparer.Ordinal);
    }
}
