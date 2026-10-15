using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Original FireRed NPC trades and the one new Lounge map. All nine original exchanges are admitted.</summary>
public sealed class PretNpcTrades
{
    public const string ResourceId = "firered-npc-trades-v1";
    public static MapId LoungeMapId { get; } = new("map.cinnabar-island-pokemon-lab-lounge");
    public NpcTradePokemonCatalog Trades { get; }
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretNpcTrades(SilphSourcePackage source, NpcTradePokemonCatalog trades,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        Texts = source.Document("presentation-text.json").EnumerateArray().ToImmutableDictionary(
            r => r.GetProperty("symbol").GetString()!, r => r.GetProperty("text").GetString()!, StringComparer.Ordinal);
        Trades = trades; NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE" => LoungeMapId,
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" => new("map.cinnabar-island-pokemon-lab-entrance"),
        _ => throw new InvalidDataException("Unknown original Lounge warp destination: " + symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == LoungeMapId && map.Width == 15 && map.Height == 11 &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "NPC trade Lounge indoor geometry differs.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }
    public static PretNpcTrades Load(string projectRoot, PokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        var source = new SilphSourcePackage(projectRoot,"maps/regions/npc-trades");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "NPC trade source/catalog identity differs.");
        var contracts = source.Document("contracts.json");
        Need(contracts.GetProperty("enabledTradeIds").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual(Enumerable.Range(0, 9)) &&
            contracts.GetProperty("deferredTradeIds").GetArrayLength() == 0 &&
            contracts.GetProperty("retainedMaps").GetArrayLength() == 8 &&
            contracts.GetProperty("construction").GetProperty("metLocationValue").GetInt32() == 254 &&
            contracts.GetProperty("construction").GetProperty("discardedRandomIvDraws").GetInt32() == 2,
            "NPC trade construction/runtime boundary differs.");
        var definitions = source.Document("trades.json").EnumerateArray().Select(Definition).ToImmutableArray();
        NpcTradePokemonCatalog trades;
        try { trades = NpcTradePokemonCatalog.Create(pokemon, definitions); }
        catch (ArgumentException e) { throw new InvalidDataException("NPC trade definitions are invalid.", e); }
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Cinnabar Lounge and nine NPC trade records.", ResourceId);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("mapPath").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative , "Lounge compact resource binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id && runtime.Width == 15 && runtime.Height == 11,
                "Lounge map identity/dimensions differ.");
            maps.Add(new(id), PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/sprites/overworld/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 1 && maps.ContainsKey(LoungeMapId) && maps[LoungeMapId].FieldWarps.Length == 1 &&
            maps[LoungeMapId].Events.GetProperty("object_events").GetArrayLength() == 3,
            "Only the original Lounge and its three actors may be added.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["INGAME_TRADE_NIDORAN"] == 2 && constants["INGAME_TRADE_NIDORINOA"] == 4 &&
            constants["FLAG_DID_MS_NIDO_TRADE"] == 587 && constants["FLAG_DID_NINA_TRADE"] == 593 &&
            constants["INGAME_TRADE_ELECTRODE"] == 6 && constants["INGAME_TRADE_TANGELA"] == 7 && constants["INGAME_TRADE_SEEL"] == 8 &&
            constants["PARTY_SIZE"] == 6, "Original trade IDs/cancel constant differ.");
        return new(source, trades, maps.ToImmutable(), constants);
    }
    private static NpcTradePokemonDefinition Definition(JsonElement row)
    {
        string S(string p) => row.GetProperty(p).GetString()!;
        int I(string p) => row.GetProperty(p).GetInt32();
        var ivs = row.GetProperty("ivs"); var conditions = row.GetProperty("contestConditions");
        return new(I("tradeId"), new(S("requestedSpecies")), new(S("receivedSpecies")), S("nickname"), row.GetProperty("personality").GetUInt32(),
            new(ivs.GetProperty("hp").GetInt32(), ivs.GetProperty("attack").GetInt32(), ivs.GetProperty("defense").GetInt32(),
                ivs.GetProperty("speed").GetInt32(), ivs.GetProperty("specialAttack").GetInt32(), ivs.GetProperty("specialDefense").GetInt32()),
            row.GetProperty("originalTrainerId").GetUInt32(), S("originalTrainerName"), I("originalTrainerGender"), I("abilitySlot"), S("heldItem"), I("mail"),
            new(conditions.GetProperty("cool").GetInt32(), conditions.GetProperty("beauty").GetInt32(), conditions.GetProperty("cute").GetInt32(),
                conditions.GetProperty("smart").GetInt32(), conditions.GetProperty("tough").GetInt32(), conditions.GetProperty("sheen").GetInt32()),
            S("tradeSymbol"), new(S("map")), I("localId"), S("scriptLabel"), S("completionFlag"), row.GetProperty("usesInlineSequence").GetBoolean());
    }
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
