using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Events;
namespace Game.Content;

/// <summary>Imported static script menu definitions.</summary>
public static class PretSourceGridMenus
{
    public const string Profile = "firered-source-grid-menus-v1";

    public static ImmutableDictionary<string, StoryMenuDefinition> Load(string root)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/source-grid-menus/manifest.json")));
        var manifest = document.RootElement;
        var options = new JsonSerializerOptions
        {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
        };
        var menus = manifest.GetProperty("menus").Deserialize<ImmutableArray<StoryMenuDefinition>>(options);
        if (menus.IsDefaultOrEmpty || menus.Any(m => m.Options.IsDefaultOrEmpty)) throw new InvalidDataException("Source grid menu missing.");
        return menus.ToImmutableDictionary(m => m.Id, StringComparer.Ordinal);
    }
}
