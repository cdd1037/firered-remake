using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record FuchsiaRouteArtDocument(int SchemaVersion,string Profile,string SourceCommit,string LockSha256,
    ImmutableArray<ForestPokemonSprite> Sprites,ImmutableArray<StorageArtIcon> Icons,ImmutableArray<Route3PokemonFootprint> Footprints,
    ImmutableArray<SnorlaxArtDex> Dex,ImmutableArray<string> Limits,ImmutableArray<SnorlaxArtOutput> Outputs);
/// <summary>Authenticated four-species normal/shiny sprites, source icons and footprints; no battle executor or audio activation.</summary>
public sealed class PretFuchsiaRouteArt
{
    public const string Profile="firered-fuchsia-route-art-v1";
    public const string ManifestSha256="dac0d3b3127795bb4f923ac02fc788512f27c9f638f85d32e51da00317a67c23";
    public const string GeneratedDirectory="content/generated/fuchsia-route-art/";
    public FuchsiaRouteArtDocument Document { get; }
    public JsonElement Manifest { get; }
    public ImmutableDictionary<string,byte[]> Files { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites=>Document.Sprites;
    public ImmutableArray<StorageArtIcon> Icons=>Document.Icons;
    public ImmutableArray<Route3PokemonFootprint> Footprints=>Document.Footprints;
    public ImmutableDictionary<int,PokedexMenuEntry> DexEntries { get; }
    public ImmutableDictionary<int,CaptureDexEntry> CaptureDexEntries { get; }
    public ImmutableDictionary<SpeciesId,int> WeightsHectograms { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretFuchsiaRouteArt(FuchsiaRouteArtDocument doc,JsonElement manifest,ImmutableDictionary<string,byte[]> files,PretPokedexMenu dex)
    {
        Document=doc;Manifest=manifest;Files=files;
        var entries=ImmutableDictionary.CreateBuilder<int,PokedexMenuEntry>();var capture=ImmutableDictionary.CreateBuilder<int,CaptureDexEntry>();var weights=ImmutableDictionary.CreateBuilder<SpeciesId,int>();
        foreach(var item in doc.Dex)
        {
            int id=item.SourceSpeciesId;var sprite=Sprites.Single(s=>s.SourceSpeciesId==id&&s.Side=="front"&&s.PaletteVariant=="normal");var foot=Footprints.Single(f=>f.SourceSpeciesId==id);
            var d=dex.Entries[id] with{Portrait=new(GeneratedDirectory+sprite.TexturePath,sprite.Sha256,sprite.RgbaSha256,64,64),Footprint=new(GeneratedDirectory+foot.TexturePath,foot.Sha256,foot.RgbaSha256,16,16)};
            entries.Add(id,d);capture.Add(id,new(d.SourceSpeciesId,d.NationalNumber,d.Name,d.Category,d.DisplayCategory,d.HeightDecimeters,d.WeightHectograms,d.HeightDisplay,d.WeightDisplay,d.Description,d.Footprint!.TexturePath,d.Portrait!.TexturePath,d.HabitatTitle,d.Source,d.DescriptionSource));weights.Add(new(sprite.SpeciesId),d.WeightHectograms);
        }
        DexEntries=entries.ToImmutable();CaptureDexEntries=capture.ToImmutable();WeightsHectograms=weights.ToImmutable();
        AdditionalSourceHashes=["fuchsia-route-art-manifest:"+ManifestSha256,"fuchsia-route-art-source-lock:"+PretFuchsiaRoutePokemon.LockSha256,"fuchsia-route-art-dex:"+PretPokedexMenu.GeneratedSha256,..doc.Outputs.Select(o=>"fuchsia-route-art:"+o.Path+":"+o.Sha256)];
    }
    public static PretFuchsiaRouteArt Load(string projectRoot,string? dependencyProjectRoot=null,PretPokedexMenu? pokedexMenu=null)
    {
        var root=Path.GetFullPath(projectRoot);var dependency=Path.GetFullPath(dependencyProjectRoot??root);
        var lb=PretCeladonApproachSource.Read(root,"scripts/fuchsia-route-pokemon.lock.json",PretFuchsiaRoutePokemon.LockSha256,22167);using var sourceLock=JsonDocument.Parse(lb);var inputs=sourceLock.RootElement.GetProperty("files");Require(inputs.GetArrayLength()==93,"Fuchsia art source inventory differs.");
        var sources=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in inputs.EnumerateArray())
        {
            string path=row.GetProperty("path").GetString()!,hash=row.GetProperty("sha256").GetString()!;var b=PretCeladonApproachSource.Read(root,"content/raw/fuchsia-route-pokemon/"+path,hash,row.GetProperty("byteLength").GetInt32());
            Require(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))==row.GetProperty("gitBlobSha1").GetString(),"Fuchsia art Git blob differs.");sources.Add(path,hash);
        }
        var bytes=PretCeladonApproachSource.Read(root,GeneratedDirectory+"manifest.json",ManifestSha256,60910);using var json=JsonDocument.Parse(bytes);var doc=ContentJson.ParseStrict<FuchsiaRouteArtDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        Require(doc.SchemaVersion==1&&doc.Profile==Profile&&doc.SourceCommit==PretFuchsiaRoutePokemon.SourceCommit&&doc.LockSha256==PretFuchsiaRoutePokemon.LockSha256&&doc.Sprites.Length==16&&doc.Icons.Length==4&&doc.Footprints.Length==4&&doc.Dex.Length==4&&doc.Outputs.Length==48&&!doc.Limits.IsDefaultOrEmpty,"Fuchsia art scope differs.");
        var expected=new Dictionary<int,string>{{83,"FARFETCHD"},{84,"DODUO"},{85,"DODRIO"},{132,"DITTO"}};var files=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var o in doc.Outputs)files.Add(o.Path,PretCeladonApproachSource.Read(root,GeneratedDirectory+o.Path,o.Sha256,o.ByteLength));
        void Source(string p,string hash)=>Require(sources.TryGetValue(p,out var h)&&h==hash,"Fuchsia art source reference differs.");
        void Pixels(string png,string rgba,string pngHash,string rgbaHash,int w,int h)=>Require(files.ContainsKey(png)&&files.ContainsKey(rgba)&&Hash(files[png])==pngHash&&Hash(files[rgba])==rgbaHash&&files[rgba].Length==w*h*4,"Fuchsia pixels differ.");
        foreach(var (id,name) in expected)
        {
            var ss=doc.Sprites.Where(s=>s.SourceSpeciesId==id).ToArray();Require(ss.Length==4&&ss.Select(s=>(s.Side,s.PaletteVariant)).ToHashSet().SetEquals(new[]{("front","normal"),("front","shiny"),("back","normal"),("back","shiny")}),"Fuchsia sprite variants differ.");
            foreach(var s in ss)
            {
                Require(s.SpeciesId=="pret:SPECIES_"+name&&s.Width==64&&s.Height==64&&s.FrameWidth==64&&s.FrameHeight==64&&s.FrameCount==1&&s.SourceFrameIndex==0&&s.TransparentIndex==0&&!s.FlipH&&s.PaletteRgb555.Length==16,"Fuchsia sprite identity differs.");Pixels(s.TexturePath,s.RgbaPath,s.Sha256,s.RgbaSha256,64,64);
                foreach(var r in s.SourceRefs){Require(r.Kind=="pret"&&r.Repository=="pret/pokefirered"&&r.Commit==PretFuchsiaRoutePokemon.SourceCommit,"Fuchsia sprite provenance differs.");Source(r.Path,r.Sha256);}
            }
            var icon=doc.Icons.Single(i=>i.SourceSpeciesId==id);Require(icon.SpeciesId=="pret:SPECIES_"+name&&icon.Width==32&&icon.Height==32&&icon.SourceFrameIndex==0&&icon.SourceFrameCount==2&&icon.TransparentIndex==0&&icon.PaletteVariant=="shared-normal-shiny"&&icon.PaletteRgb555.Length==16,"Fuchsia icon differs.");Pixels(icon.TexturePath,icon.RgbaPath,icon.Sha256,icon.RgbaSha256,32,32);foreach(var r in icon.SourceRefs)Source(r.Path,r.Sha256);
            var foot=doc.Footprints.Single(i=>i.SourceSpeciesId==id);Require(foot.Width==16&&foot.Height==16,"Fuchsia footprint differs.");Pixels(foot.TexturePath,foot.RgbaPath,foot.Sha256,foot.RgbaSha256,16,16);Source(foot.Source.Path,foot.Source.Sha256);
        }
        pokedexMenu?.ForProjectRoot(dependency);var dex=pokedexMenu??PretPokedexMenu.Load(dependency);
        Require(doc.Dex.Select(d=>d.SourceSpeciesId).ToHashSet().SetEquals(expected.Keys),"Fuchsia Dex identity differs.");
        foreach(var d in doc.Dex)
        {
            var original=dex.Entries[d.SourceSpeciesId];Require(d.Name==original.Name&&d.Category==original.Category&&d.HeightDecimeters==original.HeightDecimeters&&d.WeightHectograms==original.WeightHectograms&&d.Description==original.Description&&d.Source==original.Source&&d.DescriptionSource==original.DescriptionSource,"Fuchsia Dex differs from full authenticated source.");Source(d.Source.Path,d.Source.Sha256);Source(d.DescriptionSource.Path,d.DescriptionSource.Sha256);
        }
        return new(doc,json.RootElement.Clone(),files.ToImmutable(),dex);
    }
    public byte[] ReadTexture(string projectRelativePath)
    {
        if(!projectRelativePath.StartsWith(GeneratedDirectory,StringComparison.Ordinal)||!Files.TryGetValue(projectRelativePath[GeneratedDirectory.Length..],out var bytes))throw new InvalidDataException("Unknown Fuchsia art texture.");return [..bytes];
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
