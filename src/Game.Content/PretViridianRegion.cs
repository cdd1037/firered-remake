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

    public static MapId MapIdFromSourceSymbol(string symbol)
    {
        return new(symbol switch
        {
            "MAP_PALLET_TOWN" => "map.pallet-town",
            "MAP_PALLET_TOWN_PLAYERS_HOUSE_1F" => "map.players-house-1f",
            "MAP_PALLET_TOWN_PLAYERS_HOUSE_2F" => "map.players-house-2f",
            "MAP_PALLET_TOWN_RIVALS_HOUSE" => "map.rivals-house",
            "MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB" => "map.professor-oaks-lab",
            "MAP_ROUTE1" => "map.route-1", "MAP_VIRIDIAN_CITY" => "map.viridian-city",
            "MAP_VIRIDIAN_CITY_MART" => "map.viridian-mart", "MAP_ROUTE2" => "map.route-2",
            "MAP_ROUTE22" => "map.route-22", "MAP_ROUTE21_NORTH" => "map.route-21-north",
            "MAP_VIRIDIAN_CITY_GYM" => "map.viridian-city-gym", "MAP_VIRIDIAN_CITY_HOUSE" => "map.viridian-house",
            "MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F" => "map.viridian-pokemon-center-1f",
            "MAP_VIRIDIAN_CITY_POKEMON_CENTER_2F" => "map.viridian-pokemon-center-2f",
            "MAP_VIRIDIAN_CITY_SCHOOL" => "map.viridian-school",
            _ => throw new InvalidDataException("Unknown region source map symbol: " + symbol)
        });
    }



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
