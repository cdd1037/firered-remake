using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated, static storage layers and original frame-zero icons. Does not execute storage rules.</summary>
public sealed class PretStorageArt
{
    public const string Profile = "firered-storage-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private static readonly ImmutableDictionary<string, int> Species = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["BULBASAUR"]=1,["IVYSAUR"]=2,["VENUSAUR"]=3,["CHARMANDER"]=4,["CHARMELEON"]=5,["CHARIZARD"]=6,["SQUIRTLE"]=7,["WARTORTLE"]=8,["BLASTOISE"]=9,["CATERPIE"]=10,["METAPOD"]=11,["BUTTERFREE"]=12,["WEEDLE"]=13,["KAKUNA"]=14,["BEEDRILL"]=15,["PIDGEY"]=16,["PIDGEOTTO"]=17,["PIDGEOT"]=18,["RATTATA"]=19,["RATICATE"]=20,["PIKACHU"]=25,["RAICHU"]=26,["SANDSHREW"]=27,["SANDSLASH"]=28,["GEODUDE"]=74,["GRAVELER"]=75,["GOLEM"]=76,["ONIX"]=95,["STEELIX"]=208
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private static readonly string[] AssetIds = ["scrolling-background-phase0", "wallpaper-forest", "party-panel",
        "party-slot-empty", "party-slot-filled", "interface-frame", "box-background", "box-arrow-left", "box-arrow-right"];
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<StorageArtIcon> Icons { get; }
    public ImmutableArray<StorageArtAsset> Assets { get; }
    public JsonElement Display { get; }
    public JsonElement StorageArt { get; }
    public string GeneratedDirectory { get; }
    public string StorageArtSourcePath => Path.Combine(GeneratedDirectory, "storage-art.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory, "manifest.json");

    private PretStorageArt(string directory, ImmutableArray<StorageArtIcon> icons, ImmutableArray<StorageArtAsset> assets,
        JsonElement art, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory=directory; Icons=icons; Assets=assets; StorageArt=art;
        Display=art.GetProperty("display"); outputBytes=outputs;
    }

    /// <summary>Checks the fixed source lock, both manifests, all raw inputs and every generated payload.</summary>
    public static PretStorageArt Load(string projectRoot)
    {
        var project=Path.GetFullPath(projectRoot);
        var directory=Path.Combine(project,"content/generated/ui/storage-art");
        byte[] ReadPinned(string root,string path)
        {
            var bytes=File.ReadAllBytes(SafeFile(root,path));
            return bytes;
        }
        using var manifest=JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m=manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32()==1 && m.GetProperty("iconCount").GetInt32()==29 &&
            m.GetProperty("assetCount").GetInt32()==9 && m.GetProperty("textureCount").GetInt32()==38,"Storage art manifest identity differs.");

        var outputs=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path=entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path,ReadPinned(directory,path)),"Duplicate storage art output.");
        }
        var expected=Species.Keys.Select(s=>"icons/"+s.ToLowerInvariant()).Concat(AssetIds)
            .Select(s => s + ".png").Append("storage-art.json");
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected),"Storage art output inventory differs.");
        using var document=JsonDocument.Parse(outputs["storage-art.json"]); var a=document.RootElement;
        Require(a.GetProperty("schemaVersion").GetInt32()==1 ,
            "Storage art document identity differs.");
        var options=ContentJson.CreateOptions();
        var icons=ContentJson.ParseStrict<ImmutableArray<StorageArtIcon>>(a.GetProperty("icons").GetRawText(),options);
        var assets=ContentJson.ParseStrict<ImmutableArray<StorageArtAsset>>(a.GetProperty("assets").GetRawText(),options);
        Require(icons.Length==29 && icons.Select(i=>i.SpeciesId).Distinct().Count()==29 && assets.Length==9 &&
            assets.Select(i=>i.Id).ToHashSet().SetEquals(AssetIds),"Storage art pixel metadata inventory differs.");
        foreach(var icon in icons)
        {
            var species=Species.Single(s=>icon.SpeciesId=="pret:SPECIES_"+s.Key);
            Require(species.Value==icon.SourceSpeciesId && icon.Width==32 && icon.Height==32 && icon.SourceFrameIndex==0 &&
                icon.SourceFrameCount==2 && icon.PaletteIndex is >=0 and <=2 && icon.PaletteRgb555.Length==16 &&
                icon.TransparentIndex==0 && icon.PaletteVariant=="shared-normal-shiny" && (outputs[icon.TexturePath]).Length > 0,"Storage icon metadata differs.");
        }
        foreach(var asset in assets)
            Require(asset.Width>0 && asset.Height>0 && (outputs[asset.TexturePath]).Length > 0,
                "Storage asset pixel metadata differs.");
        var d=a.GetProperty("display");
        Require(d.GetProperty("logicalWidth").GetInt32()==240 && d.GetProperty("logicalHeight").GetInt32()==160 &&
            d.GetProperty("boxColumns").GetInt32()==6 && d.GetProperty("boxRows").GetInt32()==5 &&
            d.GetProperty("normalShinyIconsIdentical").GetBoolean() && d.GetProperty("boxIconCenters").GetArrayLength()==30,
            "Storage art layout differs.");
        return new(directory,icons,assets,a.Clone(),outputs.ToImmutable());
    }

    public bool ContainsSpecies(SpeciesId species) => Icons.Any(i=>i.SpeciesId==species.Value);
    /// <summary>Shininess does not select a different icon palette in the original storage source.</summary>
    public StorageArtIcon Icon(SpeciesId species, bool shiny=false) => Icons.Single(i=>i.SpeciesId==species.Value);
    public StorageArtAsset Asset(string id) => Assets.Single(a=>a.Id==id);
    public byte[] ReadIconTexture(SpeciesId species, bool shiny=false) => [..outputBytes[Icon(species,shiny).TexturePath]];

    public byte[] ReadAssetTexture(string id) => [..outputBytes[Asset(id).TexturePath]];

    private static void Require(bool condition,string message) { if(!condition) throw new InvalidDataException(message); }
    private static string SafeFile(string root,string path)
    {
        if(string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p=>p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe storage art path: "+path);
        for(var cursor=new DirectoryInfo(root);cursor is not null;cursor=cursor.Parent)
            if(cursor.LinkTarget is not null) throw new InvalidDataException("Storage art directory symlink is unsupported.");
        var result=root;
        foreach(var segment in path.Split('/'))
        {
            result=Path.Combine(result,segment);
            if(new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Storage art file symlink is unsupported: "+path);
        }
        return result;
    }
}

public sealed record StorageArtSourceRef(string Path, string Symbol, int? StartLine = null, int? EndLine = null);
public sealed record StorageArtIcon(string SpeciesId, int SourceSpeciesId, string TexturePath, int Width, int Height,
    int SourceFrameIndex, int SourceFrameCount, int PaletteIndex, string PaletteVariant, int TransparentIndex,
    ImmutableArray<int> PaletteRgb555);
public sealed record StorageArtAsset(string Id, string TexturePath, int Width, int Height, int X, int Y);
