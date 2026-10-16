using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{

    public PretRegionalServiceMaps RegionalServiceMaps { get; }
    public PretRegionalGiftContract RegionalGiftContract { get; }
    public PretEeveeArt? EeveeArt { get; }
    /// <summary>Kept outside CityServiceItems: Tea uses its own effect handler.</summary>
    public MachineItem? TeaItem => RegionalGiftContract?.TeaItem;
    public static ImmutableArray<MapId> RegionalServiceMapIds { get; } =
    [
        new("map.celadon-city-condominiums-1f"), new("map.celadon-city-condominiums-2f"),
        new("map.celadon-city-condominiums-3f"), new("map.celadon-city-condominiums-roof"),
        new("map.celadon-city-condominiums-roof-room")
    ];



    private static void ValidateRegionalServiceComposition(PretRegionalServiceMaps region,PretRegionalGiftContract gift,ImmutableDictionary<MapId, PretWorldMap> maps,CampaignPokemonContent pokemon,CampaignPokemonContent predecessorPokemon,PretStoryCompilation scripts,ImmutableDictionary<string, string>.Builder texts)
    {
        if (!RegionalServiceMapIds.All(maps.ContainsKey) )
            throw new InvalidDataException("regional_services_composition_scope");
        region.ValidateReusedMaps(maps);
        foreach (var id in RegionalServiceMapIds)
        {
            var map = maps[id]; var sourceWarps = map.Events.GetProperty("warp_events").EnumerateArray().ToArray();
            if (map.FieldWarps.Length != sourceWarps.Length)
                throw new InvalidDataException("regional_services_map_identity: " + id.Value);
            for (int i = 0; i < sourceWarps.Length; i++)
            {
                var source = sourceWarps[i]; var warp = map.FieldWarps[i];
                if (warp.DestinationMapId != PretRegionalServiceMaps.MapIdFromSourceSymbol(source.GetProperty("dest_map").GetString()!) ||
                    warp.DestinationWarpId != int.Parse(source.GetProperty("dest_warp_id").GetString()!, CultureInfo.InvariantCulture) ||
                    !maps.TryGetValue(warp.DestinationMapId, out var target) || warp.DestinationWarpId < 0 ||
                    warp.DestinationWarpId >= target.Events.GetProperty("warp_events").GetArrayLength())
                    throw new InvalidDataException("regional_services_source_warp: " + id.Value);
            }
        }
        foreach (string label in gift.RequiredCommonScripts)
            if (scripts.FindProgram(label) is null) throw new InvalidDataException("regional_services_common_script_missing: " + label);
        foreach (string label in gift.RequiredCommonTexts)
            if (!texts.ContainsKey(label)) throw new InvalidDataException("regional_services_common_text_missing: " + label);

        var pending = new Queue<string>(new[] { gift.Document.GetProperty("tea").GetProperty("entryScript").GetString()!,gift.Document.GetProperty("eevee").GetProperty("entryScript").GetString()! });
        foreach (var id in region.ReusedMapIds)
            foreach (var trigger in maps[id].Events.GetProperty("coord_events").EnumerateArray()) pending.Enqueue(trigger.GetProperty("script").GetString()!);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var label))
        {
            if (!seen.Add(label)) continue;
            var program = scripts.FindProgram(label) ?? throw new InvalidDataException("regional_services_program_missing: " + label);
            foreach (var instruction in program.Instructions)
            {
                if (instruction.Op == StoryOp.Unsupported) throw new InvalidDataException("regional_services_opcode_unsupported: " + label);
                if (instruction.Op is StoryOp.Dialog or StoryOp.Message && !texts.ContainsKey(instruction.Args[0]))
                    throw new InvalidDataException("regional_services_dialogue_missing: " + instruction.Args[0]);
                if (instruction.Op is StoryOp.Goto or StoryOp.Call or StoryOp.ConditionalGoto or StoryOp.ConditionalCall or
                    StoryOp.FlagGoto or StoryOp.FlagCall or StoryOp.QuestLogJump) pending.Enqueue(instruction.Args[^1]);
                if (instruction.Op == StoryOp.ApplyMovement && !scripts.Movements.ContainsKey(instruction.Args[1]))
                    throw new InvalidDataException("regional_services_movement_missing: " + instruction.Args[1]);
            }
        }
    }
}
