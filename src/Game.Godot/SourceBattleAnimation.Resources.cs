using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public sealed record SourceResolvedAnimation(SourceAnimationScript Script,IReadOnlyDictionary<string,SourcePixelAsset> Assets,
    SourceMetallicMaterial? Metallic,SourceStatMaskMaterial? StatMask)
{ public IReadOnlyDictionary<int,SourceBackgroundAsset> Backgrounds{get;init;}=new Dictionary<int,SourceBackgroundAsset>(); }

// Generated metadata is shared across families. Pixel/material loading follows the
// selected command dependencies and remains bounded by these scene-owned catalogs.
public sealed class SourceAnimationResources:IDisposable
{
    private readonly Dictionary<int,SourceAnimationScript> _scripts=[];
    private readonly Dictionary<string,SourceAnimationScript> _generalScripts=[];
    private readonly Dictionary<string,SourceSpriteRecipe> _recipes=[];
    private readonly Dictionary<string,List<(string Folder,JsonDocument Manifest)>> _assetFiles=[];
    private readonly Dictionary<string,SourcePixelAsset> _pixels=[];
    private readonly Dictionary<string,int> _spriteTags=[];
    private readonly List<JsonDocument> _documents=[];
    private readonly Dictionary<string,string> _materials=[];
    private SourceMetallicMaterial? _metallic;
    private SourceStatMaskMaterial? _statMask;
    private bool _disposed;
    private readonly string _folder;
    private SourceAnimationBackgroundCatalog? _backgroundCatalog;
    private readonly Dictionary<int,ushort[]> _terrainPixels=[];
    private SourceAnimationBackgroundCatalog BackgroundCatalog=>_backgroundCatalog??=new(Path.Combine(_folder,"backgrounds"));
    public IReadOnlyDictionary<int,SourceAnimationScript> Scripts=>_scripts;
    public IReadOnlyDictionary<string,SourceAnimationScript> GeneralScripts=>_generalScripts;
    public int CachedAssetCount=>_pixels.Count;
    public SourceAnimationResources(string folder)
    {
        _folder=folder;
        try
        {
            foreach(string file in new[]{"hit/manifest.json","physical.json","elemental.json","background-scripts.json","walls.json","flying.json","fog.json","general.json","trace.json","explosion/manifest.json","stat-moves/manifest.json","stat-mask/manifest.json"})
                ReadPack(Path.Combine(folder,file));
            int[] sine=_documents[0].RootElement.GetProperty("sineTable").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            foreach(var (id,script) in _scripts.ToArray())_scripts[id]=script with{SpriteRecipes=_recipes,SineTable=sine};
            foreach(var (label,script) in _generalScripts.ToArray())_generalScripts[label]=script with{SpriteRecipes=_recipes,SineTable=sine};
            if(_scripts.Count==0)throw new InvalidDataException("Empty source animation resources.");
        }
        catch{Dispose();throw;}
    }
    private static SourceAnimationCommand[] Commands(JsonElement rows)=>rows.EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray();
    private static string RecipeValue(SourceSpriteRecipe recipe)=>JsonSerializer.Serialize(recipe with{LoopStarts=Enumerable.Range(0,recipe.Animations.Length).Select(i=>i<recipe.LoopStarts.Length?recipe.LoopStarts[i]:-1).ToArray(),FrameLoops=Enumerable.Range(0,recipe.Animations.Length).Select(i=>i<recipe.FrameLoops.Length?recipe.FrameLoops[i]:Array.Empty<SourceSpriteFrameLoop>()).ToArray()});
    private void ReadPack(string path)
    {
        string folder=Path.GetDirectoryName(path)!;var doc=JsonDocument.Parse(File.ReadAllBytes(path));_documents.Add(doc);var root=doc.RootElement;
        if(root.TryGetProperty("spriteRecipes",out var recipes))foreach(var (name,recipe) in SourceHitAnimationCatalog.Recipes(recipes))
        {
            if(_recipes.TryGetValue(name,out var previous)){if(RecipeValue(previous)!=RecipeValue(recipe))throw new InvalidDataException($"Conflicting source sprite recipe {name}.");}
            else _recipes.Add(name,recipe);
        }
        foreach(string assetKey in new[]{"assets","indexedAssets"})
        if(root.TryGetProperty(assetKey,out var assets))foreach(var row in assets.EnumerateArray())
        {
            string id=row.GetProperty("id").GetString()!;
            if(!_assetFiles.TryGetValue(id,out var files))_assetFiles.Add(id,files=[]);
            files.Add((folder,doc));
        }
        if(root.TryGetProperty("material",out var material))
        {
            string file=material.GetProperty("path").GetString()!;
            if(Path.GetFileName(file)!=file)throw new InvalidDataException("Material must remain within its generated pack.");
            string callback=root.TryGetProperty("script",out _)?"AnimTask_StatsChange":"AnimTask_MetallicShine";
            _materials.Add(callback,Path.Combine(folder,file));
        }
        var taskAffinePrograms=root.TryGetProperty("taskAffinePrograms",out var affinePrograms)?SourceTaskAffineProgram.Read(affinePrograms):new Dictionary<string,SourceTaskAffineCommand[]>();
        var backgroundSymbols=root.TryGetProperty("backgroundSymbols",out var bgSymbols)?bgSymbols.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetInt32()):new Dictionary<string,int>();
        var subs=root.TryGetProperty("subroutines",out var subRows)?subRows.EnumerateObject().ToDictionary(p=>p.Name,p=>Commands(p.Value)):new Dictionary<string,SourceAnimationCommand[]>();
        bool general=root.TryGetProperty("generalScripts",out var generalRows);
        var rows=general?generalRows.EnumerateArray().ToArray():root.TryGetProperty("scripts",out var scripts)?scripts.EnumerateArray().ToArray():[root.GetProperty("script")];
        foreach(var row in rows)
        {
            var spriteTags=row.TryGetProperty("spriteTags",out var tags)?tags.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetInt32()):new Dictionary<string,int>();
            foreach(var (symbol,tag) in spriteTags){if(_spriteTags.TryGetValue(symbol,out int previous)&&previous!=tag)throw new InvalidDataException("Conflicting source sprite palette tag.");_spriteTags[symbol]=tag;}
            int id=row.TryGetProperty("moveId",out var move)?move.GetInt32():0;var commands=Commands(row.GetProperty("commands"));
            var required=new Dictionary<string,SourceAnimationCommand[]>();
            void AddSubroutines(IEnumerable<SourceAnimationCommand> program)
            {
                foreach(var c in program.Where(c=>c.Op=="call"))
                {string name=c.Args[0];if(required.ContainsKey(name))continue;if(!subs.TryGetValue(name,out var body))throw new InvalidDataException($"Missing source subroutine {name}.");required.Add(name,body);AddSubroutines(body);}
            }
            AddSubroutines(commands);
            var script=new SourceAnimationScript(id,row.GetProperty("label").GetString()!,commands){SpriteTags=spriteTags,SoundEffects=row.TryGetProperty("soundEffects",out var sounds)?sounds.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetInt32()):new Dictionary<string,int>(),TaskAffinePrograms=taskAffinePrograms,BackgroundSymbols=backgroundSymbols,Subroutines=required,Labels=row.TryGetProperty("labels",out var labels)?labels.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetInt32()):new Dictionary<string,int>()};
            if(general)_generalScripts.Add(script.Label,script);else _scripts.Add(id,script);
        }
    }
    public SourcePixelAsset Asset(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_pixels.TryGetValue(id,out var cached))return cached;
        if(!_assetFiles.TryGetValue(id,out var files))throw new InvalidDataException($"Missing generated animation pixels {id}.");
        SourcePixelAsset? result=null;
        foreach(var file in files)
        {
            var indexed=file.Manifest.RootElement.TryGetProperty("indexedAssets",out var indexedRows)?indexedRows.EnumerateArray().Where(r=>r.GetProperty("id").GetString()==id).ToArray():[];
            var asset=indexed.Length==0?SourceHitAnimationCatalog.Asset(file.Manifest,file.Folder,id):SourceIndexedSpriteAssets.Read(file.Folder,indexed.Single(),_spriteTags);
            if(result is not null&&(result.Width!=asset.Width||result.Height!=asset.Height||!result.Pixels.SequenceEqual(asset.Pixels)||result.PaletteTag!=asset.PaletteTag||!result.PaletteIndices.SequenceEqual(asset.PaletteIndices)||!result.Palette.SequenceEqual(asset.Palette)))throw new InvalidDataException($"Conflicting source sprite pixels {id}.");
            result=asset;
        }
        _pixels.Add(id,result!);return result!;
    }
    public SourceResolvedAnimation ResolveGeneral(string label)
    {ObjectDisposedException.ThrowIf(_disposed,this);return Resolve(_generalScripts[label]);}
    public SourceResolvedAnimation Resolve(int move)
    {ObjectDisposedException.ThrowIf(_disposed,this);return Resolve(_scripts[move]);}
    public SourceResolvedAnimation Resolve(SourceAnimationScript script)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        script=script with{SpriteRecipes=_recipes};
        var commands=script.Commands.Concat(script.Subroutines.Values.SelectMany(x=>x)).ToArray();
        var names=new HashSet<string>{"impact"}; // The compositor's shared hit primitive.
        foreach(var c in commands)
        {
            if(c.Op=="createsprite"&&_recipes.TryGetValue(c.Args[0],out var recipe))names.Add(recipe.Asset);
            // These source templates select frames from the shared fist/foot table
            // inside their motion callbacks, so the dependency is implicit.
            if(c.Op=="createsprite"&&c.Args[0] is "gKarateChopSpriteTemplate" or "gStompFootSpriteTemplate" or "gJumpKickSpriteTemplate" or "gSlidingKickSpriteTemplate")names.Add(_recipes["gFistFootSpriteTemplate"].Asset);
            // ElectricBolt creates raw segment objects rather than script sprites.
            if(c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_ElectricBolt"){names.Add("spark-wide");names.Add("spark-thin");}
        }
        bool metallic=commands.Any(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_MetallicShine");
        bool stats=commands.Any(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_StatsChange");
        if(metallic)_metallic??=new(File.ReadAllBytes(_materials["AnimTask_MetallicShine"]));
        if(stats)_statMask??=new(File.ReadAllBytes(_materials["AnimTask_StatsChange"]));
        var backgrounds=commands.Where(c=>c.Op=="fadetobg").Select(c=>script.BackgroundSymbols[c.Args[0]]).Distinct().ToDictionary(id=>id,id=>BackgroundCatalog.Move(id));
        var pixels=names.ToDictionary(id=>id,Asset);
        var spritePalettes=new Dictionary<int,ushort[]>();
        foreach(var asset in pixels.Values.Where(a=>a.PaletteTag>=0)){if(spritePalettes.TryGetValue(asset.PaletteTag,out var previous)&&!previous.SequenceEqual(asset.Palette))throw new InvalidDataException("Conflicting source palette bank.");spritePalettes[asset.PaletteTag]=asset.Palette;}
        return new(script with{SpritePalettes=spritePalettes,BackgroundPalettes=backgrounds.ToDictionary(p=>p.Key,p=>p.Value.Palette.ToArray())},pixels,metallic?_metallic:null,stats?_statMask:null){Backgrounds=backgrounds};
    }
    public SourceAnimationBackgroundSurface CreateBackgroundSurface(int terrainId)
    {ObjectDisposedException.ThrowIf(_disposed,this);return BackgroundCatalog.CreateSurface(terrainId);}
    // Existing scene selection already chose the source terrain. Resolve its exact
    // pixels once at texture composition rather than duplicate regional rules.
    public int IdentifyTerrain(SourcePixelAsset field)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(field.Width!=240||field.Height!=112)throw new ArgumentException("Source terrain canvas differs.");
        int? match=null;
        foreach(int id in BackgroundCatalog.TerrainIds)
        {
            if(!_terrainPixels.TryGetValue(id,out var pixels))
            {pixels=new ushort[240*112];BackgroundCatalog.CreateSurface(id).Render(pixels,240,112);_terrainPixels.Add(id,pixels);}
            if(!pixels.SequenceEqual(field.Pixels))continue;
            if(match is {} previous)
            {
                var first=BackgroundCatalog.Terrain(previous);var second=BackgroundCatalog.Terrain(id);
                if(!first.Tiles.Span.SequenceEqual(second.Tiles.Span)||!first.Tilemap.Span.SequenceEqual(second.Tilemap.Span)||!first.Palette.Span.SequenceEqual(second.Palette.Span))
                    throw new InvalidDataException("Visible terrain cannot identify its retained source background.");
            }
            else match=id;
        }
        return match??throw new InvalidDataException("Displayed terrain has no exact generated indexed source resource.");
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;foreach(var doc in _documents)doc.Dispose();_documents.Clear();_pixels.Clear();_assetFiles.Clear();_scripts.Clear();_generalScripts.Clear();_recipes.Clear();_spriteTags.Clear();_materials.Clear();_metallic=null;_statMask=null;_backgroundCatalog?.Dispose();_backgroundCatalog=null;_terrainPixels.Clear();
    }
}
