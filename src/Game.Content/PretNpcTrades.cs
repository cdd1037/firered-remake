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
    public string GeneratedDirectory { get; }

    private PretNpcTrades(SilphSourcePackage source, NpcTradePokemonCatalog trades,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        Texts = source.Document("presentation-text.json").EnumerateArray().ToImmutableDictionary(
            r => r.GetProperty("symbol").GetString()!, r => r.GetProperty("text").GetString()!, StringComparer.Ordinal);
        Trades = trades; NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory;

    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE" => LoungeMapId,
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" => new("map.cinnabar-island-pokemon-lab-entrance"),
        _ => throw new InvalidDataException("Unknown original Lounge warp destination: " + symbol)
    };

    public static PretNpcTrades Load(PretCurrentWorld world, PokemonCatalog pokemon)
    {
        var projectRoot = world.ProjectRoot;
        ArgumentNullException.ThrowIfNull(pokemon);
        var source = new SilphSourcePackage(projectRoot,"maps/regions/npc-trades");
        var definitions = source.Document("trades.json").EnumerateArray().Select(Definition).ToImmutableArray();
        NpcTradePokemonCatalog trades;
        try { trades = NpcTradePokemonCatalog.Create(pokemon, definitions); }
        catch (ArgumentException e) { throw new InvalidDataException("NPC trade definitions are invalid.", e); }
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
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
