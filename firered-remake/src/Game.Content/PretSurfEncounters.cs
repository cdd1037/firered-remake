using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SurfWaterHeldItem(SpeciesId Species,string Common,string Rare,PokemonSourceRef Source);
public sealed record SurfEncounterDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<WaterEncounterTable> Tables,ImmutableArray<SurfWaterHeldItem> HeldItems,int HeldItemDrawBound,
    string TableSemanticSha256,ImmutableArray<string> Limitations);

/// <summary>Authenticated17 mainland plus4 Safari water tables; not a field-use admission.</summary>
public sealed class PretSurfEncounters
{
    public const string Profile = "firered-surf-encounters-v1";
    public const string CatalogSha256 = "6183466f638317fba2d7e9c649b25ba771047c8e4953c4f6cb85308fe212b385";
    public const int CatalogByteLength = 19057;
    public const string CatalogRelativePath = "content/generated/surf-encounters/catalog.json";
    public SurfEncounterDocument Document { get; }
    public WaterEncounterCatalog Catalog { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables => Catalog.Tables;
    public ImmutableDictionary<SpeciesId,SurfWaterHeldItem> HeldItems { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretSurfEncounters(SurfEncounterDocument doc,WaterEncounterCatalog catalog,ImmutableArray<string> hashes)
    { Document=doc;Catalog=catalog;HeldItems=doc.HeldItems.ToImmutableDictionary(h=>h.Species);AdditionalSourceHashes=hashes; }
    public static PretSurfEncounters Load(string projectRoot)
    {
        string root=Path.GetFullPath(projectRoot);
        var doc=SafariSource.Parse<SurfEncounterDocument>(SafariSource.Read(root,CatalogRelativePath,CatalogSha256,CatalogByteLength));
        using var sourceLock=JsonDocument.Parse(SafariSource.Read(root,"scripts/surf-pokemon.lock.json",PretSurfPokemon.LockSha256,PretSurfPokemon.LockByteLength));
        var hashes=ImmutableArray.CreateBuilder<string>();hashes.Add("surf-encounters:"+CatalogSha256);hashes.Add("surf-encounters-source-lock:"+PretSurfPokemon.LockSha256);
        byte[] Read(string path)
        {
            var input=sourceLock.RootElement.GetProperty("files").EnumerateArray().Single(f=>f.GetProperty("path").GetString()==path);
            string hash=input.GetProperty("sha256").GetString()!;
            var bytes=SafariSource.Read(root,"content/raw/surf-pokemon/"+path,hash,input.GetProperty("byteLength").GetInt32());
            SafariSource.Require(SafariSource.GitBlob(bytes)==input.GetProperty("gitBlobSha1").GetString(),"Surf water Git blob differs.");
            hashes.Add("surf-encounters-source:"+path+":"+hash);return bytes;
        }
        using var wild=JsonDocument.Parse(Read("src/data/wild_encounters.json"));
        _=Read("src/data/pokemon/species_info.h");_=Read("src/wild_encounter.c");_=Read("include/wild_encounter.h");
        SafariSource.Require(doc.SchemaVersion==1&&doc.Profile==Profile&&doc.SourceCommit==PokemonFactory.SourceCommit&&
            doc.HeldItemDrawBound==100&&doc.HeldItems.Length==3&&doc.TableSemanticSha256==WaterEncounterCatalog.SemanticSha256&&
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
        return new(doc,WaterEncounterCatalog.Authenticate(doc.Tables),hashes.ToImmutable());
    }
}
