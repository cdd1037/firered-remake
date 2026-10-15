using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Original TM27/TM29/TM42 maps and unchanged TM18/TM48 map anchors. Loading does not admit gameplay.</summary>
public sealed class PretOriginalTmAcquisition
{
    public const string ResourceId = "firered-original-tm-acquisition-v1";
    public static MapId ReturnRoomMapId { get; } = new("map.route-12-north-entrance-2f");
    public static MapId PsychicHouseMapId { get; } = new("map.saffron-city-mr-psychics-house");
    public static MapId MemorialMapId { get; } = new("map.five-island-memorial-pillar");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, JsonElement> RetainedEvents { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement ServiceBindings => Contracts.GetProperty("serviceBindings");
    public FiveIslandTrainerDocument MemorialTrainers { get; }
    public JsonElement MemorialEncounters { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretOriginalTmAcquisition(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; Catalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        NpcSources = source.Document("npc-source.json");
        MemorialTrainers = source.Parse<FiveIslandTrainerDocument>("memorial-trainers.json");
        MemorialEncounters = source.Document("memorial-encounters.json");
        Need(            !MemorialTrainers.CompleteSourceRoster && MemorialTrainers.Trainers.Length == 3 && MemorialTrainers.Battles.Length == 3 &&
            MemorialTrainers.Trainers.Sum(t => t.Party.Length) == 6 && MemorialTrainers.Trainers.All(t => !t.DoubleBattle && t.PartyFlags == 0),
            "Original Memorial trainer source scope differs.");
        RetainedEvents = Contracts.GetProperty("retainedMaps").EnumerateArray().ToImmutableDictionary(
            e => new MapId(e.GetProperty("mapId").GetString()!),
            e => SilphSourcePackage.Json(source.Raw[e.GetProperty("sourceRef").GetProperty("path").GetString()!]));
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE12_NORTH_ENTRANCE_2F" => ReturnRoomMapId,
        "MAP_SAFFRON_CITY_MR_PSYCHICS_HOUSE" => PsychicHouseMapId,
        "MAP_FIVE_ISLAND_MEMORIAL_PILLAR" => MemorialMapId,
        "MAP_ROUTE12_NORTH_ENTRANCE_1F" => new("map.route-12-north-entrance-1f"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        "MAP_FIVE_ISLAND_MEADOW" => new("map.five-island-meadow"),
        "MAP_ROUTE12" => new("map.route-12"),
        "MAP_ROUTE15" => new("map.route-15"),
        _ => throw new InvalidDataException("Unknown original TM acquisition map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == ReturnRoomMapId || map.Id == PsychicHouseMapId || map.Id == MemorialMapId,
            "Unexpected original TM map adapter.");
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new ConnectedFieldConnection(
                c.GetProperty("direction").GetString() switch {
                    "left" => Direction.West, "right" => Direction.East, "up" => Direction.North, "down" => Direction.South,
                    _ => throw new InvalidDataException("Unknown original connection direction.") },
                c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    /// <summary>Append exactly three source instances; never replace existing Route12/15 or entrance geometry.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> MountMaps(ImmutableDictionary<MapId, PretWorldMap> existing)
    {
        ValidateRetainedMaps(existing);
        Need(!NewMaps.Keys.Any(existing.ContainsKey), "Original TM acquisition maps were already mounted.");
        return existing.AddRange(NewMaps);
    }

    public void ValidateRetainedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var (id, events) in RetainedEvents)
            Need(existing.TryGetValue(id, out var map) && JsonElement.DeepEquals(map.Events, events),
                "Original TM retained event source differs: " + id.Value);
    }

    /// <summary>Authenticate typed shared-service bindings against the actual imported event and source label inventory.</summary>
    public ImmutableArray<FieldItemServiceDefinition> ServiceDefinitions()
    {
        var labels = ScriptInventory.GetProperty("blocks").EnumerateArray()
            .Select(b => b.GetProperty("label").GetString()!).ToHashSet(StringComparer.Ordinal);
        var results = ImmutableArray.CreateBuilder<FieldItemServiceDefinition>();
        foreach (var binding in ServiceBindings.EnumerateArray())
        {
            var mapId = new MapId(binding.GetProperty("mapId").GetString()!);
            string id = binding.GetProperty("id").GetString()!, script = binding.GetProperty("scriptId").GetString()!;
            string group = binding.GetProperty("eventGroup").GetString()!;
            int index = binding.GetProperty("eventIndex").GetInt32();
            Need(NewMaps.TryGetValue(mapId, out var map) && group is "object_events" or "bg_events", "Original TM service map/group differs.");
            var events = map!.Events.GetProperty(group);
            Need(index >= 0 && index < events.GetArrayLength(), "Original TM service event index differs.");
            var e = events[index];
            Need(JsonElement.DeepEquals(e, binding.GetProperty("sourceEvent")) && e.GetProperty("script").GetString() == script,
                "Original TM service event binding differs.");
            int? Optional(string name) => binding.GetProperty(name).ValueKind == JsonValueKind.Null ? null : binding.GetProperty(name).GetInt32();
            int? local = Optional("sourceLocalId"), x = Optional("backgroundX"), y = Optional("backgroundY");
            Need(group == "object_events" ? local == index + 1 && x is null && y is null :
                local is null && x == e.GetProperty("x").GetInt32() && y == e.GetProperty("y").GetInt32(), "Original TM service interaction position differs.");
            var programs = binding.GetProperty("programIds").EnumerateArray().Select(p => p.GetString()!).ToImmutableArray();
            var items = binding.GetProperty("itemSymbols").EnumerateArray().Select(p => p.GetString()!).ToImmutableArray();
            Need(programs.Contains(script) && programs.All(labels.Contains) && programs.Distinct(StringComparer.Ordinal).Count() == programs.Length &&
                !items.IsEmpty && items.Distinct(StringComparer.Ordinal).Count() == items.Length && items.All(Constants.ContainsKey),
                "Original TM service call graph or item scope differs.");
            results.Add(new(id, mapId, script, local, x, y, programs, items));
        }
        Need(results.Count == 4 && results.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() == 4 &&
            results.Select(r => (r.Map, r.ScriptId)).Distinct().Count() == 4, "Original TM service identity collection differs.");
        return results.ToImmutable();
    }

    /// <summary>Preserve the Memorial's ordinary item ball and hidden items through the existing shared pickup mechanism.</summary>
    public ImmutableArray<FieldPickupDefinition> MemorialPickups(EarlyShopCatalog shop)
    {
        var map = NewMaps[MemorialMapId];
        var result = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        var actors = map.Events.GetProperty("object_events").EnumerateArray().Select((e, i) => (Event: e, LocalId: i + 1));
        foreach (var (e, localId) in actors.Where(p => p.Event.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_ITEM_BALL"))
        {
            string script = e.GetProperty("script").GetString()!;
            var block = ScriptInventory.GetProperty("blocks").EnumerateArray().Single(b => b.GetProperty("label").GetString() == script);
            var code = block.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("text").GetString()!.Trim()).Where(l => l.Length > 0).ToArray();
            Need(code.Length == 2 && code[0].StartsWith("finditem ", StringComparison.Ordinal) && code[1] == "end", "Original Memorial item ball script differs.");
            string symbol = code[0][9..];
            Add(e, symbol, script, localId, false, 1);
        }
        foreach (var e in map.Events.GetProperty("bg_events").EnumerateArray().Where(e => e.GetProperty("type").GetString() == "hidden_item"))
        {
            Need(!e.GetProperty("underfoot").GetBoolean(), "Unexpected Memorial underfoot item.");
            Add(e, e.GetProperty("item").GetString()!, "EventScript_HiddenItemScript", null, true, e.GetProperty("quantity").GetInt32());
        }
        Need(result.Count == 5 && result.Count(p => p.Hidden) == 4 && result.Select(p => p.Flag).Distinct(StringComparer.Ordinal).Count() == 5,
            "Original Memorial ordinary pickup collection differs.");
        return result.ToImmutable();

        void Add(JsonElement e, string symbol, string script, int? local, bool hidden, int quantity)
        {
            var id = new ItemId("pret:" + symbol);
            var item = shop.Items.SingleOrDefault(i => i.Id == id) ?? throw new InvalidDataException("Memorial item is not admitted: " + symbol);
            string flag = e.GetProperty("flag").GetString()!;
            Need(Constants[symbol] == item.SourceItemId && quantity > 0, "Original Memorial item source ID/quantity differs.");
            result.Add(new("original-tm-acquisition:" + flag, map.SourceMapSymbol, hidden, local,
                e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                script, symbol, item.SourceItemId, id, item.Name, item.Pocket, quantity, flag, Constants[flag]));
        }
    }

    /// <summary>Uses original object rows and shared machine pickup handling; item definitions remain catalog-owned.</summary>
    public ImmutableArray<FieldPickupDefinition> MachineBallPickups(PewterMachineCatalog machines)
    {
        var result = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        foreach (var (mapId, label, itemSymbol) in new[] {
            (new MapId("map.route-12"), "Route12_EventScript_ItemTM48", "ITEM_TM48"),(new MapId("map.route-15"), "Route15_EventScript_ItemTM18", "ITEM_TM18") })
        {
            var events = RetainedEvents[mapId];
            var actors = events.GetProperty("object_events").EnumerateArray().Select((e, i) => (Event: e, LocalId: i + 1))
                .Where(p => p.Event.TryGetProperty("script", out var script) && script.GetString() == label).ToArray();
            Need(actors.Length == 1, "Original machine ball object is missing or ambiguous.");
            var actor = actors[0].Event;
            var block = ScriptInventory.GetProperty("blocks").EnumerateArray().Single(b => b.GetProperty("label").GetString() == label);
            var code = block.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("text").GetString()!.Trim()).Where(l => l.Length > 0).ToArray();
            Need(code.Length == 2 && code[0] == "finditem " + itemSymbol && code[1] == "end" &&
                actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_ITEM_BALL", "Original machine ball grant differs.");
            var itemId = new ItemId("pret:" + itemSymbol);
            var item = (PewterMachineRules.Definition(machines, itemId) ??
                throw new InvalidDataException("Required original machine is not admitted: " + itemSymbol)).Machine.Item;
            string flag = actor.GetProperty("flag").GetString()!;
            Need(Constants[itemSymbol] == item.SourceItemId && item.Pocket == "POCKET_TM_CASE", "Original machine catalog identity differs.");
            result.Add(new("original-tm-acquisition:" + flag, events.GetProperty("id").GetString()!, false, actors[0].LocalId,
                actor.GetProperty("x").GetInt32(), actor.GetProperty("y").GetInt32(), actor.GetProperty("elevation").GetInt32(),
                label, itemSymbol, item.SourceItemId, itemId, item.Name, item.Pocket, 1, flag, Constants[flag]));
        }
        return result.ToImmutable();
    }

    /// <summary>Deduplicate authenticated source files before invoking the existing shared compiler.</summary>
    public void AppendSources(List<PretSourceScriptFile> sources, List<string> texts,
        Dictionary<string, int> constants)
    {
        foreach (var file in Scripts)
        {
            var previous = sources.SingleOrDefault(s => s.Path == file.Path);
            if (previous is null) sources.Add(file);
            else Need(previous.Text == file.Text,
                "Original TM script source conflict: " + file.Path);
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        foreach (var (symbol, value) in Constants)
        {
            Need(!constants.TryGetValue(symbol, out var previous) || previous == value,
                "Original TM constant conflict: " + symbol);
            constants[symbol] = value;
        }
    }

    public static PretOriginalTmAcquisition Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"original-tm-acquisition",            "scripts/original-tm-acquisition.lock.json");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 3 , "Original TM catalog scope differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original TM acquisition maps; shared gameplay admission is separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative + "/runtime-map.json" ,
                "Original TM compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Original TM map symbol differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/regional-service-maps/npc/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(map.Id, map);
        }
        Need(maps.Keys.ToHashSet().SetEquals([ReturnRoomMapId, PsychicHouseMapId, MemorialMapId]), "Original TM map collection differs.");
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 268 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Original TM script closure differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            string path = entry.GetString()!, text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                string label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Missing original TM script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret,path,label,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
