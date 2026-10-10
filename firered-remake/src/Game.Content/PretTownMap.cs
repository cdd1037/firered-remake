using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

/// <summary>Authenticated Kanto Town Map, source gift metadata and bounded first-badge position projection.</summary>
public sealed class PretTownMap
{
    public const string Profile = "firered-town-map-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "3ab8437695dac622a2affc5239acde4a97525697d8810e0af3b55c41dc9d8876";
    public const string ManifestSha256 = "32472d171f32006e5d33b97d348b637b134c197f3eacda26fa0bad21b56ae16d";
    public const string RawManifestSha256 = "81e304c6e0c1833bcffb7d97e3641b8c80adcef467cf238a5f16075b6d35955e";
    public const string TownMapSha256 = "632750eb014d5912beac3b68d0783a73f050c1cb0d7cb264f1739dfb2d40bdb0";
    public const string FullSourceHash = "f1dbfb165c5a21d531c34d400ff93c218d34193fb5c84fd345959a2790f0d0c5";
    private static readonly string[] AssetIds = ["kanto","cursor-0","cursor-1","player-red","player-leaf","dungeon-unvisited","dungeon-visited","item-town-map","keypad-dpad-any","keypad-a"];
    private readonly ImmutableDictionary<string,byte[]> outputs;
    private readonly ImmutableDictionary<string,TownMapPositionSource> positionsByMap;
    public string GeneratedDirectory { get; }
    public string TownMapSourcePath => Path.Combine(GeneratedDirectory,"town-map.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory,"manifest.json");
    public ImmutableArray<TownMapAsset> Assets { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public TownMapLayout Layout { get; }
    public TownMapGift Gift { get; }
    public QuestItemDefinition Item { get; }
    public string ItemDescription { get; }
    public string ItemIconAssetId => "item-town-map";
    public bool SwitchAvailable { get; }
    public ImmutableArray<TownMapCell> Cells { get; }
    public ImmutableArray<TownMapIcon> Icons { get; }
    public ImmutableArray<TownMapPositionSource> Positions { get; }
    public JsonElement TownMap { get; }

    private PretTownMap(string directory, ImmutableArray<PretWorldSourceFile> files, JsonElement document,
        ImmutableDictionary<string,byte[]> payloads)
    {
        GeneratedDirectory=directory;SourceFiles=files;TownMap=document;outputs=payloads;
        var options=ContentJson.CreateOptions();
        T Parse<T>(string property)=>ContentJson.ParseStrict<T>(document.GetProperty(property).GetRawText(),options);
        Assets=Parse<ImmutableArray<TownMapAsset>>("assets"); Layout=Parse<TownMapLayout>("layout");
        Item=Parse<QuestItemDefinition>("itemDefinition");ItemDescription=document.GetProperty("itemDescription").GetString()!;
        SwitchAvailable=document.GetProperty("switchAvailable").GetBoolean();
        Gift=Parse<TownMapGift>("gift");Cells=Parse<ImmutableArray<TownMapCell>>("cells");
        Icons=Parse<ImmutableArray<TownMapIcon>>("icons");Positions=Parse<ImmutableArray<TownMapPositionSource>>("positions");
        positionsByMap=Positions.ToImmutableDictionary(p=>p.StableMapId,StringComparer.Ordinal);
        AdditionalSourceHashes=["town-map-manifest:"+ManifestSha256,"town-map-lock:"+LockSha256,
            ..files.Select(f=>"town-map:"+f.Path+":"+f.Sha256)];
    }

    /// <summary>Authenticates reviewed lock, all raw Git blobs and every output before exposing content.</summary>
    public static PretTownMap Load(string projectRoot)
    {
        var project=Path.GetFullPath(projectRoot);var raw=Path.Combine(project,"content/raw/town-map");
        var generated=Path.Combine(project,"content/generated/town-map");
        byte[] Pinned(string root,string relative,string hash,int? length=null,int maximum=1048576)
        {
            var path=SafeFile(root,relative);var info=new FileInfo(path);
            Require(info.Exists && info.Length<=maximum && (length is null || info.Length==length),"Town Map file size differs: "+relative);
            // Bound allocation and read even if the file grows between the size check and open.
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            Require(stream.Length==info.Length,"Town Map file changed while opening: "+relative);
            var bytes=new byte[checked((int)info.Length)];stream.ReadExactly(bytes);
            Require(stream.ReadByte()==-1 && Hash(bytes)==hash,"Town Map file identity differs: "+relative);
            return bytes;
        }
        _=Pinned(raw,"manifest.json",RawManifestSha256,maximum:4096);
        using var sourceLock=JsonDocument.Parse(Pinned(project,"content/town-map-source-lock.json",LockSha256,maximum:65536));
        using var manifest=JsonDocument.Parse(Pinned(generated,"manifest.json",ManifestSha256,maximum:32768));
        var l=sourceLock.RootElement;var m=manifest.RootElement;
        Require(l.GetProperty("schemaVersion").GetInt32()==1 && l.GetProperty("repository").GetString()=="pret/pokefirered" &&
            l.GetProperty("commit").GetString()==SourceCommit && l.GetProperty("tree").GetString()==SourceTree &&
            l.GetProperty("profile").GetString()==Profile && l.GetProperty("gameEdition").GetString()=="FIRERED" &&
            l.GetProperty("romRevision").GetInt32()==0,"Town Map source profile differs.");
        Require(m.GetProperty("schemaVersion").GetInt32()==1 && m.GetProperty("sourceCommit").GetString()==SourceCommit &&
            m.GetProperty("tree").GetString()==SourceTree && m.GetProperty("profile").GetString()==Profile &&
            m.GetProperty("lockSha256").GetString()==LockSha256 && m.GetProperty("fullSourceHash").GetString()==FullSourceHash &&
            m.GetProperty("inputCount").GetInt32()==66 && m.GetProperty("assetCount").GetInt32()==10 &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean(),
            "Town Map manifest profile differs.");
        var files=ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach(var file in l.GetProperty("files").EnumerateArray())
        {
            var path=file.GetProperty("path").GetString()!;var hash=file.GetProperty("sha256").GetString()!;
            var blob=file.GetProperty("gitBlobSha1").GetString()!;var size=file.GetProperty("byteLength").GetInt32();
            var bytes=Pinned(raw,path,hash,size);
            Require(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {size}\0"),..bytes]))==blob,
                "Town Map Git blob differs: "+path);
            files.Add(new(path,blob,size,hash,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory=files.ToImmutable();
        Require(inventory.Length==66 && inventory.Select(f=>f.Path).Distinct(StringComparer.Ordinal).Count()==66,
            "Town Map source inventory differs.");
        Require(Hash(Encoding.UTF8.GetBytes(string.Concat(inventory.OrderBy(f=>f.Path,StringComparer.Ordinal)
            .Select(f=>f.Path+"\0"+f.Sha256+"\n"))))==FullSourceHash,"Town Map source set differs.");
        var payloads=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path=output.GetProperty("path").GetString()!;
            Require(payloads.TryAdd(path,Pinned(generated,path,output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())),"Duplicate Town Map output.");
        }
        Require(payloads.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds.SelectMany(id=>new[]{id+".png",id+".rgba"})
            .Append("town-map.json")),"Town Map output inventory differs.");
        Require(Hash(payloads["town-map.json"])==TownMapSha256,"Town Map document identity differs.");
        using var document=JsonDocument.Parse(payloads["town-map.json"]);var d=document.RootElement;
        Require(d.GetProperty("schemaVersion").GetInt32()==1 && d.GetProperty("profile").GetString()==Profile &&
            d.GetProperty("sourceCommit").GetString()==SourceCommit && d.GetProperty("fullSourceHash").GetString()==FullSourceHash,
            "Town Map document profile differs.");
        var result=new PretTownMap(generated,inventory,d.Clone(),payloads.ToImmutable());
        result.Validate();return result;
    }

    private void Validate()
    {
        Require(Assets.Length==10 && Assets.Select(a=>a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds),"Town Map asset inventory differs.");
        foreach(var asset in Assets)
        {
            Require(asset.Width>0 && asset.Height>0 && asset.Width*asset.Height<=38400 &&
                outputs[asset.RgbaPath].Length==asset.Width*asset.Height*4 &&
                Hash(outputs[asset.RgbaPath])==asset.RgbaSha256 && Hash(outputs[asset.TexturePath])==asset.Sha256 &&
                asset.SourcePaths.Length>0 && asset.SourcePaths.All(p=>SourceFiles.Any(f=>f.Path==p)),"Town Map asset metadata differs.");
        }
        Require(Layout.LogicalWidth==240 && Layout.LogicalHeight==160 && Layout.Columns==22 && Layout.Rows==15 &&
            Layout.CellPixels==8 && Layout.CenterOffsetX==36 && Layout.CenterOffsetY==36 &&
            Layout.Cancel==new TownMapPoint(21,13) && Layout.SwitchButton==new TownMapPoint(21,11) &&
            Cells.Length==330 && Positions.Length==21 && Icons.Length==12 && !SwitchAvailable,"Town Map geometry profile differs.");
        for(int i=0;i<Cells.Length;i++)Require(Cells[i].X==i%22 && Cells[i].Y==i/22,"Town Map cell order differs.");
        Require(Gift.SourceItemId==361 && Gift.Pocket=="POCKET_KEY_ITEMS" && Gift.AvailableScene==1 && Gift.ReceivedScene==2 &&
            Gift.HideFlag=="FLAG_HIDE_TOWN_MAP" && Gift.Texts.Count==8,"Town Map gift profile differs.");
        Require(Item.Id.Value=="pret:ITEM_TOWN_MAP" && Item.SourceItemSymbol==Gift.ItemId && Item.SourceItemId==361 &&
            Item.Name=="TOWN MAP" && Item.Price==0 && Item.Importance==1 && Item.Registrability==1 &&
            Item.Pocket=="POCKET_KEY_ITEMS" && Item.Type=="ITEM_TYPE_BAG_MENU" && Item.HoldEffect=="HOLD_EFFECT_NONE" &&
            Item.HoldEffectParam==0 && Item.FieldUseFunction=="FieldUseFunc_TownMap" && Item.BattleUsage==0 &&
            Item.BattleUseFunction=="NULL" && Item.SecondaryId==0 && !Item.CanToss && !Item.CanSell && !Item.CanGive &&
            Item.Source.Path=="src/data/items.json" && Item.Source.Symbol=="ITEM_TOWN_MAP" && Item.Source.StartLine>0 &&
            Item.Source.EndLine>=Item.Source.StartLine && SourceFiles.Single(f=>f.Path==Item.Source.Path).Sha256==Item.Source.Sha256 &&
            Asset(ItemIconAssetId).Width==24 && Asset(ItemIconAssetId).Height==24 && !string.IsNullOrWhiteSpace(ItemDescription),
            "Town Map Bag item profile differs.");
        foreach(var source in Positions)
            Require(source.Width>0 && source.Height>0 && source.SectionWidth>0 && source.SectionHeight>0 &&
                source.SourcePaths.All(p=>SourceFiles.Any(f=>f.Path==p)),"Town Map projection metadata differs.");
    }

    public TownMapAsset Asset(string id)=>Assets.Single(a=>a.Id==id);
    public byte[] ReadAssetTexture(string id)=>[..outputs[Asset(id).TexturePath]];
    public byte[] ReadAssetRgba(string id)=>[..outputs[Asset(id).RgbaPath]];
    public TownMapCell CellAt(int x,int y)
    {
        RequirePoint(x,y);return Cells[y*Layout.Columns+x];
    }
    public TownMapPoint CursorCenter(int x,int y)
    {
        RequirePoint(x,y);return new(Layout.CenterOffsetX+Layout.CellPixels*x,Layout.CenterOffsetY+Layout.CellPixels*y);
    }
    /// <summary>Exact source division and overrides for the authenticated opening-to-Pewter map set.</summary>
    public TownMapPoint PositionForMap(string stableMapId,int x,int y)
    {
        if(!positionsByMap.TryGetValue(stableMapId,out var source))throw new ArgumentException("Unsupported Town Map source location.",nameof(stableMapId));
        if(x<0 || y<0 || x>=source.Width || y>=source.Height)throw new ArgumentOutOfRangeException(nameof(x),"Location lies outside its source map.");
        if(source.Override is not null)return source.Override;
        var projectedX=Math.Min(x/Math.Max(1,source.Width/source.SectionWidth),source.SectionWidth-1)+source.SectionX;
        var projectedY=Math.Min(y/Math.Max(1,source.Height/source.SectionHeight),source.SectionHeight-1)+source.SectionY;
        RequirePoint(projectedX,projectedY);return new(projectedX,projectedY);
    }
    /// <summary>Only filters original icon visibility. Visits do not unlock Fly or any other feature.</summary>
    public ImmutableArray<TownMapIcon> VisibleIcons(IReadOnlySet<string> flags)=>
        [..Icons.Where(i=>i.VisibleFlag is null || flags.Contains(i.VisibleFlag))];
    private void RequirePoint(int x,int y)
    {
        if(x<0 || x>=Layout.Columns || y<0 || y>=Layout.Rows)throw new ArgumentOutOfRangeException(nameof(x),"Town Map cursor outside Kanto grid.");
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    private static string SafeFile(string root,string path)
    {
        if(string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p=>p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Town Map path: "+path);
        for(var cursor=new DirectoryInfo(root);cursor is not null;cursor=cursor.Parent)
            if(cursor.LinkTarget is not null)throw new InvalidDataException("Town Map root symlink is unsupported.");
        var target=root;
        foreach(var segment in path.Split('/'))
        {
            target=Path.Combine(target,segment);
            if(new FileInfo(target).LinkTarget is not null || new DirectoryInfo(target).LinkTarget is not null)
                throw new InvalidDataException("Town Map symlink is unsupported: "+path);
        }
        return target;
    }
}

public sealed record TownMapPoint(int X,int Y);
public sealed record TownMapRect(int X,int Y,int Width,int Height);
public sealed record TownMapAsset(string Id,int Width,int Height,string TexturePath,string RgbaPath,string Sha256,string RgbaSha256,ImmutableArray<string> SourcePaths);
public sealed record TownMapUiTexts(string Move,string Cancel,string Switch,string Space);
public sealed record TownMapLayout(int LogicalWidth,int LogicalHeight,int Columns,int Rows,int CellPixels,int CenterOffsetX,int CenterOffsetY,
    int CursorWidth,int CursorHeight,int CursorFrameTicks,int CursorMoveTicks,int CursorMovePixelsPerTick,TownMapPoint Cancel,
    TownMapPoint SwitchButton,TownMapPoint MapName,TownMapPoint DungeonName,TownMapRect MapNameWindow,TownMapRect DungeonNameWindow,
    TownMapPoint TopBarLeft,TownMapPoint TopBarRight,int FontId,int TopBarFontId,ImmutableArray<ImmutableArray<int>> TextPaletteRgb,TownMapUiTexts UiTexts,int MapTextColor,int TextShadowColor,
    int DungeonVisitedTextColor,int DungeonUnvisitedTextColor,int NameWindowDarkeningCoefficient);
public sealed record TownMapGift(string ItemId,int SourceItemId,string Pocket,bool Registrable,string SceneVariable,int AvailableScene,
    int ReceivedScene,string TemporaryReceivedVariable,string ObjectLocalId,string HideFlag,string GiftScript,string Fanfare,ImmutableDictionary<string,string> Texts);
public sealed record TownMapCell(int X,int Y,string SectionId,string Name,string DungeonSectionId,string DungeonName);
public sealed record TownMapIcon(int X,int Y,int CenterX,int CenterY,string SectionId,string VisitedFlag,string? VisibleFlag);
public sealed record TownMapPositionSource(string StableMapId,string SourceMap,string SourceName,string SectionId,int Width,int Height,
    int SectionX,int SectionY,int SectionWidth,int SectionHeight,string MapType,TownMapPoint? Override,ImmutableArray<string> SourcePaths);
