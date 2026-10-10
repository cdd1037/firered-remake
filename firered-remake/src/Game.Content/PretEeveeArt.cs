using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record EeveeArtDocument(int SchemaVersion,string Profile,string SourceCommit,string LockSha256,
    ImmutableArray<ForestPokemonSprite> Sprites,ImmutableArray<StorageArtIcon> Icons,ImmutableArray<Route3PokemonFootprint> Footprints,
    SnorlaxArtDex Dex,ImmutableArray<string> Limits,ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>Authenticated art and existing386-entry Dex projection. Does not activate presentation or battle effects.</summary>
public sealed class PretEeveeArt
{
    public const string Profile="firered-eevee-art-v1";
    public const string ManifestSha256="86ebee3091354a0c33b478d3659f571c2e5708a58fde95e50980805c2c093e57";
    public const string GeneratedDirectory="content/generated/eevee-art/";
    public EeveeArtDocument Document { get; }
    public JsonElement Manifest { get; }
    public ImmutableDictionary<string,byte[]> Files { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites=>Document.Sprites;
    public ImmutableArray<StorageArtIcon> Icons=>Document.Icons;
    public ImmutableArray<Route3PokemonFootprint> Footprints=>Document.Footprints;
    public ImmutableDictionary<int,PokedexMenuEntry> DexEntries { get; }
    public ImmutableDictionary<int,CaptureDexEntry> CaptureDexEntries { get; }
    public ImmutableDictionary<SpeciesId,int> WeightsHectograms { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretEeveeArt(EeveeArtDocument doc,JsonElement manifest,ImmutableDictionary<string,byte[]> files,PretPokedexMenu dex)
    {
        Document=doc; Manifest=manifest; Files=files;
        var sprite=Sprites.Single(s=>s.Side=="front"&&s.PaletteVariant=="normal");var foot=Footprints.Single();
        var d=dex.Entries[133] with { Portrait=new(GeneratedDirectory+sprite.TexturePath,sprite.Sha256,sprite.RgbaSha256,64,64),
            Footprint=new(GeneratedDirectory+foot.TexturePath,foot.Sha256,foot.RgbaSha256,16,16) };
        DexEntries=ImmutableDictionary<int,PokedexMenuEntry>.Empty.Add(133,d);
        CaptureDexEntries=ImmutableDictionary<int,CaptureDexEntry>.Empty.Add(133,new(d.SourceSpeciesId,d.NationalNumber,d.Name,d.Category,d.DisplayCategory,
            d.HeightDecimeters,d.WeightHectograms,d.HeightDisplay,d.WeightDisplay,d.Description,d.Footprint!.TexturePath,d.Portrait!.TexturePath,d.HabitatTitle,d.Source,d.DescriptionSource));
        WeightsHectograms=ImmutableDictionary<SpeciesId,int>.Empty.Add(new("pret:SPECIES_EEVEE"),doc.Dex.WeightHectograms);
        AdditionalSourceHashes=["eevee-art-manifest:"+ManifestSha256,"eevee-art-source-lock:"+PretEeveePokemon.LockSha256,
            "eevee-art-dex:"+PretPokedexMenu.GeneratedSha256,..doc.Outputs.Select(o=>"eevee-art:"+o.Path+":"+o.Sha256)];
    }
    public static PretEeveeArt Load(string projectRoot,string? dependencyProjectRoot=null,PretPokedexMenu? pokedexMenu=null)
    {
        var project=Path.GetFullPath(projectRoot);var dependency=Path.GetFullPath(dependencyProjectRoot??project);
        var rawLock=PretCeladonApproachSource.Read(project,"scripts/eevee-pokemon.lock.json",PretEeveePokemon.LockSha256,7991);
        using var sourceLock=JsonDocument.Parse(rawLock);var inputs=sourceLock.RootElement.GetProperty("files");
        Require(inputs.GetArrayLength()==33,"Eevee art source inventory differs.");
        var sources=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in inputs.EnumerateArray())
        {
            string path=row.GetProperty("path").GetString()!,hash=row.GetProperty("sha256").GetString()!;
            var source=PretCeladonApproachSource.Read(project,"content/raw/eevee-pokemon/"+path,hash,row.GetProperty("byteLength").GetInt32());
            Require(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {source.Length}\0"),..source]))==row.GetProperty("gitBlobSha1").GetString(),"Eevee art Git blob differs.");
            sources.Add(path,hash);
        }
        var bytes=PretCeladonApproachSource.Read(project,GeneratedDirectory+"manifest.json",ManifestSha256,15491);
        using var json=JsonDocument.Parse(bytes);
        var doc=ContentJson.ParseStrict<EeveeArtDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        Require(doc.SchemaVersion==1&&doc.Profile==Profile&&doc.SourceCommit==PretEeveePokemon.SourceCommit&&doc.LockSha256==PretEeveePokemon.LockSha256&&
            doc.Sprites.Length==4&&doc.Icons.Length==1&&doc.Footprints.Length==1&&doc.Outputs.Length==12&&!doc.Limits.IsDefaultOrEmpty,"Eevee art scope differs.");
        var files=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var output in doc.Outputs)files.Add(output.Path,PretCeladonApproachSource.Read(project,GeneratedDirectory+output.Path,output.Sha256,output.ByteLength));
        void Source(string path,string hash)=>Require(sources.TryGetValue(path,out var expected)&&expected==hash,"Eevee art source reference differs.");
        void Pixels(string png,string rgba,string pngHash,string rgbaHash,int width,int height)
            =>Require(files.ContainsKey(png)&&files.ContainsKey(rgba)&&Hash(files[png])==pngHash&&Hash(files[rgba])==rgbaHash&&files[rgba].Length==width*height*4,"Eevee pixels differ.");
        Require(doc.Sprites.Select(s=>(s.Side,s.PaletteVariant)).ToHashSet().SetEquals(new[]{("front","normal"),("front","shiny"),("back","normal"),("back","shiny")}),"Eevee sprite variants differ.");
        foreach(var sprite in doc.Sprites)
        {
            Require(sprite.SpeciesId=="pret:SPECIES_EEVEE"&&sprite.SourceSpeciesId==133&&sprite.Width==64&&sprite.Height==64&&sprite.FrameWidth==64&&sprite.FrameHeight==64&&
                sprite.FrameCount==1&&sprite.SourceFrameIndex==0&&sprite.TransparentIndex==0&&!sprite.FlipH&&sprite.PaletteRgb555.Length==16&&sprite.SourceEnemyElevation==0&&
                sprite.SingleBattleTopLeft==new ForestPokemonPoint(sprite.Side=="front"?144:40,sprite.Side=="front"?21:58),"Eevee sprite identity differs.");
            Pixels(sprite.TexturePath,sprite.RgbaPath,sprite.Sha256,sprite.RgbaSha256,64,64);
            foreach(var r in sprite.SourceRefs){Require(r.Kind=="pret"&&r.Repository=="pret/pokefirered"&&r.Commit==PretEeveePokemon.SourceCommit,"Eevee art provenance differs.");Source(r.Path,r.Sha256);}
        }
        var icon=doc.Icons.Single();
        Require(icon.SpeciesId=="pret:SPECIES_EEVEE"&&icon.SourceSpeciesId==133&&icon.Width==32&&icon.Height==32&&icon.SourceFrameIndex==0&&icon.SourceFrameCount==2&&
            icon.PaletteIndex==2&&icon.TransparentIndex==0&&icon.PaletteVariant=="shared-normal-shiny"&&icon.PaletteRgb555.Length==16,"Eevee icon differs.");
        Pixels(icon.TexturePath,icon.RgbaPath,icon.Sha256,icon.RgbaSha256,32,32);foreach(var r in icon.SourceRefs)Source(r.Path,r.Sha256);
        var foot=doc.Footprints.Single();Require(foot.SourceSpeciesId==133&&foot.Width==16&&foot.Height==16,"Eevee footprint differs.");
        Pixels(foot.TexturePath,foot.RgbaPath,foot.Sha256,foot.RgbaSha256,16,16);Source(foot.Source.Path,foot.Source.Sha256);
        pokedexMenu?.ForProjectRoot(dependency);var dex=pokedexMenu??PretPokedexMenu.Load(dependency);var original=dex.Entries[133];var d=doc.Dex;
        Require(d.SourceSpeciesId==133&&d.Name==original.Name&&d.Category==original.Category&&d.HeightDecimeters==original.HeightDecimeters&&d.WeightHectograms==65&&
            d.WeightHectograms==original.WeightHectograms&&d.Description==original.Description&&d.Source==original.Source&&d.DescriptionSource==original.DescriptionSource,
            "Eevee Dex differs from authenticated full catalog.");
        Source(d.Source.Path,d.Source.Sha256);Source(d.DescriptionSource.Path,d.DescriptionSource.Sha256);
        return new(doc,json.RootElement.Clone(),files.ToImmutable(),dex);
    }
    public byte[] ReadTexture(string projectRelativePath)
    {
        if(!projectRelativePath.StartsWith(GeneratedDirectory,StringComparison.Ordinal)||!Files.TryGetValue(projectRelativePath[GeneratedDirectory.Length..],out var bytes))
            throw new InvalidDataException("Unknown Eevee art texture.");
        return [..bytes];
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
