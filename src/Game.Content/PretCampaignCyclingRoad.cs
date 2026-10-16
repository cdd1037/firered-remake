using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretCyclingRoadRegion CyclingRoadRegion { get; }
    public bool CyclingRoadEnabled => CyclingRoadRegion is not null;
    public ImmutableHashSet<MapId> CyclingRoadSceneInteriors { get; private set; } = [];

    // The scene remains armed when climbing stairs inside a gate. Derive that
    // interior closure from real source triggers/warps, never a list of floors.
    private void BindCyclingRoadSceneInteriors()
    {
        const string scene = "VAR_MAP_SCENE_ROUTE16";
        bool StartsScene(string script) => FindProgram(script)?.Instructions.Any(i =>
            i.Op == StoryOp.SetVar && i.Args.Length == 2 && i.Args[0] == scene && i.Args[1] == "1") == true;
        var indoor = Maps.Keys.Select(MapDefinition).Where(m => m.MapType == "MAP_TYPE_INDOOR")
            .ToDictionary(m => m.Id);
        var closure = indoor.Values.Where(m => m.CoordTriggers.Any(t => StartsScene(t.ScriptId)))
            .Select(m => m.Id).ToHashSet();
        var pending = new Queue<MapId>(closure);
        while (pending.TryDequeue(out var id))
            foreach (var warp in Maps[id].FieldWarps)
            {
                if (!indoor.ContainsKey(warp.DestinationMapId) ||
                    !Maps.TryGetValue(warp.DestinationMapId, out var target) ||
                    warp.DestinationWarpId < 0 || warp.DestinationWarpId >= target.FieldWarps.Length)
                    continue;
                if (closure.Add(warp.DestinationMapId)) pending.Enqueue(warp.DestinationMapId);
            }
        if (closure.Count == 0) throw new InvalidDataException("cycling_road_scene_entries_missing");
        CyclingRoadSceneInteriors = closure.ToImmutableHashSet();
    }

    public CyclingRoadTrainerCatalog CyclingRoadTrainers => CyclingRoadRegion.Trainers;
}
