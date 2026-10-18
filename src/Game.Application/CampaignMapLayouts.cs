using System.Collections;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Events;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private IReadOnlyDictionary<MapId, ConnectedFieldMap> MapLayoutConnectedMaps(CampaignState state)
    {
        var maps = TrainerTowerConnectedMaps(state);
        if (state.MapLayout is not {} layout) return maps;
        var id = state.World.Player.MapId;
        return ReplaceMap(maps, id,
            _content.ConnectedMapLayout(id, layout) ?? throw new InvalidDataException("map_layout_missing"));
    }
    private IReadOnlyDictionary<MapId, IFieldMap> MapLayoutRawMaps(CampaignState state)
    {
        var maps = TrainerTowerMovementMaps(state);
        if (state.MapLayout is not {} layout) return maps;
        var id = state.World.Player.MapId;
        return ReplaceMap(maps, id,
            _content.MapLayout(id, layout) ?? throw new InvalidDataException("map_layout_missing"));
    }

    private static IReadOnlyDictionary<MapId, T> ReplaceMap<T>(IReadOnlyDictionary<MapId, T> maps,
        MapId id, T replacement) => new CampaignMapView<T>(maps, (key, value) => key == id ? replacement : value);

    // A command-local view preserves adapter order without rebuilding every region.
    // Rules normally request only the current map and a connected/warp destination.
    // Full enumeration remains available, and each requested value is composed once.
    private sealed class CampaignMapView<T>(IReadOnlyDictionary<MapId, T> maps, Func<MapId, T, T> project)
        : IReadOnlyDictionary<MapId, T>
    {
        private readonly Dictionary<MapId, T> _resolved = [];
        public int Count => maps.Count;
        public IEnumerable<MapId> Keys => maps.Keys;
        public IEnumerable<T> Values => Keys.Select(key => this[key]);
        public T this[MapId key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        public bool ContainsKey(MapId key) => maps.ContainsKey(key);
        public bool TryGetValue(MapId key, out T value)
        {
            if (_resolved.TryGetValue(key, out value!)) return true;
            if (!maps.TryGetValue(key, out var original)) { value = default!; return false; }
            value = project(key, original);
            _resolved.Add(key, value);
            return true;
        }
        public IEnumerator<KeyValuePair<MapId, T>> GetEnumerator()
        {
            foreach (var key in Keys) yield return new(key, this[key]);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private IFieldMap MapLayoutForArrival(CampaignState state, IFieldMap destination)
    {
        if (!_content.HasMapLayouts(destination.Id)) return destination;
        var arriving = state with { World = state.World with { Player = state.World.Player with { MapId = destination.Id } } };
        var layout = RestoreMapLayout(arriving);
        return layout is null ? destination : _content.MapLayout(destination.Id, layout)!;
    }

    private bool TryApplyMapLayoutEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode != "setmaplayoutindex") return false;
        if (effect.Args.Length != 1 || _content.MapLayout(state.World.Player.MapId, effect.Args[0]) is null)
            throw new InvalidDataException("map_layout_binding_invalid");
        next = state with { MapLayout = effect.Args[0] };
        return true;
    }

    // Replays only the generated entry program in an isolated interpreter. It never
    // changes saved variables, flags, actors, RNG or rewards, and runs only at restore/save validation or an actual incoming warp.
    private string? RestoreMapLayout(CampaignState state)
    {
        var map = state.World.Player.MapId;
        if (!_content.HasMapLayouts(map)) return null;
        var entry = _content.MapDefinition(map).Programs.OnTransition
            ?? throw new InvalidDataException("map_layout_transition_missing");
        var script = StoryState.Empty with { Variables = state.Script.Variables, Flags = state.Script.Flags };
        var transition = StoryRules.Start(script, entry, _content);
        string? layout = null;
        for (int n = 0; n < 128; n++)
        {
            if (transition.Status != TransitionStatus.Accepted)
                throw new InvalidDataException("map_layout_restore_script:" + transition.Diagnostic?.Code);
            script = transition.State;
            foreach (var effect in transition.Effects)
            {
                var args = effect.Args;
                switch (effect.Opcode)
                {
                    case "setmaplayoutindex":
                        if (args.Length != 1 || _content.MapLayout(map, args[0]) is null)
                            throw new InvalidDataException("map_layout_restore_binding");
                        layout = args[0]; break;
                    case "specialvar" when args.Length == 2 && args[1] == "IsNationalPokedexEnabled":
                        script = script with { Variables = script.Variables.SetItem(args[0], NationalDexEnabledIn(state) ? 1 : 0) }; break;
                    case "addvar" when args.Length == 2 && int.TryParse(args[1], out var amount):
                        script = script with { Variables = script.Variables.SetItem(args[0], unchecked((ushort)(script.Variables.GetValueOrDefault(args[0]) + amount))) }; break;
                    case "setobjectxyperm": case "setobjectmovementtype":
                        // Actor initialization is already persisted and independently validated.
                        break;
                    default: throw new InvalidDataException("map_layout_restore_effect:" + effect.Opcode);
                }
            }
            if (script.Completed) return layout;
            if (script.Wait is not { Kind: StoryWaitKind.NativeEffect } wait)
                throw new InvalidDataException("map_layout_restore_wait");
            transition = StoryRules.Continue(script, wait.CorrelationId, _content);
        }
        throw new InvalidDataException("map_layout_restore_budget");
    }
}
