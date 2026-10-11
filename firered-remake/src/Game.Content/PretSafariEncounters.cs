using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record SafariEncounterSourceRef(SourceKind Kind, string Path, string Symbol, string Repository, string Commit, string Sha256, int Line);
public sealed record SafariHeldItemRow(string Species, string Common, string Rare, SafariEncounterSourceRef SourceRef);
public sealed record SafariEncounterDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<EarlyLandEncounterTable> Tables, ImmutableArray<SafariHeldItemRow> HeldItems,
    int HeldItemDrawBound, JsonElement ExcludedTables, ImmutableArray<string> MissingGroundSpecies,
    ImmutableArray<string> MissingDeferredSpecies, ImmutableArray<string> Limitations);

/// <summary>Four authenticated FireRed ground tables. Loading never admits water, fishing or a battle profile.</summary>
public sealed class PretSafariEncounters
{
    public const string Profile = "firered-safari-encounters-v1";
    public ImmutableDictionary<MapId, EarlyLandEncounterTable> Tables { get; }
    public ImmutableDictionary<SpeciesId, SafariHeldItemRow> HeldItems { get; }
    public JsonElement ExcludedTables { get; }
    public SafariEncounterDocument Document { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretSafariEncounters(SafariEncounterDocument document, ImmutableArray<string> hashes)
    {
        Document = document; Tables = document.Tables.ToImmutableDictionary(t => t.Map);
        HeldItems = document.HeldItems.ToImmutableDictionary(h => new SpeciesId(h.Species));
        ExcludedTables = document.ExcludedTables.Clone(); AdditionalSourceHashes = hashes;
    }

    public static PretSafariEncounters Load(PretSafariRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        var doc = ContentJson.ParseStrict<SafariEncounterDocument>(region.EncounterInventory.GetRawText(), ContentJson.CreateOptions());
        Need(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == PretWorldMap.SourceCommit &&
            doc.Tables.Length == 4 && doc.ExcludedTables.GetArrayLength() == 8 && doc.HeldItemDrawBound == 100 &&
            doc.MissingGroundSpecies.SequenceEqual(new[] { "SPECIES_CHANSEY", "SPECIES_SCYTHER", "SPECIES_TAUROS" }) &&
            doc.MissingDeferredSpecies.SequenceEqual(new[] { "SPECIES_DRAGONAIR", "SPECIES_DRATINI", "SPECIES_PSYDUCK" }) &&
            !doc.Limitations.IsDefaultOrEmpty, "Safari encounter scope differs.");
        const string sourcePath = "src/data/wild_encounters.json";
        var source = region.SourceFiles.Single(f => f.Path == sourcePath);
        var rawTables = region.SourceWildEncounters.GetProperty("wild_encounter_groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("encounters").EnumerateArray()).ToArray();
        var expectedAreas = new[] { "Center", "East", "North", "West" };
        for (var index = 0; index < expectedAreas.Length; index++)
        {
            var area = expectedAreas[index]; var table = doc.Tables[index]; var symbol = "sSafariZone" + area + "_FireRed";
            var map = new MapId("map.safari-zone-" + area.ToLowerInvariant());
            Need(table.Map == map && region.SourceMaps.ContainsKey(map) && table.SourceCommit == PretWorldMap.SourceCommit &&
                table.SourceSymbol == symbol && table.SourcePath == sourcePath && table.SourceSha256 == source.Sha256,
                "Safari encounter map/version identity differs.");
            var raw = rawTables.Single(t => t.GetProperty("base_label").GetString() == symbol);
            Need(raw.GetProperty("map").GetString() == "MAP_SAFARI_ZONE_" + area.ToUpperInvariant(), "Safari source map symbol differs.");
            var land = raw.GetProperty("land_mons"); var mons = land.GetProperty("mons");
            Need(table.RawEncounterRate == 21 && table.RawEncounterRate == land.GetProperty("encounter_rate").GetInt32() &&
                table.Slots.Length == 12 && mons.GetArrayLength() == 12, "Safari source ground table differs.");
            for (var slot = 0; slot < 12; slot++)
            {
                var actual = table.Slots[slot]; var expected = mons[slot];
                Need(actual.Index == slot && actual.Weight == EarlyLandEncounterRules.SlotWeights[slot] &&
                    actual.Species.Value == "pret:" + expected.GetProperty("species").GetString() &&
                    actual.MinimumLevel == expected.GetProperty("min_level").GetByte() && actual.MaximumLevel == expected.GetProperty("max_level").GetByte(),
                    "Safari slot differs from authenticated source.");
            }
            foreach (var method in new[] { "water_mons", "fishing_mons" })
            {
                var excluded = doc.ExcludedTables.EnumerateArray().Single(e => e.GetProperty("map").GetString() == map.Value && e.GetProperty("method").GetString() == method);
                Need(!excluded.GetProperty("enabled").GetBoolean() && excluded.GetProperty("sourceSymbol").GetString() == symbol &&
                    JsonElement.DeepEquals(excluded.GetProperty("sourceTable"), raw.GetProperty(method)), "Safari deferred source table differs.");
            }
        }
        var species = doc.Tables.SelectMany(t => t.Slots).Select(s => s.Species.Value).ToHashSet(StringComparer.Ordinal);
        Need(species.Count == 15 && doc.HeldItems.Length == species.Count && doc.HeldItems.Select(h => h.Species).ToHashSet(StringComparer.Ordinal).SetEquals(species), "Safari held-item species closure differs.");
        foreach (var row in doc.HeldItems)
            Need(row.SourceRef.Kind == SourceKind.Pret && row.SourceRef.Repository == "pret/pokefirered" && row.SourceRef.Commit == PretWorldMap.SourceCommit &&
                row.SourceRef.Line > 0 && row.SourceRef.Path == "src/data/pokemon/species_info.h" && row.SourceRef.Symbol == row.Species[5..] &&
                region.SourceFiles.Any(f => f.Path == row.SourceRef.Path && f.Sha256 == row.SourceRef.Sha256), "Safari held-item source identity differs.");
        Need(doc.HeldItems.Single(h => h.Species == "pret:SPECIES_CHANSEY") is { Common: "ITEM_NONE", Rare: "ITEM_LUCKY_EGG" }, "Chansey Lucky Egg source differs.");
        return new(doc, ["safari-encounters-source:" + source.Sha256, "safari-encounters-manifest:" + PretSafariRegion.ManifestSha256]);
    }
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
