using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// A separately authenticated Pokemon Center 1F source increment. Source availability does not
/// certify script handlers, healing animation, audio, PC storage, upstairs or link play.
/// </summary>
public sealed class PretViridianCenter
{
    public const string Profile = "firered-viridian-center-import-v1";
    public const string StableMapId = "map.viridian-pokemon-center-1f";
    public PretWorldMap Map { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public string GeneratedDirectory { get; }

    private PretViridianCenter(string directory, PretWorldMap map, ImmutableDictionary<string, int> constants,
        ImmutableDictionary<string, string> uiTexts)
    {
        GeneratedDirectory = directory; Map = map;
        Constants = constants; SourceUiTexts = uiTexts;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => PretViridianRegion.MapIdFromSourceSymbol(symbol);

    public static PretViridianCenter Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/viridian-center");
        var map = world.Maps[new MapId(StableMapId)];

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["HEAL_LOCATION_VIRIDIAN_CITY"] == 2 && constants["LOCALID_VIRIDIAN_NURSE"] == 1 &&
            constants["FLDEFF_POKECENTER_HEAL"] == 25 && constants["SCR_MENU_CANCEL"] == 127, "Center required source constants differ.");
        var storyTexts = PretStoryRuntime.Load(projectRoot).TextBank;
        var uiTexts = ImmutableDictionary<string, string>.Empty.Add("gText_PlayerScurriedToCenter", storyTexts["gText_PlayerScurriedToCenter"]);
        return new(directory, map, constants.ToImmutable(), uiTexts);
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
}
