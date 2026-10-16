using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Items;
using Game.Domain.Encounters;
using Game.Domain.Roaming;

namespace Game.Content;
/// <summary>The current-game content catalog. Unknown operations remain faults.</summary>
public sealed partial class PretCampaignCatalog : ICampaignCatalog
{
    private readonly PretStoryCompilation _scripts;
    private readonly ImmutableDictionary<MapId, FieldMapDefinition> _definitions;
    private readonly ImmutableDictionary<string, string> _text;
    public GamePokemonCatalog GamePokemon { get; }
    public GameOpponentPokemonCatalog OpponentPokemon { get; }
    public MachineCatalog Machines { get; }
    public string ContentVersion { get; }
    public CampaignPokemonContent PokemonContent { get; }
    public CampaignBattleContent BattleContent { get; }
    public OpeningItemsCatalog OpeningItems { get; }
    public QuestItemsCatalog QuestItems { get; }
    public InventoryCatalog Inventory { get; }
    public EscapeRopePolicy EscapeRoutes { get; }
    public PretViridianCenter ViridianCenter { get; }
    public PretOldManTutorialContent OldManTutorial { get; }
    public PretForestRegion ForestRegion { get; }
    public PretForestPokemon? ForestPokemon { get; }
    public PretPewterRegion PewterRegion { get; }
    public PretPokemonStorageSource PokemonStorageSource { get; }
    public PretEarlyPickups? EarlyPickups { get; }
    public ImmutableArray<FieldPickupDefinition> FieldPickups { get; }
    public ImmutableArray<FieldItemServiceDefinition> FieldItemServices { get; init; } = [];
    public PretEarlyRepelDocument EarlyRepel { get; }
    public PretTownMap TownMap { get; }
    public PretOakRating OakRating { get; }
    /// <summary>Immutable metadata shared by Oak, Route22 and presentation in this composition.</summary>
    public PretPokedexMenu PokedexMenu { get; }
    public PretRoute22Region Route22Region { get; }
    public PokemonPresentationPackage? Route22Pokemon { get; }
    public Route22TrainerCatalog? Route22Trainers { get; }
    public PretRoute3Region Route3Region { get; }
    public PokemonPresentationPackage? Route3Pokemon { get; }
    public Route3TrainerCatalog? Route3Trainers { get; }
    public PretMagikarpContract MagikarpContract { get; }
    public PretMtMoonRegion MtMoonRegion { get; }
    public PokemonPresentationPackage? MtMoonPokemon { get; }
    public MtMoonTrainerCatalog? MtMoonTrainers { get; }
    public PretCeruleanRegion CeruleanRegion { get; }
    public PokemonPresentationPackage? CeruleanPokemon { get; }
    public CeruleanTrainerCatalog? CeruleanTrainers { get; }
    public PretCeruleanCaveRegion CeruleanCaveRegion { get; }
    public WaterEncounterCatalog CeruleanCaveWaterEncounters { get; }
    public PretCityServices CityServices { get; }
    public PretGameCornerCoins GameCornerCoins { get; }
    public GameCornerPrizeCatalog GameCornerPrizes { get; }
    public FieldFlyCatalog FieldFly { get; }
    public FieldRockSmashCatalog? FieldRockSmash { get; private init; }
    public PretFlyMap FlyMapPresentation { get; }
    public PretRoute16FlyHouse Route16FlyHouse { get; }
    public PretOriginalTmAcquisition OriginalTmAcquisition { get; }
    public PretSchoolHouseInteriors SchoolHouseInteriors { get; }
    public PretTrainerFanClub TrainerFanClub { get; }
    public PretLostCaveRegion LostCaveRegion { get; }
    public LostCaveTrainerCatalog LostCaveTrainers { get; }
    public Route16FlyTrainerCatalog Route16FlyTrainers { get; }
    public PretOldRod? OldRod { get; }
    public IReadOnlyDictionary<MapId, OldRodEncounterTable> OldRodEncounters { get; }
    public ImmutableArray<MachineItem> CityServiceItems { get; }
    public PretVermilionGymRegion VermilionGymRegion { get; }
    public VermilionGymTrainerCatalog? VermilionGymTrainers { get; }
    public PretSsAnneRegion SsAnneRegion { get; }
    public PokemonPresentationPackage? SsAnnePokemon { get; }
    public SsAnneTrainerCatalog? SsAnneTrainers { get; }
    public PretVermilionRegion VermilionRegion { get; }
    public PokemonPresentationPackage? VermilionPokemon { get; }
    public VermilionTrainerCatalog? VermilionTrainers { get; }
    public PretBillRegion BillRegion { get; }
    public PokemonPresentationPackage? BillPokemon { get; }
    public PretBillItemsDocument? BillItems { get; }
    public BillTrainerCatalog? BillTrainers { get; }
    public PretMtMoonItemsDocument? MtMoonItems { get; }

    public bool IsValidPokedexSourceSpecies(int sourceSpeciesId) => PokedexMenu?.Entries.ContainsKey(sourceSpeciesId) == true;
    public int? SourceSpeciesHeightDecimeters(int sourceSpeciesId) => PokedexMenu?.Entries.GetValueOrDefault(sourceSpeciesId)?.HeightDecimeters;
    public string? SourceSpeciesName(int sourceSpeciesId) => PokedexMenu?.Entries.GetValueOrDefault(sourceSpeciesId)?.Name;

    public CampaignOakRatingProjection EvaluateOakRating(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)
    {
        var result = (OakRating ?? throw new InvalidDataException("oak_rating_not_enabled")).EvaluateSpeciesIds(seenSpecies, caughtSpecies, PokedexMenu!, new(HasPokedex: true));
        return new(result.Kanto.Seen, result.Kanto.Caught, result.Rating.TextSymbol, result.Rating.KantoComplete);
    }

    public PretEarlyShopDocument EarlyShopDocument { get; }
    public EarlyShopCatalog? EarlyShop => EarlyShopDocument?.Catalog;
    public OrdinaryMartCatalog OrdinaryMarts { get; }
    public ForestTrainerCatalog ForestTrainers { get; }
    public PewterTrainerCatalog? PewterTrainers { get; }
    public PretPewterPokemon? PewterPokemon { get; }
    public IReadOnlyDictionary<MapId, EarlyLandEncounterTable> LandEncounters { get; }
    public PretMartMetatiles? MartMetatiles { get; }
    public IReadOnlyDictionary<int, FieldMetatileDefinition> MetatileDefinitions { get; }
    public IReadOnlyDictionary<MapId, IFieldMap> Maps { get; }
    public IReadOnlyDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public PretStoryCompilation ScriptCompilation => _scripts;
    public PretViridianRegion? ViridianRegion { get; }
    public IReadOnlyDictionary<MapId, ConnectedFieldMap> ConnectedFieldMaps { get; }





    private static ImmutableArray<FieldPickupDefinition> SourceRegionPickupDefinitions(IReadOnlyDictionary<MapId,PretWorldMap> regionMaps,JsonElement items,JsonElement contracts,Func<string,MapId> resolveMap,string proofId,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        var definitions = items.GetProperty("declarations").EnumerateArray()
            .ToDictionary(e => e.GetProperty("itemId").GetString()!, StringComparer.Ordinal);
        EarlyShopItemDefinition Item(string symbol)
        {
            var item = shop.Items.SingleOrDefault(i => i.SourceItemSymbol == symbol)
                ?? throw new InvalidDataException(proofId+"_pickup_item_missing:" + symbol);
            if (!definitions.TryGetValue(symbol, out var source) || item.Id.Value != "pret:" + symbol ||
                !scripts.TryConstant(symbol, out var id) || item.SourceItemId != id ||
                item.Name != source.GetProperty("english").GetString() || item.Pocket != source.GetProperty("pocket").GetString() ||
                item.Price != source.GetProperty("price").GetInt32())
                throw new InvalidDataException(proofId+"_pickup_item_source:" + symbol);
            return item;
        }
        var pickups = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        foreach (var row in contracts.GetProperty("itemPickups").EnumerateArray())
        {
            var source = row.GetProperty("event");
            var map = regionMaps[resolveMap(row.GetProperty("map").GetString()!)];
            int local = row.GetProperty("localId").GetInt32(),quantity = row.GetProperty("quantity").GetInt32();
            string label = source.GetProperty("script").GetString()!,symbol = row.GetProperty("item").GetString()!;
            string flag = source.GetProperty("flag").GetString()!;
            var item = Item(symbol);
            var origin = row.GetProperty("sourceRef");
            if (quantity != 1 || !JsonElement.DeepEquals(source, map.Events.GetProperty("object_events")[local - 1]) ||
                !scripts.TryConstant(flag, out int flagId) || scripts.FindProgram(label) is not { } program ||
                program.Instructions.Any(i => i.Op == StoryOp.Unsupported || i.Source.Path != origin.GetProperty("path").GetString()) ||
                !program.Instructions.Any(i => i.Opcode == "setorcopyvar" && i.Args.SequenceEqual(new[] { "VAR_0x8000",symbol })) ||
                !program.Instructions.Any(i => i.Opcode == "setorcopyvar" && i.Args.SequenceEqual(new[] { "VAR_0x8001","1" })))
                throw new InvalidDataException(proofId+"_pickup_source:" + label);
            pickups.Add(new(proofId+":" + flag, map.SourceMapSymbol, false, local,
                source.GetProperty("x").GetInt32(), source.GetProperty("y").GetInt32(), source.GetProperty("elevation").GetInt32(),
                label, symbol, item.SourceItemId, item.Id, item.Name, item.Pocket, quantity, flag, flagId));
        }

        if (pickups.Select(p => p.SourceFlagId).Distinct().Count() != pickups.Count)
            throw new InvalidDataException(proofId+"_pickup_scope");
        return pickups.ToImmutable();
    }

    private FieldMapDefinition Definition(PretWorldMap map)
    {
        var e = map.Events;
        var root = map.SourceMapName + "_MapScripts";
        var header = _scripts.MapHeaders.GetValueOrDefault(root, []);
        string? Target(string kind) => header.FirstOrDefault(x => x.EventKind == kind)?.Target;
        var actors = e.GetProperty("object_events").EnumerateArray().Select((x, index) => CampaignSourceActor(map, x, index)).ToImmutableArray();
        var coords = e.GetProperty("coord_events").EnumerateArray().Select(x => new FieldCoordTrigger(x.GetProperty("x").GetInt32(), x.GetProperty("y").GetInt32(), x.GetProperty("elevation").GetInt32(), x.GetProperty("var").GetString()!, int.Parse(x.GetProperty("var_value").GetString()!, System.Globalization.CultureInfo.InvariantCulture), x.GetProperty("script").GetString()!)).ToImmutableArray();
        var signs = e.GetProperty("bg_events").EnumerateArray().Select(x => RegionalBackgroundTrigger(x, SupportsRegionalFlavor(map.Id))).ToImmutableArray();
        return new(map.Id, map.SourceMapSymbol, actors, coords, signs, new(Target("MAP_SCRIPT_ON_TRANSITION"), Target("MAP_SCRIPT_ON_WARP_INTO_MAP_TABLE"), Target("MAP_SCRIPT_ON_FRAME_TABLE"), Target("MAP_SCRIPT_ON_LOAD"), Target("MAP_SCRIPT_ON_RESUME"), Target("MAP_SCRIPT_ON_RETURN_TO_FIELD")), e.GetProperty("music").GetString()!, e.GetProperty("region_map_section").GetString()!)
        {
            MapType = e.GetProperty("map_type").GetString()!,RequiresFlash = e.GetProperty("requires_flash").GetBoolean()
        };
    }

    public FieldMetatileDefinition? MetatileDefinition(MapId map,int id)
    {
        if (SeafoamRegion.NewMaps.ContainsKey(map)) return SeafoamRegion.MetatileDefinition(map, id);
        if (TrainerFanClub.NewMaps.TryGetValue(map, out var goodRodMap) || LostCaveRegion.NewMaps.TryGetValue(map, out goodRodMap) || SchoolHouseInteriors.NewMaps.TryGetValue(map, out goodRodMap) || OriginalTmAcquisition.NewMaps.TryGetValue(map, out goodRodMap) || FourIslandDayCare.NewMaps.TryGetValue(map, out goodRodMap) || Route5DayCare.NewMaps.TryGetValue(map, out goodRodMap) || Route16FlyHouse.NewMaps.TryGetValue(map, out goodRodMap) || CapeBrinkTutor.NewMaps.TryGetValue(map, out goodRodMap) || SuperRodService.NewMaps.TryGetValue(map, out goodRodMap) || GoodRodService.NewMaps.TryGetValue(map, out goodRodMap) || MoveReminderHouse.NewMaps.TryGetValue(map, out goodRodMap) || MoveDeleterHouse.NewMaps.TryGetValue(map, out goodRodMap) || OriginalTutorRooms.NewMaps.TryGetValue(map, out goodRodMap) || CopycatHouse.NewMaps.TryGetValue(map, out goodRodMap))
        {
            var cell = goodRodMap.Cells.Concat(goodRodMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (NpcTradeContent.NewMaps.TryGetValue(map, out var npcTradeMap) || NpcTradeElyssa.NewMaps.TryGetValue(map, out npcTradeMap) || NpcTradeHaden.NewMaps.TryGetValue(map, out npcTradeMap))
        {
            var cell = npcTradeMap.Cells.Concat(npcTradeMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (FossilLabRegion.NewMaps.TryGetValue(map, out var fossilMap))
        {
            var cell = fossilMap.Cells.Concat(fossilMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (FightingDojoRegion.NewMaps.TryGetValue(map, out var dojoMap))
        {
            var cell = dojoMap.Cells.Concat(dojoMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (TrainerTowerRegion.Maps.ContainsKey(map))
        {
            var cell = TrainerTowerRegion.Layouts.Where(p => p.Key.Map == map).SelectMany(p => p.Value.Cells.Concat(p.Value.BorderCells)).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if(PowerPlantRegion.NewMaps.TryGetValue(map,out var plantMap))
        {
            var cell=plantMap.Cells.Concat(plantMap.BorderCells).FirstOrDefault(c=>c.MetatileId==id);
            return cell is null?null:new(id,cell.BehaviorId,cell.Terrain,cell.EncounterType,cell.LayerType);
        }
        if (EventIslandRegion.NewMaps.TryGetValue(map, out var eventMap))
        {
            var cell = eventMap.Cells.Concat(eventMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (SevenIslandRegion.NewMaps.ContainsKey(map)) return SevenIslandRegion.MetatileDefinition(map, id);
        if (CeruleanCaveRegion.NewMaps.TryGetValue(map, out var cave))
        {
            var cell = cave.Cells.Concat(cave.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (FiveIslandRegion?.SourceMaps.ContainsKey(map) == true)
            return FiveIslandRegion.MetatileDefinition(map, id);
        if (SixIslandRegion?.SourceMaps.ContainsKey(map) == true)
            return SixIslandRegion.MetatileDefinition(map, id);
        if (FourIslandRegion?.SourceMaps.ContainsKey(map) == true)
            return FourIslandRegion.MetatileDefinition(map, id);
        if (SeviiRubyRegion?.SourceMaps.ContainsKey(map) == true)
            return SeviiRubyRegion.MetatileDefinition(map, id);
        if (PokemonLeagueRegion?.SourceMaps.ContainsKey(map) == true)
            return PokemonLeagueRegion.MetatileDefinition(map, id);
        if (VictoryRoadRegion?.SourceMaps.ContainsKey(map) == true)
            return VictoryRoadRegion.MetatileDefinition(map, id);
        if (ViridianGymRegion?.NewMaps.ContainsKey(map) == true)
            return ViridianGymRegion.MetatileDefinition(map, id);
        if (FirstSeviiRegion?.NewMaps.ContainsKey(map) == true)
            return FirstSeviiRegion.MetatileDefinition(map, id);
        if (CinnabarRegion?.NewMaps.ContainsKey(map) == true)
            return CinnabarRegion.MetatileDefinition(map, id);
        if (SilphRegion?.NewMaps.ContainsKey(map) == true)
            return SilphRegion.MetatileDefinition(map, id);
        if (VermilionGymRegion is not null && map.Value == "map.vermilion-city-gym")
            return VermilionGymRegion.BeamDefinitions.TryGetValue(id, out var tile) ? new(id, tile.Behavior, tile.Terrain, tile.EncounterType, tile.LayerType) : null;
        if (RocketHideoutRegion?.NewMaps.ContainsKey(map) == true)
            return RocketHideoutMetatileDefinition(map, id);
        return MetatileDefinitions?.GetValueOrDefault(id);
    }

    public int? TerrainAt(MapId map,int x,int y) => SourceMaps.TryGetValue(map, out var source) ? source.GetSourceCell(x, y)?.Terrain : null;
    public bool AllowsCycling(MapId map) => SourceMaps.TryGetValue(map, out var source) && source.Events.GetProperty("allow_cycling").GetBoolean();
    public FieldMapDefinition MapDefinition(MapId id) => _definitions[id];
    public MapId MapFromSourceSymbol(string symbol) => SourceMaps.Values.First().ResolveMapSymbol(symbol);
    public ImmutableArray<FieldProgramCondition> Conditions(string table) => _scripts.MapConditions.TryGetValue(table, out var list) ? list.Select(x => new FieldProgramCondition(x.Variable, ConditionValue(table, x.Expected), x.Target)).ToImmutableArray() : [];
    private int ConditionValue(string table,string token)
    {
        if (int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
            return value;
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value))
            return value;
        if (_scripts.TryConstant(token, out value))
            return value;
        throw ContentJson.Error("map_condition_value", table, "Unknown map condition value: " + token);
    }

    public ImmutableArray<string> Movement(string program) => _scripts.Movements.TryGetValue(program, out var list) ? list.Select(x => x.Opcode).ToImmutableArray() : throw new InvalidDataException("Missing movement program: " + program);
    public StoryProgram? FindProgram(string id) => _scripts.FindProgram(id);
    public StoryMenuDefinition? FindMenu(string id) => _scripts.FindMenu(id);
    public StoryMenuDefinition? FindListMenu(int sourceId) => _scripts.FindListMenu(sourceId);
    public bool TryConstant(string symbol,out int value) => _scripts.TryConstant(symbol, out value);
    public ImmutableArray<string> TextPages(string symbol)
    {
        if (!_text.TryGetValue(symbol, out var raw))
            throw new InvalidDataException("Missing source text: " + symbol);
        if (Regex.IsMatch(raw, @"\\(?![npl])"))
            throw new InvalidDataException("Unsupported text escape: " + symbol);
        var pages = ImmutableArray.CreateBuilder<string>();
        var lines = new List<string>();
        var line = new StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '$')
            {
                lines.Add(line.ToString());
                pages.Add(string.Join("\n", lines));
                break;
            }

            if (c == '\\')
            {
                char control = raw[++i];
                lines.Add(line.ToString());
                line.Clear();
                if (control == 'p')
                {
                    pages.Add(string.Join("\n", lines));
                    lines.Clear();
                }
                else if (control == 'l')
                {
                    pages.Add(string.Join("\n", lines));
                    var last = lines[^1];
                    lines.Clear();
                    lines.Add(last);
                }
            }
            else
                line.Append(c);
        }

        if (pages.Count == 0)
            throw new InvalidDataException("Missing text terminator: " + symbol);
        return pages.ToImmutable();
    }
}
