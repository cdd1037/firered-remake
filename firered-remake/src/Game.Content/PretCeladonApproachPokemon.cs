// Reconstructed Content source; compilation awaits authentic RockTunnel93 dependencies.
// Authenticated CeladonApproach additions; all historical catalogs remain unchanged.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CeladonApproachPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MtMoonEvolutionOverride> EvolutionOverrides, int? NaturalLevelLimit, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record CeladonApproachPokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions);

/// <summary>Authenticated Muk addition and source evolution references; effects execute independently.</summary>
public sealed class PretCeladonApproachPokemon
{
    public const string Profile = "firered-celadon-approach-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions { get; }
    public CeladonApproachPokemonSupplement Supplement { get; }
    public ImmutableArray<StorageArtIcon> Icons => Supplement.Icons;
    public ImmutableArray<Route3PokemonFootprint> Footprints => Supplement.Footprints;
    public ImmutableArray<Route3PokemonCry> Cries => Supplement.Cries;
    public ImmutableDictionary<SpeciesId, int> WeightsHectograms { get; }
    public ImmutableDictionary<int, PokedexMenuEntry> DexEntries { get; }
    public PretPokedexMenu PokedexMenu { get; }
    public ImmutableDictionary<int, CaptureDexEntry> CaptureDexEntries { get; }
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement BattleArt { get; }
    public JsonElement Scope { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "catalog.json");
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretCeladonApproachPokemon(string directory, CeladonApproachPokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art,
        CeladonApproachPokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs, JsonElement scope)
    {
        Scope = scope; GeneratedDirectory = directory; Catalog = content.Catalog; SpeciesNames = content.SpeciesNames;
        MoveNames = content.MoveNames; Constants = content.Constants; EncodedNames = content.EncodedNames;
        Sprites = sprites; SourceFiles = files; BattleArt = art; Supplement = supplement;
        PokedexMenu = dex; _outputs = outputs; DeferredEvolutions = content.DeferredEvolutions;
        WeightsHectograms = supplement.Weights.ToImmutableDictionary(w => new SpeciesId(w.SpeciesId), w => w.WeightHectograms);
        DexEntries = supplement.DexSpeciesIds.ToImmutableDictionary(sid => sid, sid =>
        {
            var sprite = sprites.Single(s => s.SourceSpeciesId == sid && s.Side == "front" && s.PaletteVariant == "normal");
            var foot = supplement.Footprints.Single(f => f.SourceSpeciesId == sid);
            return dex.Entries[sid] with
            {
                Portrait = new("content/generated/celadon-approach-pokemon/" + sprite.TexturePath,64,64),Footprint = new("content/generated/celadon-approach-pokemon/" + foot.TexturePath,16,16)
            };
        });
        CaptureDexEntries = DexEntries.ToImmutableDictionary(e => e.Key, e =>
        {
            var d = e.Value;
            return new CaptureDexEntry(d.SourceSpeciesId, d.NationalNumber, d.Name, d.Category, d.DisplayCategory,
                d.HeightDecimeters, d.WeightHectograms, d.HeightDisplay, d.WeightDisplay, d.Description,
                d.Footprint!.TexturePath, d.Portrait!.TexturePath, d.HabitatTitle);
        });
    }


    public static PretCeladonApproachPokemon Load(string projectRoot, PretPokedexMenu? pokedexMenu = null)
    {
        var project=Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var inherited=PretRockTunnelPokemon.Load(project,pokedexMenu);
        var outputs=PretCeladonApproachSource.Outputs(project,"celadon-approach-pokemon");
        var options=ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc=ContentJson.ParseStrict<CeladonApproachPokemonDocument>(Utf8.GetString(outputs["catalog.json"]),options);
        Require(doc.SchemaVersion==1&&doc.EvolutionOverrides.Length==1&&doc.DeferredEvolutions.IsEmpty&&
            doc.NaturalLevelLimit is null&&!doc.ItemEvolutionExecutorImplemented&&!doc.LevelEvolutionExecutorImplemented&&!doc.TradeEvolutionExecutorImplemented&&
            !doc.Limitations.IsDefaultOrEmpty,"Celadon Pokemon metadata scope differs.");
        var muk=doc.Species.Single();
        Require(muk.Id.Value=="pret:SPECIES_MUK"&&muk.SourceSpeciesId==89&&muk.Ability1=="ABILITY_STENCH"&&muk.Ability2=="ABILITY_STICKY_HOLD"&&muk.Evolutions.IsEmpty&&
            muk.LevelUpMoves.Select(m=>(m.Level,m.Move.Value)).SequenceEqual(new[]{(1,"POISON_GAS"),(1,"POUND"),(1,"HARDEN"),(4,"HARDEN"),(8,"DISABLE"),(13,"SLUDGE"),(19,"MINIMIZE"),(26,"SCREECH"),(34,"ACID_ARMOR"),(47,"SLUDGE_BOMB"),(61,"MEMENTO")}.Select(m=>(m.Item1,"pret:MOVE_"+m.Item2))),"Muk full source learnset differs.");
        var species=inherited.Catalog.Species.ToBuilder();species.Add(muk.Id,muk);
        var overlay=doc.EvolutionOverrides.Single();var grimer=new SpeciesId("pret:SPECIES_GRIMER");
        Require(overlay.Species==grimer&&species[grimer].Evolutions.IsEmpty&&overlay.Evolution.Level==38&&overlay.Evolution.Target==muk.Id&&
            overlay.Evolution.Method==PokemonEvolutionMethod.Level&&overlay.Evolution.Parameter is null,"Grimer source evolution overlay differs.");
species[grimer]=species[grimer] with{Evolutions=[overlay.Evolution]};
        var moves=inherited.Catalog.Moves.ToBuilder();
        foreach(var m in doc.Moves)
        {
            if(moves.TryGetValue(m.Id,out var old))Require(JsonSerializer.Serialize(old)==JsonSerializer.Serialize(m),"Inherited move definition changed.");
            else moves.Add(m.Id,m);
        }
        var names=inherited.SpeciesNames.ToBuilder();foreach(var n in doc.SpeciesNames){names.Add(species[n.Id].SourceSpeciesId,n.Name);}
        var moveNames=inherited.MoveNames.ToBuilder();foreach(var n in doc.MoveNames)
        {Require(!moveNames.TryGetValue(n.Id,out var old)||old==n.Name,"Inherited move name changed.");moveNames[n.Id]=n.Name;}
        var constants=inherited.Constants.ToBuilder();foreach(var c in doc.Constants)
        {Require(!constants.TryGetValue(c.Symbol,out int old)||old==c.Value,"Inherited constant changed.");constants[c.Symbol]=c.Value;}
        var encoded=inherited.EncodedNames.ToBuilder();foreach(var n in doc.EncodedNames)
        {
            Require(n.Bytes.Length==n.Name.Length+1&&n.Bytes[^1]==255&&n.Bytes.Take(n.Bytes.Length-1).All(b=>b is >=0 and <255),"Celadon encoded name differs.");
            Require(!encoded.TryGetValue(n.Symbol,out var old)||JsonSerializer.Serialize(old)==JsonSerializer.Serialize(n),"Inherited encoded name changed.");encoded[n.Symbol]=n;
        }
        var catalog=new PokemonCatalog(SourceCommit,species.ToImmutable(),moves.ToImmutable());
        Require(inherited.Catalog.Species.Count==93&&catalog.Species.Count==94&&catalog.Moves.Count==inherited.Catalog.Moves.Count&&catalog.Moves.Count==217&&names.Count==94,"Celadon composed Pokemon scope differs.");
        PokemonFactory.ValidateCatalog(catalog);
        var content=new CeladonApproachPokemonCatalogContent(catalog,names.ToImmutable(),moveNames.ToImmutable(),constants.ToImmutable(),encoded.ToImmutable(),inherited.DeferredEvolutions.Where(e=>e.Species!=grimer).ToImmutableArray());
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 4 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 4,
            "CeladonApproach Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "CeladonApproach Pokemon sprite identity/pixels differ.");
            foreach (var spriteSource in sprite.SourceRefs)
                Require(spriteSource.Kind == "pret" , "CeladonApproach sprite provenance differs.");
        }
        var supplement = ContentJson.ParseStrict<CeladonApproachPokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 89 }) && supplement.Icons.Length == 1 &&
            supplement.Footprints.Length == 1 && supplement.Cries.Length == 1 && supplement.Weights.Length == 94 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit is null && !supplement.Limitations.IsDefaultOrEmpty,
            "CeladonApproach supplemental data scope differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 &&
                (outputs[icon.TexturePath]).Length > 0, "CeladonApproach icon identity/pixels differ.");
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                (outputs[foot.TexturePath]).Length > 0, "CeladonApproach footprint pixels differ.");
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified && !cry.AudioBytesImported &&
                cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 && cry.CrySymbol=="Cry_Muk",
                "CeladonApproach cry metadata differs or claims playback.");
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "CeladonApproach catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "CeladonApproach weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "CeladonApproach weight differs from the authenticated Dex source.");
        }

        using var scope=JsonDocument.Parse(outputs["scope.json"]);
        var sc=scope.RootElement;
        Require(sc.GetProperty("trainers").GetArrayLength()==11&&sc.GetProperty("requiredTrainerMoves").GetArrayLength()==44&&
            sc.GetProperty("naturalLevelLimit").ValueKind==JsonValueKind.Null&&!sc.GetProperty("effectsEnabledByThisCatalog").GetBoolean()&&
            sc.GetProperty("newAbilities").GetArrayLength()==0&&sc.GetProperty("newMoveDefinitions").GetArrayLength()==0,"Celadon source scope differs.");
        return new(Path.Combine(project,"content/generated/celadon-approach-pokemon"),content,sprites,doc.Inputs.Select(i => new PretWorldSourceFile(i.Path)).ToImmutableArray(),art.RootElement.Clone(),supplement,dex,outputs,sc.Clone());
    }
    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) => Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));
    public StorageArtIcon Icon(SpeciesId species, bool shiny = false) => Icons.Single(i => i.SpeciesId == species.Value);
    public byte[] ReadIconTexture(SpeciesId species, bool shiny = false) => [.. _outputs[Icon(species, shiny).TexturePath]];
    public byte[] ReadFootprintTexture(int sourceSpeciesId) => [.. _outputs[Footprints.Single(f => f.SourceSpeciesId == sourceSpeciesId).TexturePath]];
    public byte[] ReadSpriteTexture(SpeciesId species, string side, bool shiny = false) => [.. _outputs[Sprite(species, side, shiny).TexturePath]];
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe CeladonApproach Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("CeladonApproach Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("CeladonApproach Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record CeladonApproachPokemonSupplement(int SchemaVersion, string Profile, string SourceCommit,
    PokedexMenuDependency DexMetadataDependency, ImmutableArray<int> DexSpeciesIds, ImmutableArray<StorageArtIcon> Icons,
    ImmutableArray<Route3PokemonFootprint> Footprints, ImmutableArray<Route3PokemonCry> Cries,
    ImmutableArray<Route3PokemonWeight> Weights, bool NormalShinyIconsIdentical, int? NaturalLevelLimit, ImmutableArray<string> Limitations);
