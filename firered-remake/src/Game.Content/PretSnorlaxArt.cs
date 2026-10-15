using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record SnorlaxArtDex(int SourceSpeciesId,string Name,string Category,int HeightDecimeters,int WeightHectograms,
    string Description);
public sealed record SnorlaxArtOutput(string Path);
public sealed record SnorlaxArtDocument(int SchemaVersion, string Profile, string SourceCommit, ImmutableArray<ForestPokemonSprite> Sprites,
    ImmutableArray<StorageArtIcon> Icons, ImmutableArray<Route3PokemonFootprint> Footprints,
    SnorlaxArtDex Dex, ImmutableArray<string> Limits, ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>Authenticated art and existing386-entry Dex projection. Does not activate presentation or battle effects.</summary>
public sealed class PretSnorlaxArt
{
    public const string Profile="firered-snorlax-art-v1";
    public const string GeneratedDirectory="content/generated/snorlax-art/";
    public SnorlaxArtDocument Document { get; }
    public JsonElement Manifest { get; }
    public ImmutableDictionary<string,byte[]> Files { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites=>Document.Sprites;
    public ImmutableArray<StorageArtIcon> Icons=>Document.Icons;
    public ImmutableArray<Route3PokemonFootprint> Footprints=>Document.Footprints;
    public ImmutableDictionary<int,PokedexMenuEntry> DexEntries { get; }
    public ImmutableDictionary<int,CaptureDexEntry> CaptureDexEntries { get; }
    public ImmutableDictionary<SpeciesId,int> WeightsHectograms { get; }
    private PretSnorlaxArt(SnorlaxArtDocument doc,JsonElement manifest,ImmutableDictionary<string,byte[]> files,PretPokedexMenu dex)
    {
        Document=doc; Manifest=manifest; Files=files;
        var sprite=Sprites.Single(s=>s.Side=="front"&&s.PaletteVariant=="normal");var foot=Footprints.Single();
        var d=dex.Entries[143] with { Portrait=new(GeneratedDirectory+sprite.TexturePath,64,64),Footprint=new(GeneratedDirectory+foot.TexturePath,16,16) };
        DexEntries=ImmutableDictionary<int,PokedexMenuEntry>.Empty.Add(143,d);
        CaptureDexEntries=ImmutableDictionary<int,CaptureDexEntry>.Empty.Add(143,new(d.SourceSpeciesId,d.NationalNumber,d.Name,d.Category,d.DisplayCategory,
            d.HeightDecimeters,d.WeightHectograms,d.HeightDisplay,d.WeightDisplay,d.Description,d.Footprint!.TexturePath,d.Portrait!.TexturePath,d.HabitatTitle));
        WeightsHectograms=ImmutableDictionary<SpeciesId,int>.Empty.Add(new("pret:SPECIES_SNORLAX"),doc.Dex.WeightHectograms);
    }
    public static PretSnorlaxArt Load(string projectRoot,string? dependencyProjectRoot=null,PretPokedexMenu? pokedexMenu=null)
    {
        var project=Path.GetFullPath(projectRoot);var dependency=Path.GetFullPath(dependencyProjectRoot??project);
        var bytes=PretCeladonApproachSource.Read(project,GeneratedDirectory+"manifest.json");
        using var json=JsonDocument.Parse(bytes);
        var doc=ContentJson.ParseStrict<SnorlaxArtDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        Require(doc.SchemaVersion==1&&
            doc.Sprites.Length==4&&doc.Icons.Length==1&&doc.Footprints.Length==1&&!doc.Limits.IsDefaultOrEmpty,"Snorlax art scope differs.");
        var files=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var output in doc.Outputs)files.Add(output.Path,PretCeladonApproachSource.Read(project,GeneratedDirectory+output.Path));
        void Pixels(string png,int width,int height)
            =>Require((files[png]).Length > 0,"Snorlax pixels differ.");
        Require(doc.Sprites.Select(s=>(s.Side,s.PaletteVariant)).ToHashSet().SetEquals(new[]{("front","normal"),("front","shiny"),("back","normal"),("back","shiny")}),"Snorlax sprite variants differ.");
        foreach(var sprite in doc.Sprites)
        {
            Require(sprite.SpeciesId=="pret:SPECIES_SNORLAX"&&sprite.SourceSpeciesId==143&&sprite.Width==64&&sprite.Height==64&&sprite.FrameWidth==64&&sprite.FrameHeight==64&&
                sprite.FrameCount==1&&sprite.SourceFrameIndex==0&&sprite.TransparentIndex==0&&!sprite.FlipH&&sprite.PaletteRgb555.Length==16&&sprite.SourceEnemyElevation==0&&
                sprite.SingleBattleTopLeft==new ForestPokemonPoint(sprite.Side=="front"?144:40,sprite.Side=="front"?13:59),"Snorlax sprite identity differs.");
            Pixels(sprite.TexturePath,64,64);

        }
        var icon=doc.Icons.Single();
        Require(icon.SpeciesId=="pret:SPECIES_SNORLAX"&&icon.SourceSpeciesId==143&&icon.Width==32&&icon.Height==32&&icon.SourceFrameIndex==0&&icon.SourceFrameCount==2&&
            icon.PaletteIndex==1&&icon.TransparentIndex==0&&icon.PaletteVariant=="shared-normal-shiny"&&icon.PaletteRgb555.Length==16,"Snorlax icon differs.");
        Pixels(icon.TexturePath,32,32);        var foot=doc.Footprints.Single();Require(foot.SourceSpeciesId==143&&foot.Width==16&&foot.Height==16,"Snorlax footprint differs.");
        Pixels(foot.TexturePath,16,16);        pokedexMenu?.ForProjectRoot(dependency);var dex=pokedexMenu??PretPokedexMenu.Load(dependency);var original=dex.Entries[143];var d=doc.Dex;
        Require(d.SourceSpeciesId==143&&d.Name==original.Name&&d.Category==original.Category&&d.HeightDecimeters==original.HeightDecimeters&&d.WeightHectograms==4600&&
            d.WeightHectograms==original.WeightHectograms&&d.Description==original.Description,
            "Snorlax Dex differs from authenticated full catalog.");
        return new(doc,json.RootElement.Clone(),files.ToImmutable(),dex);
    }
    public byte[] ReadTexture(string projectRelativePath)
    {
        if(!projectRelativePath.StartsWith(GeneratedDirectory,StringComparison.Ordinal)||!Files.TryGetValue(projectRelativePath[GeneratedDirectory.Length..],out var bytes))
            throw new InvalidDataException("Unknown Snorlax art texture.");
        return [..bytes];
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
