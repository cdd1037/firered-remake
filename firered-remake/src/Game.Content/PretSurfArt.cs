using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record SurfArtDocument(int SchemaVersion, string Profile, string SourceCommit, ImmutableArray<ForestPokemonSprite> Sprites,
    ImmutableArray<StorageArtIcon> Icons, ImmutableArray<Route3PokemonFootprint> Footprints,
    ImmutableArray<SnorlaxArtDex> Dex, ImmutableArray<string> Limits,
    ImmutableArray<SnorlaxArtOutput> Outputs);
/// <summary>Authenticated two-species normal/shiny sprites, source icons and footprints; no battle executor or audio activation.</summary>
public sealed class PretSurfArt
{
    public const string Profile="firered-surf-art-v1";
    public const string GeneratedDirectory="content/generated/surf-art/";
    public SurfArtDocument Document { get; }
    public JsonElement Manifest { get; }
    public ImmutableDictionary<string,byte[]> Files { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites=>Document.Sprites;
    public ImmutableArray<StorageArtIcon> Icons=>Document.Icons;
    public ImmutableArray<Route3PokemonFootprint> Footprints=>Document.Footprints;
    public ImmutableDictionary<int,PokedexMenuEntry> DexEntries { get; }
    public ImmutableDictionary<int,CaptureDexEntry> CaptureDexEntries { get; }
    public ImmutableDictionary<SpeciesId,int> WeightsHectograms { get; }
    private PretSurfArt(SurfArtDocument doc,JsonElement manifest,ImmutableDictionary<string,byte[]> files,PretPokedexMenu dex)
    {
        Document=doc;Manifest=manifest;Files=files;
        var entries=ImmutableDictionary.CreateBuilder<int,PokedexMenuEntry>();var capture=ImmutableDictionary.CreateBuilder<int,CaptureDexEntry>();var weights=ImmutableDictionary.CreateBuilder<SpeciesId,int>();
        foreach(var item in doc.Dex)
        {
            int id=item.SourceSpeciesId;var sprite=Sprites.Single(s=>s.SourceSpeciesId==id&&s.Side=="front"&&s.PaletteVariant=="normal");var foot=Footprints.Single(f=>f.SourceSpeciesId==id);
            var d=dex.Entries[id] with{Portrait=new(GeneratedDirectory+sprite.TexturePath,64,64),Footprint=new(GeneratedDirectory+foot.TexturePath,16,16)};
            entries.Add(id,d);capture.Add(id,new(d.SourceSpeciesId,d.NationalNumber,d.Name,d.Category,d.DisplayCategory,d.HeightDecimeters,d.WeightHectograms,d.HeightDisplay,d.WeightDisplay,d.Description,d.Footprint!.TexturePath,d.Portrait!.TexturePath,d.HabitatTitle));weights.Add(new(sprite.SpeciesId),d.WeightHectograms);
        }
        DexEntries=entries.ToImmutable();CaptureDexEntries=capture.ToImmutable();WeightsHectograms=weights.ToImmutable();
    }
    public static PretSurfArt Load(string projectRoot,string? dependencyProjectRoot=null,PretPokedexMenu? pokedexMenu=null)
    {
        var root=Path.GetFullPath(projectRoot);var dependency=Path.GetFullPath(dependencyProjectRoot??root);
        var bytes=SafariSource.Read(root,GeneratedDirectory+"manifest.json");using var json=JsonDocument.Parse(bytes);var doc=SafariSource.Parse<SurfArtDocument>(bytes);
        Require(doc.SchemaVersion==1&&doc.Sprites.Length==8&&doc.Icons.Length==2&&doc.Footprints.Length==2&&doc.Dex.Length==2&&!doc.Limits.IsDefaultOrEmpty,"Surf art scope differs.");
        var expected=new Dictionary<int,string>{{54,"PSYDUCK"},{55,"GOLDUCK"}};var files=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var o in doc.Outputs)files.Add(o.Path,SafariSource.Read(root,GeneratedDirectory+o.Path));
        void Pixels(string png,int w,int h)=>Require((files[png]).Length > 0,"Surf pixels differ.");
        foreach(var (id,name) in expected)
        {
            var ss=doc.Sprites.Where(s=>s.SourceSpeciesId==id).ToArray();Require(ss.Length==4&&ss.Select(s=>(s.Side,s.PaletteVariant)).ToHashSet().SetEquals(new[]{("front","normal"),("front","shiny"),("back","normal"),("back","shiny")}),"Surf sprite variants differ.");
            foreach(var s in ss)
            {
                Require(s.SpeciesId=="pret:SPECIES_"+name&&s.Width==64&&s.Height==64&&s.FrameWidth==64&&s.FrameHeight==64&&s.FrameCount==1&&s.SourceFrameIndex==0&&s.TransparentIndex==0&&!s.FlipH&&s.PaletteRgb555.Length==16,"Surf sprite identity differs.");Pixels(s.TexturePath,64,64);
            }
            var icon=doc.Icons.Single(i=>i.SourceSpeciesId==id);Require(icon.SpeciesId=="pret:SPECIES_"+name&&icon.Width==32&&icon.Height==32&&icon.SourceFrameIndex==0&&icon.SourceFrameCount==2&&icon.TransparentIndex==0&&icon.PaletteVariant=="shared-normal-shiny"&&icon.PaletteRgb555.Length==16,"Surf icon differs.");Pixels(icon.TexturePath,32,32);            var foot=doc.Footprints.Single(i=>i.SourceSpeciesId==id);Require(foot.Width==16&&foot.Height==16,"Surf footprint differs.");Pixels(foot.TexturePath,16,16);        }
        pokedexMenu?.ForProjectRoot(dependency);var dex=pokedexMenu??PretPokedexMenu.Load(dependency);
        Require(doc.Dex.Select(d=>d.SourceSpeciesId).ToHashSet().SetEquals(expected.Keys),"Surf Dex identity differs.");
        foreach(var d in doc.Dex)
        {
            var original=dex.Entries[d.SourceSpeciesId];Require(d.Name==original.Name&&d.Category==original.Category&&d.HeightDecimeters==original.HeightDecimeters&&d.WeightHectograms==original.WeightHectograms&&d.Description==original.Description,"Surf Dex differs from full authenticated source.");        }
        return new(doc,json.RootElement.Clone(),files.ToImmutable(),dex);
    }
    public byte[] ReadTexture(string projectRelativePath)
    {
        if(!projectRelativePath.StartsWith(GeneratedDirectory,StringComparison.Ordinal)||!Files.TryGetValue(projectRelativePath[GeneratedDirectory.Length..],out var bytes))throw new InvalidDataException("Unknown Surf art texture.");return [..bytes];
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
