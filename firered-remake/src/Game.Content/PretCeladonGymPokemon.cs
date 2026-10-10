using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CeladonGymPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MtMoonEvolutionOverride> EvolutionOverrides, int? NaturalLevelLimit, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record CeladonGymPokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions);

/// <summary>Authenticated four-species Celadon Gym addition and source evolution references; effects execute independently.</summary>
public sealed class PretCeladonGymPokemon
{
    public const string Profile = "firered-celadon-gym-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "298a3f2de294974a4bd0ad40be60f6b8983e0b217a54452355501bd5cbb4298a";
    public const string ManifestSha256 = "c7180712967fb4f180c57dddc69cd9fe1c6c42fe2b75a6004ff8b0f362a7ff4d";
    public const string CatalogSha256 = "289f0e0fbbbd44764c6b4aa15618d9b5f634b4afa8dbfce68a8cb61295a8d511";
    public const string RawManifestSha256 = "c91d50b1ccda90a593ea02179f1f11625fb10a7ce49d5106adb31a50a3e330b6";
    public const string BattleArtSha256 = "c0cba50dccda908a46a782e1ee94b08583c6987adc14a5fb95f9c37fafd043de";
    public const string FullSourceHash = "c33eb70fc14f6d7d56572533135222dcd9cea06bfbdfe07c580254f6bc0577f6";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions { get; }
    public CeladonGymPokemonSupplement Supplement { get; }
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
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement BattleArt { get; }
    public JsonElement Scope { get; }
    public JsonElement OriginalSpecies { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "catalog.json");
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretCeladonGymPokemon(string directory, CeladonGymPokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art,
        CeladonGymPokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs, JsonElement scope)
    {
        using var original = JsonDocument.Parse(outputs["source-species.json"]); OriginalSpecies = original.RootElement.Clone();
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
                Portrait = new("content/generated/celadon-gym-pokemon/" + sprite.TexturePath, sprite.Sha256, sprite.RgbaSha256, 64, 64),
                Footprint = new("content/generated/celadon-gym-pokemon/" + foot.TexturePath, foot.Sha256, foot.RgbaSha256, 16, 16)
            };
        });
        CaptureDexEntries = DexEntries.ToImmutableDictionary(e => e.Key, e =>
        {
            var d = e.Value;
            return new CaptureDexEntry(d.SourceSpeciesId, d.NationalNumber, d.Name, d.Category, d.DisplayCategory,
                d.HeightDecimeters, d.WeightHectograms, d.HeightDisplay, d.WeightDisplay, d.Description,
                d.Footprint!.TexturePath, d.Portrait!.TexturePath, d.HabitatTitle, d.Source, d.DescriptionSource);
        });
        AdditionalSourceHashes = ["celadon-gym-pokemon-manifest:" + ManifestSha256, "celadon-gym-pokemon-lock:" + LockSha256,
            .. files.Select(f => "celadon-gym-pokemon:" + f.Path + ":" + f.Sha256), "celadon-gym-pokemon-dex:" + PretPokedexMenu.GeneratedSha256];
    }


    public static PretCeladonGymPokemon Load(string projectRoot, PretPokedexMenu? pokedexMenu = null, string? dependencyProjectRoot = null)
    {
        var project=Path.GetFullPath(projectRoot);
        var dependency = Path.GetFullPath(dependencyProjectRoot ?? project);
        pokedexMenu?.ForProjectRoot(dependency);
        var inherited=PretCeladonApproachPokemon.Load(dependency,pokedexMenu);
        var source=PretCeladonApproachSource.Load(project,"celadon-gym-pokemon",Profile,LockSha256,75);
        var inventory=source.Files;
        var material=string.Concat(inventory.OrderBy(f=>f.Path,StringComparer.Ordinal).Select(f=>f.Path+"\0"+f.Sha256+"\n"));
        Require(Hash(Utf8.GetBytes(material))==FullSourceHash,"Celadon Pokemon source-set hash differs.");
        _=PretCeladonApproachSource.Read(project,"content/raw/celadon-gym-pokemon/manifest.json",RawManifestSha256);
        var outputs=PretCeladonApproachSource.Outputs(project,"celadon-gym-pokemon",Profile,ManifestSha256,LockSha256,75);
        Require(outputs.Count==53&&Hash(outputs["catalog.json"])==CatalogSha256&&Hash(outputs["battle-art.json"])==BattleArtSha256,"Celadon Pokemon output set differs.");
        var options=ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc=ContentJson.ParseStrict<CeladonGymPokemonDocument>(Utf8.GetString(outputs["catalog.json"]),options);
        Require(doc.SchemaVersion==1&&doc.Profile==Profile&&doc.SourceCommit==SourceCommit&&doc.Inputs.Length==75&&doc.Species.Length==4&&
            doc.Moves.Length==29&&doc.SpeciesNames.Length==4&&doc.MoveNames.Length==29&&doc.EvolutionOverrides.Length==2&&doc.DeferredEvolutions.Length==1&&
            doc.NaturalLevelLimit is null&&!doc.ItemEvolutionExecutorImplemented&&!doc.LevelEvolutionExecutorImplemented&&!doc.TradeEvolutionExecutorImplemented&&
            !doc.Limitations.IsDefaultOrEmpty,"Celadon Pokemon metadata scope differs.");
        Require(doc.Inputs.Select(f=>(f.Path,f.Sha256,f.GitBlobSha1,f.ByteLength)).SequenceEqual(inventory.Select(f=>(f.Path,f.Sha256,f.GitBlobSha1,f.ByteLength))),"Celadon Pokemon source inventory differs.");
        void Source(PokemonSourceRef r)=>source.Reference(r.Path,r.Sha256,r.StartLine,r.EndLine,r.Symbol);
        var expected = new[] { ("VILEPLUME",45),("VICTREEBEL",71),("EXEGGCUTE",102),("TANGELA",114) };
        var species=inherited.Catalog.Species.ToBuilder();
        for (int index=0;index<4;index++)
        {
            var value=doc.Species[index];var identity=expected[index];
            Require(value.Id.Value=="pret:SPECIES_"+identity.Item1 && value.SourceSpeciesId==identity.Item2 &&
                value.Ability1=="ABILITY_CHLOROPHYLL" && value.Ability2=="ABILITY_NONE" && value.Evolutions.IsEmpty,
                "Celadon Gym species source identity differs.");
            Source(value.Source);Source(value.LearnsetSource);foreach(var move in value.LevelUpMoves)Source(move.Source);
            species.Add(value.Id,value);
        }
        var targets = new[] { ("GLOOM","VILEPLUME"),("WEEPINBELL","VICTREEBEL") };
        for (int index=0;index<2;index++)
        {
            var overlay=doc.EvolutionOverrides[index];var prior=new SpeciesId("pret:SPECIES_"+targets[index].Item1);
            Require(overlay.Species==prior && species[prior].Evolutions.IsEmpty && overlay.Evolution.Level==0 &&
                overlay.Evolution.Target.Value=="pret:SPECIES_"+targets[index].Item2 && overlay.Evolution.Method==PokemonEvolutionMethod.Item &&
                overlay.Evolution.Parameter=="ITEM_LEAF_STONE","Celadon Gym source evolution target differs.");
            Source(overlay.Evolution.Source);species[prior]=species[prior] with { Evolutions=[overlay.Evolution] };
        }
        var deferred=doc.DeferredEvolutions.Single();Source(deferred.Source);
        Require(deferred.Species.Value=="pret:SPECIES_EXEGGCUTE" && deferred.Target.Value=="pret:SPECIES_EXEGGUTOR" &&
            deferred.Method=="EVO_ITEM" && deferred.Parameter=="ITEM_LEAF_STONE" && !deferred.Executable,
            "Celadon Gym deferred Exeggcute evolution differs.");
        var moves=inherited.Catalog.Moves.ToBuilder();
        foreach(var m in doc.Moves)
        {
            Source(m.Source);
            if(moves.TryGetValue(m.Id,out var old))Require(JsonSerializer.Serialize(old)==JsonSerializer.Serialize(m),"Inherited move definition changed.");
            else moves.Add(m.Id,m);
        }
        var names=inherited.SpeciesNames.ToBuilder();foreach(var n in doc.SpeciesNames){Source(n.Source);names.Add(species[n.Id].SourceSpeciesId,n.Name);}
        var moveNames=inherited.MoveNames.ToBuilder();foreach(var n in doc.MoveNames)
        {Source(n.Source);Require(!moveNames.TryGetValue(n.Id,out var old)||old==n.Name,"Inherited move name changed.");moveNames[n.Id]=n.Name;}
        var constants=inherited.Constants.ToBuilder();foreach(var c in doc.Constants)
        {Source(c.Source);Require(!constants.TryGetValue(c.Symbol,out int old)||old==c.Value,"Inherited constant changed.");constants[c.Symbol]=c.Value;}
        var encoded=inherited.EncodedNames.ToBuilder();foreach(var n in doc.EncodedNames)
        {
            Source(n.Source);Source(n.EncodingSource);
            Require(n.Bytes.Length==n.Name.Length+1&&n.Bytes[^1]==255&&n.Bytes.Take(n.Bytes.Length-1).All(b=>b is >=0 and <255),"Celadon encoded name differs.");
            Require(!encoded.TryGetValue(n.Symbol,out var old)||JsonSerializer.Serialize(old)==JsonSerializer.Serialize(n),"Inherited encoded name changed.");encoded[n.Symbol]=n;
        }
        var catalog=new PokemonCatalog(SourceCommit,species.ToImmutable(),moves.ToImmutable());
        Require(inherited.Catalog.Species.Count==94&&catalog.Species.Count==98&&catalog.Moves.Count==inherited.Catalog.Moves.Count+5&&catalog.Moves.Count==222&&names.Count==98,"Celadon composed Pokemon scope differs.");
        PokemonFactory.ValidateCatalog(catalog);
        var content=new CeladonGymPokemonCatalogContent(catalog,names.ToImmutable(),moveNames.ToImmutable(),constants.ToImmutable(),encoded.ToImmutable(),inherited.DeferredEvolutions.Where(e=>!doc.EvolutionOverrides.Any(o=>o.Species==e.Species)).Concat(doc.DeferredEvolutions).ToImmutableArray());
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 16 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 16,
            "CeladonGym Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                Hash(outputs[sprite.TexturePath]) == sprite.Sha256 && Hash(outputs[sprite.RgbaPath]) == sprite.RgbaSha256 &&
                outputs[sprite.RgbaPath].Length == 64 * 64 * 4, "CeladonGym Pokemon sprite identity/pixels differ.");
            foreach (var spriteSource in sprite.SourceRefs)
                Require(spriteSource.Kind == "pret" && spriteSource.Repository == "pret/pokefirered" && spriteSource.Commit == SourceCommit &&
                    inventory.Any(f => f.Path == spriteSource.Path && f.Sha256 == spriteSource.Sha256), "CeladonGym sprite provenance differs.");
        }
        var supplement = ContentJson.ParseStrict<CeladonGymPokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(dependency);
        Require(supplement.SchemaVersion == 1 && supplement.Profile == Profile && supplement.SourceCommit == SourceCommit &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexMetadataDependency.Sha256 == PretPokedexMenu.GeneratedSha256 &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 45, 71, 102, 114 }) && supplement.Icons.Length == 4 &&
            supplement.Footprints.Length == 4 && supplement.Cries.Length == 4 && supplement.Weights.Length == 98 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit is null && !supplement.Limitations.IsDefaultOrEmpty,
            "CeladonGym supplemental data scope differs.");
        _ = PretCeladonApproachSource.Read(dependency, supplement.DexMetadataDependency.Path, supplement.DexMetadataDependency.Sha256, supplement.DexMetadataDependency.ByteLength);
        void Reference(StorageArtSourceRef reference)
            => source.Reference(reference.Path,reference.Sha256,reference.StartLine??1,reference.EndLine??reference.StartLine??1,reference.Symbol);
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && icon.SourceRefs.Length == 5 &&
                Hash(outputs[icon.TexturePath]) == icon.Sha256 && Hash(outputs[icon.RgbaPath]) == icon.RgbaSha256 &&
                outputs[icon.RgbaPath].Length == 4096, "CeladonGym icon identity/pixels differ.");
            foreach (var reference in icon.SourceRefs) Reference(reference);
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                Hash(outputs[foot.TexturePath]) == foot.Sha256 && Hash(outputs[foot.RgbaPath]) == foot.RgbaSha256 &&
                outputs[foot.RgbaPath].Length == 1024, "CeladonGym footprint pixels differ.");
            Reference(foot.Source);
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified && !cry.AudioBytesImported &&
                cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 && cry.CrySymbol=="Cry_"+new Dictionary<int,string>{{45,"Vileplume"},{71,"Victreebel"},{102,"Exeggcute"},{114,"Tangela"}}[cry.SourceSpeciesId],
                "CeladonGym cry metadata differs or claims playback.");
            Reference(cry.Source); Reference(cry.MappingSource); Reference(cry.CallerSource);
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "CeladonGym catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "CeladonGym weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "CeladonGym weight differs from the authenticated Dex source.");
            Reference(weight.Source);
        }

        using var scope=JsonDocument.Parse(outputs["scope.json"]);
        var sc=scope.RootElement;
        Require(sc.GetProperty("profile").GetString()==Profile&&sc.GetProperty("trainers").GetArrayLength()==8&&sc.GetProperty("requiredTrainerMoves").GetArrayLength()==17&&
            sc.GetProperty("naturalLevelLimit").ValueKind==JsonValueKind.Null&&!sc.GetProperty("effectsEnabledByThisCatalog").GetBoolean()&&
            sc.GetProperty("newAbilities").GetArrayLength()==0&&sc.GetProperty("newMoveDefinitions").GetArrayLength()==5,"Celadon source scope differs.");
        return new(Path.Combine(project,"content/generated/celadon-gym-pokemon"),content,sprites,inventory,art.RootElement.Clone(),supplement,dex,outputs,sc.Clone());
    }
    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) => Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));
    public StorageArtIcon Icon(SpeciesId species, bool shiny = false) => Icons.Single(i => i.SpeciesId == species.Value);
    public byte[] ReadIconTexture(SpeciesId species, bool shiny = false) => [.. _outputs[Icon(species, shiny).TexturePath]];
    public byte[] ReadFootprintTexture(int sourceSpeciesId) => [.. _outputs[Footprints.Single(f => f.SourceSpeciesId == sourceSpeciesId).TexturePath]];
    public byte[] ReadSpriteTexture(SpeciesId species, string side, bool shiny = false) => [.. _outputs[Sprite(species, side, shiny).TexturePath]];
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe CeladonGym Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("CeladonGym Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("CeladonGym Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record CeladonGymPokemonSupplement(int SchemaVersion, string Profile, string SourceCommit,
    PokedexMenuDependency DexMetadataDependency, ImmutableArray<int> DexSpeciesIds, ImmutableArray<StorageArtIcon> Icons,
    ImmutableArray<Route3PokemonFootprint> Footprints, ImmutableArray<Route3PokemonCry> Cries,
    ImmutableArray<Route3PokemonWeight> Weights, bool NormalShinyIconsIdentical, int? NaturalLevelLimit, ImmutableArray<string> Limitations);
