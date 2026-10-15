using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SurfWaterHeldItem(SpeciesId Species,string Common,string Rare,PokemonSourceRef Source);
public sealed record SurfEncounterDocument(int SchemaVersion, string Profile, string SourceCommit, ImmutableArray<WaterEncounterTable> Tables,
    ImmutableArray<SurfWaterHeldItem> HeldItems, int HeldItemDrawBound, ImmutableArray<string> Limitations);

/// <summary>Authenticated17 mainland plus4 Safari water tables; not a field-use admission.</summary>
public sealed class PretSurfEncounters
{
    public const string Profile = "firered-surf-encounters-v1";
    public const string CatalogRelativePath = "content/generated/surf-encounters/catalog.json";
    public SurfEncounterDocument Document { get; }
    public WaterEncounterCatalog Catalog { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables => Catalog.Tables;
    public ImmutableDictionary<SpeciesId,SurfWaterHeldItem> HeldItems { get; }
    private PretSurfEncounters(SurfEncounterDocument doc,WaterEncounterCatalog catalog)
    { Document=doc;Catalog=catalog;HeldItems=doc.HeldItems.ToImmutableDictionary(h=>h.Species);}
    public static PretSurfEncounters Load(string projectRoot)
    {
        string root=Path.GetFullPath(projectRoot);
        var doc=SafariSource.Parse<SurfEncounterDocument>(SafariSource.Read(root,CatalogRelativePath));
byte[] Read(string path)
        {
            var bytes=SafariSource.Read(root,PretSourcePaths.FireRedRawDirectory + "/"+path);
return bytes;
        }
        using var wild=JsonDocument.Parse(Read("src/data/wild_encounters.json"));
        SafariSource.Require(doc.SchemaVersion==1&&
            doc.HeldItemDrawBound==100&&doc.HeldItems.Length==3&&
            !doc.Limitations.IsDefaultOrEmpty,"Surf water scope differs.");
        var originals=wild.RootElement.GetProperty("wild_encounter_groups").EnumerateArray().SelectMany(g=>g.GetProperty("encounters").EnumerateArray()).ToArray();
        foreach(var table in doc.Tables)
        {
            var original=originals.Single(t=>t.GetProperty("base_label").GetString()==table.SourceSymbol);
            SafariSource.Require(original.GetProperty("map").GetString()==table.SourceMapSymbol,"Surf water map symbol differs.");
            var water=original.GetProperty("water_mons");
            SafariSource.Require(water.GetProperty("encounter_rate").GetInt32()==table.RawEncounterRate&&water.GetProperty("mons").GetArrayLength()==5,"Surf source water rate differs.");
            foreach(var slot in table.Slots)
            {
                var raw=water.GetProperty("mons")[slot.Index];
                SafariSource.Require(slot.Species.Value=="pret:"+raw.GetProperty("species").GetString()&&slot.MinimumLevel==raw.GetProperty("min_level").GetByte()&&
                    slot.MaximumLevel==raw.GetProperty("max_level").GetByte()&&slot.Weight==WaterEncounterRules.SlotWeights[slot.Index],"Surf source water slot differs.");
            }
        }
        SafariSource.Require(doc.HeldItems.Select(h=>h.Species).ToHashSet().SetEquals(doc.Tables.SelectMany(t=>t.Slots).Select(s=>s.Species))&&
            doc.HeldItems.All(h=>h.Common=="ITEM_NONE"&&h.Rare=="ITEM_NONE"),"Surf source held-item closure differs.");
        return new(doc,WaterEncounterCatalog.Authenticate(doc.Tables));
    }
}
