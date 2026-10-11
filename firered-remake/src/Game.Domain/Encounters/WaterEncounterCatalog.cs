using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Exact21-table source proof. A copied record does not inherit table authentication.</summary>
public sealed class WaterEncounterCatalog
{
    public const string SemanticSha256 = "13357b80d46f0d4c1d42ac6a2e012d81217751565c7f0d4a525774f967f0ce38";
    private static readonly ConditionalWeakTable<WaterEncounterTable, WaterEncounterCatalog> Authenticated = new();
    public ImmutableDictionary<MapId, WaterEncounterTable> Tables { get; }
    private WaterEncounterCatalog(ImmutableArray<WaterEncounterTable> tables) => Tables = tables.ToImmutableDictionary(t => t.Map);
    public static WaterEncounterCatalog Authenticate(ImmutableArray<WaterEncounterTable> tables)
    {
        if (tables.IsDefault || tables.Length != 21 || tables.Any(t => t is null || t.Slots.IsDefault || t.Slots.Length != 5 ||
            t.Slots.Any(s => s is null))) throw new ArgumentException("water_catalog_scope");
        // Delimited fingerprint fields cannot contain delimiters, so distinct record boundaries
        // cannot be forged by moving separators between source/map/species strings.
        bool Clean(string? value) => value is not null && value.Length > 0 && !value.Contains('|') &&
            !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r');
        if (tables.Any(t => new[] { t.Map.Value,t.SourceMapSymbol,t.SourceCommit,t.SourcePath,t.SourceSymbol,t.SourceSha256 }.Any(v=>!Clean(v)) ||
            t.SourceCommit != Game.Domain.Pokemon.PokemonFactory.SourceCommit || t.SourcePath != "src/data/wild_encounters.json" ||
            t.Slots.Any(slot=>!Clean(slot.Species.Value)))) throw new ArgumentException("water_catalog_identity");
        string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        string value = string.Join('\n', tables.OrderBy(t => t.Map.Value, StringComparer.Ordinal).Select(t => string.Join('|',
            new[] { t.Map.Value,t.SourceMapSymbol,t.SourceCommit,t.SourcePath,t.SourceSymbol,t.SourceSha256,N(t.RawEncounterRate) }
            .Concat(t.Slots.Select(s => string.Join(',',N(s.Index),s.Species.Value,N(s.MinimumLevel),N(s.MaximumLevel),N(s.Weight)))))));
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value))) != SemanticSha256)
            throw new ArgumentException("water_catalog_source");
        // Each proof receives independent roots; caller-side records and clones stay unauthenticated.
        var proof = new WaterEncounterCatalog(tables.Select(t => t with { }).ToImmutableArray());
        foreach (var table in proof.Tables.Values) Authenticated.Add(table, proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Authenticated.TryGetValue(table,out _);
    public static void ValidateTable(WaterEncounterTable table)
    { if (!IsAuthenticated(table)) throw new ArgumentException("water_table_admission"); }
}
