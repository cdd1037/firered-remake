using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Original FireRed table identity plus the original map actor and one-time script binding.</summary>
public sealed record NpcTradePokemonDefinition(int TradeId, SpeciesId RequestedSpecies, SpeciesId ReceivedSpecies,
    string Nickname, uint Personality, SixStats Ivs, uint OriginalTrainerId, string OriginalTrainerName,
    int OriginalTrainerGender, int AbilitySlot, string HeldItem, int Mail, PokemonContestConditions ContestConditions,
    string TradeSymbol, MapId Map, int LocalId, string ScriptLabel, string CompletionFlag,
    PokemonSourceRef Source, PokemonSourceRef ActorSource, PokemonSourceRef ScriptSource, bool UsesInlineSequence);

/// <summary>Authenticated immutable original trade definitions. A serialized origin never creates this proof.</summary>
public sealed class NpcTradePokemonCatalog
{
    public const string SemanticSha256 = "c259397a134f07268ae6d788ff7c7f2cb66d6d19755e07c3d525f4dec92c7414";
    private static readonly ConditionalWeakTable<PokemonCatalog, NpcTradePokemonCatalog> Proofs = new();
    public SourceMechanicsCatalog Mechanics { get; }
    public ImmutableDictionary<int, NpcTradePokemonDefinition> Definitions { get; }
    private NpcTradePokemonCatalog(SourceMechanicsCatalog mechanics, ImmutableArray<NpcTradePokemonDefinition> definitions)
    { Mechanics = mechanics; Definitions = definitions.ToImmutableDictionary(d => d.TradeId); }
    public bool IsEnabled(int tradeId) => tradeId is 0 or 2 or 3 or 4 or 6 or 7 or 8;

    public static NpcTradePokemonCatalog Authenticate(SourceMechanicsCatalog mechanics,
        ImmutableArray<NpcTradePokemonDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(mechanics);
        if (!ReferenceEquals(SourceMechanicsCatalog.For(mechanics.Catalog), mechanics) || definitions.IsDefault ||
            definitions.Length != 9 || definitions.Any(d => d is null || d.Ivs is null || d.ContestConditions is null ||
                d.Source is null || d.ActorSource is null || d.ScriptSource is null) ||
            !definitions.Select(d => d.TradeId).SequenceEqual(Enumerable.Range(0, 9)))
            throw new ArgumentException("npc_trade_catalog_identity");
        if (Fingerprint(definitions) != SemanticSha256)
            throw new ArgumentException("npc_trade_catalog_fingerprint");
        foreach (var definition in definitions)
            if (!mechanics.Catalog.Species.ContainsKey(definition.RequestedSpecies) ||
                !mechanics.Catalog.Species.ContainsKey(definition.ReceivedSpecies))
                throw new ArgumentException("npc_trade_catalog_species");
        return Proofs.GetValue(mechanics.Catalog, _ => new(mechanics, definitions));
    }

    public static NpcTradePokemonCatalog? For(PokemonCatalog? catalog)
    {
        if (catalog is null) return null;
        var mechanics = SourceMechanicsCatalog.For(catalog) ?? OwnedBattlePokemonCatalog.For(catalog)?.Mechanics;
        return mechanics is not null && Proofs.TryGetValue(mechanics.Catalog, out var proof) ? proof : null;
    }

    private static string Fingerprint(ImmutableArray<NpcTradePokemonDefinition> definitions)
    {
        // This transport projection is independently identical to the importer's sorted compact JSON.
        // All source field values live in authenticated data, never an authored duplicate table.
        var rows = definitions.Select(d => new {
            d.TradeId, RequestedSpecies = d.RequestedSpecies.Value, ReceivedSpecies = d.ReceivedSpecies.Value,
            d.Nickname, d.Personality,
            Ivs = new { d.Ivs.Hp, d.Ivs.Attack, d.Ivs.Defense, d.Ivs.Speed, d.Ivs.SpecialAttack, d.Ivs.SpecialDefense },
            d.OriginalTrainerId, d.OriginalTrainerName, d.OriginalTrainerGender, d.AbilitySlot, d.HeldItem, d.Mail,
            d.ContestConditions, d.TradeSymbol, Map = d.Map.Value, d.LocalId, d.ScriptLabel, d.CompletionFlag,
            d.Source, d.ActorSource, d.ScriptSource, d.UsesInlineSequence
        });
        var document = JsonSerializer.SerializeToElement(rows, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteCanonical(writer, document);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(p.Name); WriteCanonical(writer, p.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var row in value.EnumerateArray()) WriteCanonical(writer, row); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
}
