using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated source increment. Content availability does not certify runtime handlers.</summary>
public sealed class PretViridianRegion
{
    public const string Profile = "firered-viridian-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public string GeneratedDirectory { get; }

    private PretViridianRegion(string directory,ImmutableDictionary<MapId, PretWorldMap> maps,        ImmutableDictionary<string, int> constants,ImmutableDictionary<string, string> sourceUiTexts)
    { GeneratedDirectory = directory; NewMaps = maps; Constants = constants; SourceUiTexts = sourceUiTexts; }

    public static PretViridianRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/viridian");

        var maps = world.MapsInDirectory(directory);

        var constants = PretSharedConstants.ForPackage(directory);
        var uiTexts = ContentJson.ParseStrict<ImmutableDictionary<string,string>>(
            File.ReadAllText(Path.Combine(directory, "ui-texts.json")), ContentJson.CreateOptions());
        return new(directory,maps,constants,uiTexts);
    }
}
