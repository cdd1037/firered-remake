using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;
public sealed partial class PretViridianGymRegion
{
    private static SilphSourcePackage ViridianGymSource(string root) => new(root, "viridian-gym-region", Profile,
        "content/sources/viridian-gym-region.lock.json", LockSha256, 32508,
        ManifestSha256, 4054, 102, 21);

    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        var map = maps[new("map.viridian-city-gym")]; var events = map.Events;
        var contract = source.Document("contracts.json"); var city = SilphSourcePackage.Json(source.Raw["data/maps/ViridianCity/map.json"]);
        Need(map.Width == 20 && map.Height == 24 && map.BorderWidth == 2 && map.BorderHeight == 2 &&
            events.GetProperty("music").GetString() == "MUS_GYM" && events.GetProperty("battle_scene").GetString() == "MAP_BATTLE_SCENE_GYM" &&
            events.GetProperty("map_type").GetString() == "MAP_TYPE_INDOOR" && !events.GetProperty("requires_flash").GetBoolean() &&
            !events.GetProperty("allow_cycling").GetBoolean() && !events.GetProperty("allow_running").GetBoolean() &&
            !events.GetProperty("allow_escaping").GetBoolean() && events.GetProperty("connections").ValueKind == JsonValueKind.Null &&
            events.GetProperty("coord_events").GetArrayLength() == 0, "Viridian Gym header/topology differs.");
        Need(events.GetProperty("object_events").GetArrayLength() == 10 && events.GetProperty("bg_events").GetArrayLength() == 3 &&
            contract.GetProperty("itemPickups").GetArrayLength() == 0 && source.Document("npc-source.json").GetProperty("graphics").GetArrayLength() == 6,
            "Viridian Gym event/sprite scope differs.");
        var gate = contract.GetProperty("entrance"); var cityWarp = city.GetProperty("warp_events")[2];
        Need(gate.GetProperty("cityWarpIndex").GetInt32() == 2 && gate.GetProperty("gymWarpIndex").GetInt32() == 1 &&
            JsonElement.DeepEquals(gate.GetProperty("cityWarp"), cityWarp) && cityWarp.GetProperty("x").GetInt32() == 36 && cityWarp.GetProperty("y").GetInt32() == 10 &&
            cityWarp.GetProperty("dest_map").GetString() == "MAP_VIRIDIAN_CITY_GYM" && cityWarp.GetProperty("dest_warp_id").GetString() == "1" &&
            JsonElement.DeepEquals(gate.GetProperty("gymWarp"), events.GetProperty("warp_events")[1]), "Viridian city/gym entrance differs.");
        Need(gate.GetProperty("requiredBadges").EnumerateArray().Select(v => v.GetString()).SequenceEqual(Enumerable.Range(2, 6).Select(i => $"FLAG_BADGE{i:00}_GET")) &&
            gate.GetProperty("latched").GetBoolean() && gate.GetProperty("doorScene").GetString() == "VAR_MAP_SCENE_VIRIDIAN_CITY_GYM_DOOR",
            "Viridian Gym door must retain the source badges 02-07 latch.");
        var trigger = gate.GetProperty("lockedTrigger");
        Need(city.GetProperty("coord_events").EnumerateArray().Count(e => JsonElement.DeepEquals(e, trigger)) == 1 &&
            trigger.GetProperty("x").GetInt32() == 36 && trigger.GetProperty("y").GetInt32() == 11 && trigger.GetProperty("elevation").GetInt32() == 3 &&
            trigger.GetProperty("var").GetString() == "VAR_MAP_SCENE_VIRIDIAN_CITY_GYM_DOOR" && trigger.GetProperty("var_value").GetString() == "0",
            "Viridian Gym locked trigger differs.");
        var warps = events.GetProperty("warp_events").EnumerateArray().ToArray();
        Need(warps.Length == 3 && warps.Select(w => w.GetProperty("x").GetInt32()).SequenceEqual(new[] { 16, 17, 18 }) &&
            warps.All(w => w.GetProperty("y").GetInt32() == 22 && w.GetProperty("dest_map").GetString() == "MAP_VIRIDIAN_CITY" && w.GetProperty("dest_warp_id").GetString() == "2") &&
            map.SourceBehaviorAt(17, 22) == 0x65 && map.SourceBehaviorAt(16, 22) != 0x65 && map.SourceBehaviorAt(18, 22) != 0x65,
            "Three source exit records must not become three arrow-warp tiles.");
        foreach (var behavior in new[] { 0x54, 0x55, 0x56, 0x57 }) Need(map.Cells.Count(c => c.BehaviorId == behavior) == 3, "Viridian Gym arrow count differs.");
        Need(map.Cells.Count(c => c.BehaviorId == 0x58) == 12 && map.Cells.Count(c => c.BehaviorId == 0) == 453, "Viridian Gym stop/floor counts differ.");
        var hidden = contract.GetProperty("hiddenItems"); var h = hidden[0]; var hiddenEvent = h.GetProperty("event");
        Need(hidden.GetArrayLength() == 1 && JsonElement.DeepEquals(hiddenEvent, events.GetProperty("bg_events")[2]) &&
            hiddenEvent.GetProperty("item").GetString() == "ITEM_MACHO_BRACE" && hiddenEvent.GetProperty("flag").GetString() == "FLAG_HIDDEN_ITEM_VIRIDIAN_CITY_GYM_MACHO_BRACE" &&
            hiddenEvent.GetProperty("x").GetInt32() == 2 && hiddenEvent.GetProperty("y").GetInt32() == 2 && hiddenEvent.GetProperty("elevation").GetInt32() == 3 &&
            hiddenEvent.GetProperty("quantity").GetInt32() == 1 && hiddenEvent.GetProperty("underfoot").GetBoolean() &&
            h.GetProperty("acquisition").GetString() == "ItemfinderUnderfootOnly" && !h.GetProperty("ordinaryInteractionAllowed").GetBoolean() && !h.GetProperty("runtimeSupported").GetBoolean(),
            "Macho Brace must remain an unsupported underfoot Itemfinder reward.");
        var occurrences = contract.GetProperty("trainerOccurrences").EnumerateArray().ToArray();
        Need(occurrences.Length == 9 && occurrences.Select(o => o.GetProperty("sourceTrainerId").GetInt32()).ToHashSet().SetEquals(new[] { 296, 297, 322, 323, 324, 350, 392, 400, 401 }),
            "Viridian Gym trainer IDs differ.");
        foreach (var occurrence in occurrences)
        {
            var id = occurrence.GetProperty("localId").GetInt32(); var actor = events.GetProperty("object_events")[id - 1];
            Need(JsonElement.DeepEquals(actor, occurrence.GetProperty("event")) && actor.GetProperty("script").GetString() == occurrence.GetProperty("script").GetString() &&
                constants[occurrence.GetProperty("trainer").GetString()!] == occurrence.GetProperty("sourceTrainerId").GetInt32() && !occurrence.GetProperty("admitted").GetBoolean(),
                "Trainer occurrence must preserve its actor and remain unadmitted.");
            bool giovanni = occurrence.GetProperty("sourceTrainerId").GetInt32() == 350;
            Need(occurrence.GetProperty("mode").GetInt32() == (giovanni ? 1 : 0) && occurrence.GetProperty("noMusic").GetBoolean() == giovanni &&
                occurrence.GetProperty("callback").GetString() == (giovanni ? "ViridianCity_Gym_EventScript_DefeatedGiovanni" : null), "Trainer source mode/callback differs.");
        }
        Need(contract.GetProperty("autoDefeatedTrainers").EnumerateArray().Select(v => v.GetString()).ToHashSet().SetEquals(
            occurrences.Where(o => o.GetProperty("sourceTrainerId").GetInt32() != 350).Select(o => o.GetProperty("trainer").GetString())), "Skipped trainer flags differ.");
        Need(constants["LOCALID_VIRIDIAN_GIOVANNI"] == 8 && constants["ITEM_TM26"] == 314 && constants["ITEM_MACHO_BRACE"] == 181,
            "Gym local/item source constants differ.");
        var item = contract.GetProperty("itemReferences").EnumerateArray().Single(i => i.GetProperty("item").GetString() == "ITEM_TM26");
        Need(item.GetProperty("record").GetProperty("moveId").GetString() == "Earthquake" && item.GetProperty("record").GetProperty("pocket").GetString() == "POCKET_TM_CASE",
            "TM26 original item reference differs.");
        foreach (var block in source.Document("scripts.json").GetProperty("blocks").EnumerateArray())
        {
            var reference = block.GetProperty("sourceRef"); var path = reference.GetProperty("path").GetString()!;
            Need(source.Files.Any(f => f.Path == path && f.Sha256 == reference.GetProperty("sha256").GetString()), "Script source hash differs.");
            var original = Encoding.UTF8.GetString(source.Raw[path]).Split('\n');
            Need(original[reference.GetProperty("line").GetInt32() - 1].TrimEnd(':', '\r') == block.GetProperty("label").GetString(), "Script label line differs.");
            foreach (var line in block.GetProperty("lines").EnumerateArray()) Need(original[line.GetProperty("line").GetInt32() - 1].TrimEnd('\r') == line.GetProperty("text").GetString(), "Script transcript differs.");
        }
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "Viridian Gym source map symbol differs.");
    }
}
