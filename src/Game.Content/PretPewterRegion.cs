using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// A separately authenticated Pewter City / gym / services source increment. Source availability does not
/// certify script handlers, trainer battles, wild encounters, Cut, hidden items or audio.
/// </summary>
public sealed class PretPewterRegion
{
    public const string Profile = "firered-pewter-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Dependencies { get; }
    public ImmutableArray<PretPewterTrainerSource> Trainers { get; }
    public ImmutableArray<string> MartItems { get; }
    public PretPewterHealLocation Healing { get; }
    public JsonElement GymReward => Dependencies.GetProperty("gymReward");
    public JsonElement Catalog { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretPewterRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement dependencies, JsonElement catalog)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Dependencies = dependencies; Catalog = catalog;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters = true,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        Trainers = dependencies.GetProperty("trainers").Deserialize<ImmutableArray<PretPewterTrainerSource>>(options);
        MartItems = dependencies.GetProperty("martInventories")[0].GetProperty("items").Deserialize<ImmutableArray<string>>(options);
        Healing = dependencies.GetProperty("healLocations")[0].GetProperty("table").Deserialize<PretPewterHealLocation>(options)
            ?? throw new InvalidDataException("Missing Pewter heal location.");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_PEWTER_CITY" => new("map.pewter-city"),
        "MAP_PEWTER_CITY_GYM" => new("map.pewter-gym"),
        "MAP_PEWTER_CITY_MART" => new("map.pewter-mart"),
        "MAP_PEWTER_CITY_POKEMON_CENTER_1F" => new("map.pewter-pokemon-center-1f"),
        "MAP_PEWTER_CITY_HOUSE1" => new("map.pewter-house-1"),
        "MAP_PEWTER_CITY_HOUSE2" => new("map.pewter-house-2"),
        "MAP_PEWTER_CITY_MUSEUM_1F" => new("map.pewter-museum-1f"),
        "MAP_PEWTER_CITY_MUSEUM_2F" => new("map.pewter-museum-2f"),
        "MAP_PEWTER_CITY_POKEMON_CENTER_2F" => new("map.pewter-pokemon-center-2f"),
        "MAP_ROUTE3" => new("map.route-3"),
        _ => PretForestRegion.MapIdFromSourceSymbol(symbol)
    };

    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var values = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), values);
    }

    public static PretPewterRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/pewter");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "Pewter manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"maps/regions/pewter/"+path))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate pewter output.");
        }
        using var catalog = JsonDocument.Parse(outputs["pewter-catalog.json"]);

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Pewter City / gym / services; runtime certification is separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("mapPath").GetString()!;
            if(runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!),out var compact) )
            {
                var runtimeMap=PretWorldMap.FromRuntime(compact,metadata,SafeFile(directory,relative),PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"),MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id,runtimeMap),"Duplicate runtime map.");continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        Require(maps.Count == 8 && maps.Values.Sum(x => x.Width*x.Height) == 3073, "Incomplete pewter map set.");

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["TRAINER_LEADER_BROCK"] == 414 && constants["TRAINER_CAMPER_LIAM"] == 142,
            "Pewter trainer source constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 2 &&
            dependencies.RootElement.GetProperty("encounters").GetArrayLength() == 0, "Pewter dependency scope differs.");
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), dependencies.RootElement.Clone(), catalog.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe pewter path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Pewter directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Pewter file symlink is unsupported: " + path);
        }
        return result;
    }
}

/// <summary>Original ordered custom moves include MOVE_NONE slots; no battle behavior is certified.</summary>
public sealed record PretPewterTrainerMonSource(string Species, int SourceSpeciesId, int Level,
    int SourceIv, string HeldItem, ImmutableArray<string> CustomMoves, string SpeciesName);

public sealed record PretPewterTrainerSource(string Symbol, int SourceTrainerId,
    ImmutableDictionary<string, string> Declaration, string Name, ImmutableArray<int> EncodedName,
    int BaseMoney, ImmutableArray<PretPewterTrainerMonSource> Party, PretPewterSourceLocation PartySourceRef,
    PretPewterSourceLocation SourceRef, int SourceTrainerClass, int SourceTrainerPic, int EncounterMusicGender,
    int AiFlags, int PartyFlags, string ScriptEntry, string IntroText, string DefeatText,
    string? PostBattleScript, string? MusicOption, PretPewterSourceLocation ScriptSourceRef, string RuntimeStatus);

public sealed record PretPewterHealLocation(string Id, string Map, int X, int Y,
    [property: JsonPropertyName("respawn_map")] string RespawnMap,
    [property: JsonPropertyName("respawn_npc")] string RespawnNpc);

/// <summary>Original definition line retained alongside the immutable source-file identity.</summary>
public sealed record PretPewterSourceLocation(SourceKind Kind, string Path, string Symbol, string Repository, string Commit, int Line)
{
    public SourceRef Reference => new(Kind,Path,Symbol,Repository,Commit);
}
