using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Decode source event/call-graph bindings for the shared inventory script adapter.</summary>
internal static class PretFieldItemServices
{
    public static ImmutableArray<FieldItemServiceDefinition> Decode(JsonElement bindings,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, IReadOnlyDictionary<string, int> constants)
    {
        var results = ImmutableArray.CreateBuilder<FieldItemServiceDefinition>();
        foreach (var binding in bindings.EnumerateArray())
        {
            var mapId = new MapId(binding.GetProperty("mapId").GetString()!);
            string id = binding.GetProperty("id").GetString()!, script = binding.GetProperty("scriptId").GetString()!;
            string group = binding.GetProperty("eventGroup").GetString()!;
            int index = binding.GetProperty("eventIndex").GetInt32();
            Need(maps.TryGetValue(mapId, out var map) && group is "object_events" or "bg_events", "Field item service map/group differs.");
            var events = map!.Events.GetProperty(group);
            Need(index >= 0 && index < events.GetArrayLength(), "Field item service event index differs.");
            var e = events[index];
            Need(JsonElement.DeepEquals(e, binding.GetProperty("sourceEvent")) && e.GetProperty("script").GetString() == script,
                "Field item service event binding differs.");
            int? Optional(string name) => binding.GetProperty(name).ValueKind == JsonValueKind.Null ? null : binding.GetProperty(name).GetInt32();
            int? local = Optional("sourceLocalId"), x = Optional("backgroundX"), y = Optional("backgroundY");
            Need(group == "object_events" ? local == index + 1 && x is null && y is null :
                local is null && x == e.GetProperty("x").GetInt32() && y == e.GetProperty("y").GetInt32(), "Field item service interaction position differs.");
            var programs = binding.GetProperty("programIds").EnumerateArray().Select(p => p.GetString()!).ToImmutableArray();
            var items = binding.GetProperty("itemSymbols").EnumerateArray().Select(p => p.GetString()!).ToImmutableArray();
            Need(programs.Contains(script) && programs.Distinct(StringComparer.Ordinal).Count() == programs.Length &&
                !items.IsEmpty && items.Distinct(StringComparer.Ordinal).Count() == items.Length && items.All(constants.ContainsKey),
                "Field item service call graph or item scope differs.");
            results.Add(new(id, mapId, script, local, x, y, programs, items));
        }
        Need(results.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() == results.Count &&
            results.Select(r => (r.Map, r.ScriptId)).Distinct().Count() == results.Count,
            "Field item service identity collection differs.");
        return results.ToImmutable();
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
