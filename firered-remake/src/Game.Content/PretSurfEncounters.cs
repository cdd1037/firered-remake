using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SurfWaterHeldItem(SpeciesId Species,string Common,string Rare);
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
        SafariSource.Require(doc.SchemaVersion==1&&
            doc.HeldItemDrawBound==100&&doc.HeldItems.Length==3&&
            !doc.Limitations.IsDefaultOrEmpty,"Surf water scope differs.");
        SafariSource.Require(doc.HeldItems.Select(h=>h.Species).ToHashSet().SetEquals(doc.Tables.SelectMany(t=>t.Slots).Select(s=>s.Species))&&
            doc.HeldItems.All(h=>h.Common=="ITEM_NONE"&&h.Rare=="ITEM_NONE"),"Surf source held-item closure differs.");
        return new(doc,WaterEncounterCatalog.Authenticate(doc.Tables));
    }
}
