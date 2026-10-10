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
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "56f010572588b32392d6ca95428fcb737d780e2e53fce33862282f3f2c5565ea";
    public const string ManifestSha256 = "5b2cab19c6636313ad7b0cdd02f4ecca6c5b2e6548f114656c445bdbb8198c88";
    public const string RawManifestSha256 = "0b3b453853e1d364678de275ddf9463cde6d60705ec70bd57da842320dbb812c";
    public const string StorageArtSha256 = "6d987aa6399f640427c78b7463a9727eca239fb1e9185a0b22669ad1f8ed0e51";
    public const string FullSourceHash = "2cb80021413baf1443c0c99a5ee55bd8053f71f2ded97a411d3aed55acbb44f7";
    private static readonly ImmutableDictionary<string, int> Species = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["BULBASAUR"]=1, ["IVYSAUR"]=2, ["VENUSAUR"]=3, ["CHARMANDER"]=4, ["CHARMELEON"]=5,
        ["CHARIZARD"]=6, ["SQUIRTLE"]=7, ["WARTORTLE"]=8, ["BLASTOISE"]=9, ["CATERPIE"]=10,
        ["METAPOD"]=11, ["BUTTERFREE"]=12, ["WEEDLE"]=13, ["KAKUNA"]=14, ["BEEDRILL"]=15,
        ["PIDGEY"]=16, ["PIDGEOTTO"]=17, ["PIDGEOT"]=18, ["RATTATA"]=19, ["RATICATE"]=20,
        ["PIKACHU"]=25, ["RAICHU"]=26, ["SANDSHREW"]=27, ["SANDSLASH"]=28, ["GEODUDE"]=74,
        ["GRAVELER"]=75, ["GOLEM"]=76, ["ONIX"]=95, ["STEELIX"]=208
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private static readonly string[] AssetIds = ["scrolling-background-phase0", "wallpaper-forest", "party-panel",
        "party-slot-empty", "party-slot-filled", "interface-frame", "box-background", "box-arrow-left", "box-arrow-right"];
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<StorageArtIcon> Icons { get; }
    public ImmutableArray<StorageArtAsset> Assets { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Display { get; }
    public JsonElement StorageArt { get; }
    public string GeneratedDirectory { get; }
    public string StorageArtSourcePath => Path.Combine(GeneratedDirectory, "storage-art.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory, "manifest.json");

    private PretStorageArt(string directory, ImmutableArray<StorageArtIcon> icons, ImmutableArray<StorageArtAsset> assets,
        ImmutableArray<PretWorldSourceFile> files, JsonElement art, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory=directory; Icons=icons; Assets=assets; SourceFiles=files; StorageArt=art;
        Display=art.GetProperty("display"); outputBytes=outputs;
        AdditionalSourceHashes=["storage-art-manifest:"+ManifestSha256, "storage-art-lock:"+LockSha256,
            .. files.Select(f=>"storage-art:"+f.Path+":"+f.Sha256)];
    }

    /// <summary>Checks the fixed source lock, both manifests, all raw inputs and every generated payload.</summary>
    public static PretStorageArt Load(string projectRoot)
    {
        var project=Path.GetFullPath(projectRoot);
        var directory=Path.Combine(project,"content/generated/storage-art");
        var raw=Path.Combine(project,"content/raw/storage-art");
        byte[] ReadPinned(string root, string path, string hash, int? length=null)
        {
            var bytes=File.ReadAllBytes(SafeFile(root,path));
            Require(Hash(bytes)==hash && (length is null || bytes.Length==length),"Pinned storage art differs: "+path);
            return bytes;
        }
        _=ReadPinned(raw,"manifest.json",RawManifestSha256);
        using var lockDocument=JsonDocument.Parse(ReadPinned(project,"scripts/fetch-storage-art.lock.json",LockSha256));
        using var manifest=JsonDocument.Parse(ReadPinned(directory,"manifest.json",ManifestSha256));
        var l=lockDocument.RootElement; var m=manifest.RootElement;
        Require(l.GetProperty("schemaVersion").GetInt32()==1 && l.GetProperty("profile").GetString()==Profile &&
            l.GetProperty("repository").GetString()=="pret/pokefirered" && l.GetProperty("commit").GetString()==SourceCommit &&
            l.GetProperty("tree").GetString()==SourceTree && l.GetProperty("gameEdition").GetString()=="FIRERED" &&
            l.GetProperty("romRevision").GetInt32()==0,"Storage art lock identity differs.");
        Require(m.GetProperty("schemaVersion").GetInt32()==1 && m.GetProperty("profile").GetString()==Profile &&
            m.GetProperty("sourceCommit").GetString()==SourceCommit && m.GetProperty("tree").GetString()==SourceTree &&
            m.GetProperty("lockSha256").GetString()==LockSha256 && m.GetProperty("fullSourceHash").GetString()==FullSourceHash &&
            m.GetProperty("inputCount").GetInt32()==63 && m.GetProperty("iconCount").GetInt32()==29 &&
            m.GetProperty("assetCount").GetInt32()==9 && m.GetProperty("textureCount").GetInt32()==38 &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean() &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(),"Storage art manifest identity differs.");
        var files=ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach(var entry in l.GetProperty("files").EnumerateArray())
        {
            var path=entry.GetProperty("path").GetString()!; var hash=entry.GetProperty("sha256").GetString()!;
            var blob=entry.GetProperty("gitBlobSha1").GetString()!; var length=entry.GetProperty("byteLength").GetInt32();
            var bytes=ReadPinned(raw,path,hash,length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), ..bytes]))==blob,
                "Storage art Git blob differs: "+path);
            files.Add(new(path,blob,length,hash,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory=files.ToImmutable();
        Require(inventory.Length==63 && inventory.Select(f=>f.Path).Distinct(StringComparer.Ordinal).Count()==63,
            "Storage art source inventory differs.");
        Require(Hash(Encoding.UTF8.GetBytes(string.Concat(inventory.OrderBy(f=>f.Path,StringComparer.Ordinal)
            .Select(f=>f.Path+"\0"+f.Sha256+"\n"))))==FullSourceHash,"Storage art source hash differs.");
        var outputs=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path=entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path,ReadPinned(directory,path,entry.GetProperty("sha256").GetString()!,
                entry.GetProperty("byteLength").GetInt32())),"Duplicate storage art output.");
        }
        var expected=Species.Keys.Select(s=>"icons/"+s.ToLowerInvariant()).Concat(AssetIds)
            .SelectMany(s=>new[]{s+".png",s+".rgba"}).Append("storage-art.json");
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected),"Storage art output inventory differs.");
        Require(Hash(outputs["storage-art.json"])==StorageArtSha256,"Storage art document differs.");
        using var document=JsonDocument.Parse(outputs["storage-art.json"]); var a=document.RootElement;
        Require(a.GetProperty("profile").GetString()==Profile && a.GetProperty("schemaVersion").GetInt32()==1 &&
            a.GetProperty("sourceCommit").GetString()==SourceCommit && a.GetProperty("fullSourceHash").GetString()==FullSourceHash,
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
                icon.TransparentIndex==0 && icon.PaletteVariant=="shared-normal-shiny" && icon.SourceRefs.Length==5 &&
                outputs[icon.RgbaPath].Length==4096 && Hash(outputs[icon.RgbaPath])==icon.RgbaSha256 &&
                Hash(outputs[icon.TexturePath])==icon.Sha256,"Storage icon metadata differs.");
            foreach(var source in icon.SourceRefs)
                Require(inventory.Any(f=>f.Path==source.Path && f.Sha256==source.Sha256),"Storage icon provenance differs.");
        }
        foreach(var asset in assets)
            Require(asset.Width>0 && asset.Height>0 && outputs[asset.RgbaPath].Length==asset.Width*asset.Height*4 &&
                Hash(outputs[asset.RgbaPath])==asset.RgbaSha256 && Hash(outputs[asset.TexturePath])==asset.Sha256,
                "Storage asset pixel metadata differs.");
        var d=a.GetProperty("display");
        Require(d.GetProperty("logicalWidth").GetInt32()==240 && d.GetProperty("logicalHeight").GetInt32()==160 &&
            d.GetProperty("boxColumns").GetInt32()==6 && d.GetProperty("boxRows").GetInt32()==5 &&
            d.GetProperty("normalShinyIconsIdentical").GetBoolean() && d.GetProperty("boxIconCenters").GetArrayLength()==30,
            "Storage art layout differs.");
        return new(directory,icons,assets,inventory,a.Clone(),outputs.ToImmutable());
    }

    public bool ContainsSpecies(SpeciesId species) => Icons.Any(i=>i.SpeciesId==species.Value);
    /// <summary>Shininess does not select a different icon palette in the original storage source.</summary>
    public StorageArtIcon Icon(SpeciesId species, bool shiny=false) => Icons.Single(i=>i.SpeciesId==species.Value);
    public StorageArtAsset Asset(string id) => Assets.Single(a=>a.Id==id);
    public byte[] ReadIconTexture(SpeciesId species, bool shiny=false) => [..outputBytes[Icon(species,shiny).TexturePath]];
    public byte[] ReadIconRgba(SpeciesId species, bool shiny=false) => [..outputBytes[Icon(species,shiny).RgbaPath]];
    public byte[] ReadAssetTexture(string id) => [..outputBytes[Asset(id).TexturePath]];
    public byte[] ReadAssetRgba(string id) => [..outputBytes[Asset(id).RgbaPath]];
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
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

public sealed record StorageArtSourceRef(string Path,string Symbol,string Sha256,int? StartLine=null,int? EndLine=null);
public sealed record StorageArtIcon(string SpeciesId,int SourceSpeciesId,string TexturePath,string RgbaPath,
    string Sha256,string RgbaSha256,int Width,int Height,int SourceFrameIndex,int SourceFrameCount,int PaletteIndex,
    string PaletteVariant,int TransparentIndex,string IndexSha256,ImmutableArray<int> PaletteRgb555,
    ImmutableArray<StorageArtSourceRef> SourceRefs);
public sealed record StorageArtAsset(string Id,string TexturePath,string RgbaPath,int Width,int Height,int X,int Y,
    string Sha256,string RgbaSha256);
