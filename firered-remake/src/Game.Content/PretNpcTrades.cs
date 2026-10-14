using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Original FireRed NPC trades and the one new Lounge map. Runtime enablement is bounded to 0/2/4/6/7/8.</summary>
public sealed class PretNpcTrades
{
    public const string ResourceId = "firered-npc-trades-v1";
    public const string ManifestSha256 = "b8a7a2b17f7114ca74b2ec4a8a10b37bb5269cd71ef8c5439a9d3183345032cb";
    public const string LockSha256 = "08c9ee2df1df83d49a2fd0a8b6f028cbea3659e239124b2c89264db646032c28";
    public const string FullSourceHash = "d34edcac7ca2b83d5e6f73360b3e1be983ad46bb558876768a26980507bd82be";
    public static MapId LoungeMapId { get; } = new("map.cinnabar-island-pokemon-lab-lounge");
    public NpcTradePokemonCatalog Trades { get; }
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretNpcTrades(SilphSourcePackage source, NpcTradePokemonCatalog trades,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants)
    {
        Texts = source.Document("presentation-text.json").EnumerateArray().ToImmutableDictionary(
            r => r.GetProperty("symbol").GetString()!, r => r.GetProperty("text").GetString()!, StringComparer.Ordinal);
        Trades = trades; NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants;
        SourceFiles = source.Files; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["npc-trades-manifest:" + ManifestSha256, "npc-trades-lock:" + LockSha256,
            "npc-trades-semantics:" + NpcTradePokemonCatalog.SemanticSha256,
            .. source.Files.Select(f => "npc-trades:" + f.Path + ":" + f.Sha256)];
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
    public static PretNpcTrades Load(string projectRoot, SourceMechanicsCatalog mechanics)
    {
        ArgumentNullException.ThrowIfNull(mechanics);
        var source = new SilphSourcePackage(projectRoot, "npc-trades", ResourceId,
            "scripts/npc-trades.lock.json", LockSha256, 32280, ManifestSha256, 4054, 105, 17);
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")) &&
            source.Manifest.GetProperty("tradeSemanticSha256").GetString() == NpcTradePokemonCatalog.SemanticSha256,
            "NPC trade source/catalog identity differs.");
        foreach (var row in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(row.GetProperty("reuseExisting").GetBoolean(), "Lounge actors must reuse authenticated existing graphics.");
            _ = SafariSource.Read(projectRoot, row.GetProperty("texturePath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        }
        var contracts = source.Document("contracts.json");
        foreach (var row in contracts.GetProperty("retainedMaps").EnumerateArray())
            _ = SafariSource.Read(projectRoot, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        Need(contracts.GetProperty("enabledTradeIds").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual([0, 2, 4, 6, 7, 8]) &&
            contracts.GetProperty("deferredTradeIds").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual([1, 3, 5]) &&
            contracts.GetProperty("retainedMaps").GetArrayLength() == 5 &&
            contracts.GetProperty("construction").GetProperty("metLocationValue").GetInt32() == 254 &&
            contracts.GetProperty("construction").GetProperty("discardedRandomIvDraws").GetInt32() == 2,
            "NPC trade construction/runtime boundary differs.");
        var definitions = source.Document("trades.json").EnumerateArray().Select(row => Definition(source, row)).ToImmutableArray();
        NpcTradePokemonCatalog trades;
        try { trades = NpcTradePokemonCatalog.Authenticate(mechanics, definitions); }
        catch (ArgumentException e) { throw new InvalidDataException("NPC trade semantic authentication failed.", e); }
        foreach (var definition in definitions)
        {
            var map = SilphSourcePackage.Json(source.Raw[definition.ActorSource.Path]);
            var actor = map.GetProperty("object_events")[definition.LocalId - 1];
            Need(actor.GetProperty("script").GetString() == definition.ScriptLabel &&
                definition.ScriptSource.Symbol == definition.ScriptLabel && definition.Source.Symbol == definition.TradeSymbol,
                "NPC trade original actor/source binding differs.");
        }
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "Original Cinnabar Lounge and nine NPC trade records.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray().ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
            Need(path == relative + "/runtime-map.json" && hash == outputs[path].GetProperty("sha256").GetString() &&
                spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length, "Lounge compact resource binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
                relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(), spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
            PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 15 && runtime.Height == 11,
                "Lounge map identity/dimensions differ.");
            foreach (var artifact in runtime.Artifacts)
                Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256, "Lounge image binding differs.");
            maps.Add(new(id), PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/silph-region/npc/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 1 && maps.ContainsKey(LoungeMapId) && maps[LoungeMapId].FieldWarps.Length == 1 &&
            maps[LoungeMapId].Events.GetProperty("object_events").GetArrayLength() == 3,
            "Only the original Lounge and its three actors may be added.");
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 152 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "NPC trade source script closure differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            string path = entry.GetString()!;
            // Other three services remain evidence only. ExperimentRoom already owns its full script.
            if (path.StartsWith("data/maps/", StringComparison.Ordinal) &&
                !path.StartsWith("data/maps/CinnabarIsland_PokemonLab_Lounge/", StringComparison.Ordinal) &&
                !path.StartsWith("data/maps/Route2_House/", StringComparison.Ordinal) &&
                !path.StartsWith("data/maps/UndergroundPath_NorthEntrance/", StringComparison.Ordinal) &&
                !path.StartsWith("data/maps/Route11_EastEntrance_2F/", StringComparison.Ordinal)) continue;
            string text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                string label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Missing NPC trade source script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret, path, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["INGAME_TRADE_NIDORAN"] == 2 && constants["INGAME_TRADE_NIDORINOA"] == 4 &&
            constants["FLAG_DID_MS_NIDO_TRADE"] == 587 && constants["FLAG_DID_NINA_TRADE"] == 593 &&
            constants["INGAME_TRADE_ELECTRODE"] == 6 && constants["INGAME_TRADE_TANGELA"] == 7 && constants["INGAME_TRADE_SEEL"] == 8 &&
            constants["PARTY_SIZE"] == 6, "Original trade IDs/cancel constant differ.");
        return new(source, trades, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static NpcTradePokemonDefinition Definition(SilphSourcePackage source, JsonElement row)
    {
        string S(string p) => row.GetProperty(p).GetString()!;
        int I(string p) => row.GetProperty(p).GetInt32();
        PokemonSourceRef Ref(string p)
        {
            var r = row.GetProperty(p);
            var result = new PokemonSourceRef(r.GetProperty("path").GetString()!, r.GetProperty("symbol").GetString()!,
                r.GetProperty("startLine").GetInt32(), r.GetProperty("endLine").GetInt32(), r.GetProperty("sha256").GetString()!);
            source.Reference(result); return result;
        }
        var ivs = row.GetProperty("ivs"); var conditions = row.GetProperty("contestConditions");
        return new(I("tradeId"), new(S("requestedSpecies")), new(S("receivedSpecies")), S("nickname"), row.GetProperty("personality").GetUInt32(),
            new(ivs.GetProperty("hp").GetInt32(), ivs.GetProperty("attack").GetInt32(), ivs.GetProperty("defense").GetInt32(),
                ivs.GetProperty("speed").GetInt32(), ivs.GetProperty("specialAttack").GetInt32(), ivs.GetProperty("specialDefense").GetInt32()),
            row.GetProperty("originalTrainerId").GetUInt32(), S("originalTrainerName"), I("originalTrainerGender"), I("abilitySlot"), S("heldItem"), I("mail"),
            new(conditions.GetProperty("cool").GetInt32(), conditions.GetProperty("beauty").GetInt32(), conditions.GetProperty("cute").GetInt32(),
                conditions.GetProperty("smart").GetInt32(), conditions.GetProperty("tough").GetInt32(), conditions.GetProperty("sheen").GetInt32()),
            S("tradeSymbol"), new(S("map")), I("localId"), S("scriptLabel"), S("completionFlag"), Ref("source"), Ref("actorSource"), Ref("scriptSource"), row.GetProperty("usesInlineSequence").GetBoolean());
    }
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
