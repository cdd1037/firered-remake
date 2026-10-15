using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Original map identity is stable across Single, Double and Knockout layouts. Mixed selects a format per floor.</summary>
public enum PretTrainerTowerLayoutFormat { Common = -1, Single = 0, Double = 1, Knockout = 2 }

/// <summary>Authenticated Trainer Tower resources; loading these assets does not admit a challenge or alter current composition.</summary>
public sealed class PretTrainerTowerRegion
{
    public const string Profile = "firered-trainer-tower-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> Maps { get; }
    public ImmutableDictionary<(MapId Map, PretTrainerTowerLayoutFormat Format), PretWorldMap> Layouts { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }

    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretTrainerTowerRegion(SilphSourcePackage source,
        ImmutableDictionary<(MapId Map, PretTrainerTowerLayoutFormat Format), PretWorldMap> layouts)
    {
        Layouts = layouts;
        Maps = layouts.Where(p => p.Key.Format is PretTrainerTowerLayoutFormat.Common or PretTrainerTowerLayoutFormat.Single)
            .ToImmutableDictionary(p => p.Key.Map, p => p.Value); SourceFiles = ImmutableArray<PretWorldSourceFile>.Empty; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        Constants = PretSharedConstants.ForPackage(source.Directory);
    }

    /// <summary>format is the floor's resolved format (0..2), never the Mixed course value 3.</summary>
    public PretWorldMap GetMap(MapId map, int format) => GetMap(map, (PretTrainerTowerLayoutFormat)format);
    public PretWorldMap GetMap(MapId map, PretTrainerTowerLayoutFormat format)
    {
        Need(Enum.IsDefined(format), "Mixed is a course, not a Trainer Tower layout format.");
        if (Layouts.TryGetValue((map, PretTrainerTowerLayoutFormat.Common), out var common)) return common;
        return Layouts.TryGetValue((map, format), out var floor) ? floor : throw new InvalidDataException("Unknown Trainer Tower map/format.");
    }
    public PretWorldMap GetMap(MapId map, TrainerTowerBattleFormat format) => GetMap(map, (int)format);
    /// <summary>Original dynamic object variable slots mapped to source graphics symbols.</summary>
    public ImmutableDictionary<int, string> NpcGraphics(TrainerTowerBattleFormat format, TrainerTowerFloorTemplate template)
    {
        Need(Enum.IsDefined(format) && template.Format == format && !template.Trainers.IsDefaultOrEmpty, "Tower NPC template format differs.");
        var data = Contracts.GetProperty("dynamicGraphics");
        string Single(string facility) => data.GetProperty("single").EnumerateArray()
            .Where(r => r.GetProperty("facilityClass").GetString() == facility)
            .Select(r => r.GetProperty("graphicsId").GetString()!).FirstOrDefault() ?? data.GetProperty("default").GetString()!;
        var result = ImmutableDictionary.CreateBuilder<int, string>();
        if (format == TrainerTowerBattleFormat.Single) result.Add(1, Single(template.Trainers[0].FacilityClass));
        else if (format == TrainerTowerBattleFormat.Double)
        {
            var row = data.GetProperty("double").EnumerateArray().FirstOrDefault(r => r.GetProperty("facilityClass").GetString() == template.Trainers[0].FacilityClass);
            result.Add(0, row.ValueKind == JsonValueKind.Undefined ? data.GetProperty("default").GetString()! : row.GetProperty("graphicsId1").GetString()!);
            result.Add(3, row.ValueKind == JsonValueKind.Undefined ? data.GetProperty("default").GetString()! : row.GetProperty("graphicsId2").GetString()!);
        }
        else
        {
            Need(template.Trainers.Length == 3, "Knockout requires all three original trainer graphics.");
            int[] slots = [2, 0, 1];
            for (int i = 0; i < 3; i++) result.Add(slots[i], Single(template.Trainers[i].FacilityClass));
        }
        return result.ToImmutable();
    }
    public int OpponentTextGender(TrainerTowerFloorTemplate template,int trainerIndex,bool secondDoubleSpeaker)
    {
        Need(trainerIndex>=0&&trainerIndex<template.Trainers.Length,"Tower text trainer index differs.");
        var data=Contracts.GetProperty("dynamicGraphics");bool pair=template.Format==TrainerTowerBattleFormat.Double;
        string facility=template.Trainers[pair?0:trainerIndex].FacilityClass;
        var row=data.GetProperty(pair?"double":"single").EnumerateArray().FirstOrDefault(r=>r.GetProperty("facilityClass").GetString()==facility);
        string gender=row.ValueKind==JsonValueKind.Undefined?"MALE":row.GetProperty(pair?(secondDoubleSpeaker?"gender2":"gender1"):"gender").GetString()!;
        return gender=="FEMALE"?1:0;
    }
    public int GraphicsIdValue(string symbol) => Constants.TryGetValue(symbol, out int value) && symbol.StartsWith("OBJ_EVENT_GFX_", StringComparison.Ordinal)
        ? value : throw new InvalidDataException("Unknown Tower graphics symbol: " + symbol);
    public ImmutableDictionary<string, int> NpcGraphicsValues(TrainerTowerBattleFormat format, TrainerTowerFloorTemplate template) =>
        NpcGraphics(format, template).ToImmutableDictionary(p => "VAR_OBJ_GFX_ID_" + p.Key, p => GraphicsIdValue(p.Value), StringComparer.Ordinal);
    /// <summary>Retains the original numeric lookup comparison against converted trainer class, including its source table quirk.</summary>
    public string EncounterMusic(TrainerTowerFloorTemplate template, int trainerIndex)
    {
        Need(trainerIndex >= 0 && trainerIndex < template.Trainers.Length, "Tower music trainer index is outside the floor template.");
        var data = Contracts.GetProperty("encounterMusic"); int trainerClass = template.Trainers[trainerIndex].SourceTrainerClass;
        var match = data.GetProperty("lookup").EnumerateArray().FirstOrDefault(r => r.GetProperty("comparisonValue").GetInt32() == trainerClass);
        int index = match.ValueKind == JsonValueKind.Undefined ? data.GetProperty("defaultMusicIndex").GetInt32() : match.GetProperty("musicIndex").GetInt32();
        return data.GetProperty("songs").EnumerateArray().Single(r => r.GetProperty("musicIndex").GetInt32() == index).GetProperty("song").GetString()!;
    }
    public string LayoutId(MapId map, PretTrainerTowerLayoutFormat format)
    {
        var selected = GetMap(map, format);
        return Catalog.GetProperty("maps").EnumerateArray().Single(s =>
            s.GetProperty("directory").GetString() == Path.GetRelativePath(GeneratedDirectory, selected.DirectoryPath).Replace('\\', '/'))
            .GetProperty("layoutId").GetString()!;
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_TRAINER_TOWER_LOBBY" => new("map.trainer-tower-lobby"),
        "MAP_TRAINER_TOWER_ROOF" => new("map.trainer-tower-roof"),
        "MAP_TRAINER_TOWER_ELEVATOR" => new("map.trainer-tower-elevator"),
        "MAP_SEVEN_ISLAND_TRAINER_TOWER" => new("map.seven-island-trainer-tower"),
        "MAP_DYNAMIC" => PretRocketHideoutRegion.DynamicMapId,
        _ when Regex.IsMatch(symbol, "^MAP_TRAINER_TOWER_[1-8]F$", RegexOptions.CultureInvariant) => new("map.trainer-tower-" + symbol[^2..].ToLowerInvariant()),
        _ => throw new InvalidDataException("Unknown Trainer Tower map symbol: " + symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Tower indoor connection scope differs.");
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    public static PretTrainerTowerRegion Load(string projectRoot)
    {
        var source = Source(projectRoot);
        var catalog = source.Document("catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Trainer Tower resources; runtime admission is separate.", Profile);
        var layouts = ImmutableDictionary.CreateBuilder<(MapId Map, PretTrainerTowerLayoutFormat Format), PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string relative = spec.GetProperty("directory").GetString()!, path = spec.GetProperty("path").GetString()!;
            string id = spec.GetProperty("stableId").GetString()!;
            Need(path == relative + "/runtime-map.json", "Tower map path differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Tower source map identity differs.");
            var format = Enum.Parse<PretTrainerTowerLayoutFormat>(spec.GetProperty("format").GetString()!);
            string originalId = runtime.Events.GetProperty("layout").GetString()!;
            string expectedId = originalId + (format == PretTrainerTowerLayoutFormat.Double ? "_DOUBLES" : format == PretTrainerTowerLayoutFormat.Knockout ? "_KNOCKOUT" : "");
            Need(spec.GetProperty("layoutId").GetString() == expectedId, "Tower map/format layout binding differs.");
            layouts.Add((new(id), format), PretWorldMap.FromRuntime(runtime, metadata, Path.Combine(source.Directory, relative),
                PretOverworldTextures.Resolve(source.Directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(layouts.Count == 27 && layouts.Keys.Select(k => k.Map).Distinct().Count() == 11, "Tower map/layout count differs.");

        var files = ImmutableArray<PretWorldSourceFile>.Empty.ToDictionary(f => f.Path, StringComparer.Ordinal);
        
        return new(source, layouts.ToImmutable());
    }
    private static SilphSourcePackage Source(string root) => new(root,"trainer-tower-region");
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
