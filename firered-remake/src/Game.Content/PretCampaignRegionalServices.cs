using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
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
    /// <summary>Tea and Eevee successor; predecessor profiles never read this package.</summary>
    public static PretCampaignCatalog LoadRegionalServicesProfile(string root) => Load(root,
        includeStarterEvents:true, includeFirstBattle:true, includeOpeningItems:true,
        includeViridianRegion:true, includeParcelQuest:true, includeCapture:true, includeViridianCenter:true,
        includeOldManTutorial:true, includePartyTraining:true, includeForest:true, includeForestTrainers:true,
        includePewter:true, includeEarlyShop:true, includeEarlyStarter:true, includeEarlyParty:true,
        includePokemonStorage:true, includeFieldPickups:true, includeRepel:true, includeTownMap:true,
        includeOakRating:true, includeRoute22:true, useCompactMapData:true, includeRoute3:true,
        includeMagikarp:true, includeMtMoon:true, includeCerulean:true, includeBill:true, includeVermilion:true,
        includeSsAnne:true, includeVermilionGym:true, includeCityServices:true, includeRoute11:true,
        includeRockTunnel:true, includeCeladonApproach:true, includeCeladonGym:true,
        includeRocketHideout:true, includePokemonTower:true, includeSnorlax:true,
        includeRegionalFlavor:true, includeRegionalServices:true);

    public PretRegionalServiceMaps? RegionalServiceMaps { get; }
    public PretRegionalGiftContract? RegionalGiftContract { get; }
    public PretEeveePokemon? EeveePokemon { get; }
    public PretEeveeArt? EeveeArt { get; }
    public bool RegionalServicesEnabled => RegionalGiftContract is not null && RegionalServiceMaps is not null;
    /// <summary>Kept outside CityServiceItems: Tea uses its own authenticated effect handler.</summary>
    public PewterMachineItem? TeaItem => RegionalGiftContract?.TeaItem;
    public ImmutableArray<MtMoonDeferredEvolution> EeveeDeferredEvolutions => EeveePokemon?.DeferredEvolutions ?? [];
    public static ImmutableArray<MapId> RegionalServiceMapIds { get; } =
    [
        new("map.celadon-city-condominiums-1f"), new("map.celadon-city-condominiums-2f"),
        new("map.celadon-city-condominiums-3f"), new("map.celadon-city-condominiums-roof"),
        new("map.celadon-city-condominiums-roof-room")
    ];

    private static ConnectedFieldMap RegionalServiceConnectedMap(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        var connections = source.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            source.EnumerateArray().Select(c => new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Direction.North, "down" => Direction.South,
                "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("regional_services_connection_direction")
            }, c.GetProperty("offset").GetInt32(), PretRegionalServiceMaps.MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), connections);
    }

    private static void MergeRegionalServiceConstants(Dictionary<string, int> constants, IReadOnlyDictionary<string, int> added)
    {
        foreach (var (symbol, value) in added)
        {
            if (constants.TryGetValue(symbol, out var prior) && prior != value)
                throw new InvalidDataException("regional_services_constant_conflict: " + symbol);
            constants[symbol] = value;
        }
    }

    private static void AppendRegionalServiceSources(string root, PretRegionalServiceMaps region,
        PretRegionalGiftContract gift, ImmutableDictionary<MapId, PretWorldMap> maps,
        List<PretSourceScriptFile> sources, List<string> texts, Dictionary<string, int> constants, List<string> hashes)
    {
        // One original path always has one authenticated hash, even when several packages retain it.
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        void Identity(string path, string sha)
        {
            if (identities.TryGetValue(path, out var prior) && prior != sha)
                throw new InvalidDataException("regional_services_source_conflict: " + path);
            identities[path] = sha;
        }
        foreach (var map in maps.Values)
            foreach (var file in map.Metadata.Files) Identity(file.Path, file.Sha256);
        foreach (var source in sources) Identity(source.Path, source.SourceRef.Sha256!);
        foreach (var file in region.SourceFiles) Identity(file.Path, file.Sha256);
        foreach (var file in gift.SourceFiles) Identity(file.Path, file.Sha256);

        var paths = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        void Script(PretSourceScriptFile source)
        {
            if (paths.TryGetValue(source.Path, out var prior))
            {
                if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                    throw new InvalidDataException("regional_services_script_conflict: " + source.Path);
                return;
            }
            paths.Add(source.Path, source); sources.Add(source);
            if (!texts.Contains(source.Text, StringComparer.Ordinal)) texts.Add(source.Text);
        }
        foreach (var source in region.Scripts) Script(source);
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);

        // Most contract inputs are already included by the map or common-script packages.
        // Authenticate the path before reusing it, and import only actual event/text sources.
        foreach (var file in gift.SourceFiles.Where(f => f.Path == "data/event_scripts.s" ||
            f.Path.StartsWith("data/scripts/", StringComparison.Ordinal) || f.Path.StartsWith("data/text/", StringComparison.Ordinal) ||
            f.Path.StartsWith("data/maps/", StringComparison.Ordinal) && f.Path.EndsWith(".inc", StringComparison.Ordinal)))
        {
            if (paths.TryGetValue(file.Path, out var prior))
            {
                if (prior.SourceRef.Sha256 != file.Sha256)
                    throw new InvalidDataException("regional_services_gift_source_conflict: " + file.Path);
                continue;
            }
            var bytes = File.ReadAllBytes(Path.Combine(root, "content/raw/regional-tea-eevee", file.Path));
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256)
                throw new InvalidDataException("regional_services_gift_source_hash: " + file.Path);
            string text = Encoding.UTF8.GetString(bytes);
            if (file.Path.StartsWith("data/text/", StringComparison.Ordinal) || file.Path.EndsWith("/text.inc", StringComparison.Ordinal))
            {
                if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
            }
            else Script(new(file.Path, text, new(SourceKind.Pret, file.Path, "regional-services", "pret/pokefirered", PretWorldMap.SourceCommit, file.Sha256)));
        }
        MergeRegionalServiceConstants(constants, region.Constants);
        MergeRegionalServiceConstants(constants, gift.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.AddRange(gift.AdditionalSourceHashes);
        hashes.Add("regional-services-application:fr-regional-services-v1");
    }

    private static void ValidateRegionalServiceComposition(PretRegionalServiceMaps region, PretRegionalGiftContract gift,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> predecessorMaps,
        CampaignPokemonContent pokemon, CampaignPokemonContent predecessorPokemon,
        PretStoryCompilation scripts, ImmutableDictionary<string, string>.Builder texts)
    {
        if (maps.Count != 136 || pokemon.Catalog.Species.Count != 103 || pokemon.Catalog.Moves.Count != 241 ||
            !maps.Keys.Except(predecessorMaps.Keys).ToHashSet().SetEquals(RegionalServiceMapIds) ||
            predecessorMaps.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            predecessorPokemon.Catalog.Species.Any(p => !ReferenceEquals(p.Value, pokemon.Catalog.Species[p.Key])) ||
            predecessorPokemon.Catalog.Moves.Any(p => !ReferenceEquals(p.Value, pokemon.Catalog.Moves[p.Key])))
            throw new InvalidDataException("regional_services_composition_scope");
        region.ValidateReusedMaps(maps);
        foreach (var id in RegionalServiceMapIds)
        {
            var map = maps[id]; var sourceWarps = map.Events.GetProperty("warp_events").EnumerateArray().ToArray();
            if (!ReferenceEquals(map, region.NewMaps[id]) || map.FieldWarps.Length != sourceWarps.Length)
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

        var pending = new Queue<string>(new[] { gift.Document.GetProperty("tea").GetProperty("entryScript").GetString()!,
            gift.Document.GetProperty("eevee").GetProperty("entryScript").GetString()! });
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
