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
    private static readonly string[] AssetIds = ["kanto","cursor-0","cursor-1","player-red","player-leaf","dungeon-unvisited","dungeon-visited","item-town-map","keypad-dpad-any","keypad-a"];
    private readonly ImmutableDictionary<string,byte[]> outputs;
    private readonly ImmutableDictionary<string,TownMapPositionSource> positionsByMap;
    public string GeneratedDirectory { get; }
    public string TownMapSourcePath => Path.Combine(GeneratedDirectory,"town-map.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory,"manifest.json");
    public ImmutableArray<TownMapAsset> Assets { get; }
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

    private PretTownMap(string directory, JsonElement document,
        ImmutableDictionary<string,byte[]> payloads, SharedItemCatalog items, IReadOnlyDictionary<string, string>? mapNames)
    {
        GeneratedDirectory=directory;TownMap=document;outputs=payloads;
        var options=items.CreateJsonOptions();
        T Parse<T>(string property)=>ContentJson.ParseStrict<T>(document.GetProperty(property).GetRawText(),options);
        Assets=Parse<ImmutableArray<TownMapAsset>>("assets"); Layout=Parse<TownMapLayout>("layout");
        Item=Parse<QuestItemDefinition>("itemDefinition");ItemDescription=document.GetProperty("itemDescription").GetString()!;
        SwitchAvailable=document.GetProperty("switchAvailable").GetBoolean();
        Gift=Parse<TownMapGift>("gift");Cells=Parse<ImmutableArray<TownMapCell>>("cells");
        if (mapNames is not null) Cells = Cells.Select(cell => PretFlyMap.ProjectNames(cell, mapNames)).ToImmutableArray();
        Icons=Parse<ImmutableArray<TownMapIcon>>("icons");Positions=Parse<ImmutableArray<TownMapPositionSource>>("positions");
        positionsByMap=Positions.ToImmutableDictionary(p=>p.StableMapId,StringComparer.Ordinal);
    }

    /// <summary>Authenticates reviewed lock, all raw Git blobs and every output before exposing content.</summary>
    public static PretTownMap Load(string projectRoot, SharedItemCatalog? items = null, IReadOnlyDictionary<string, string>? mapNames = null)
    {
            var project=Path.GetFullPath(projectRoot);        var generated=Path.Combine(project,"content/generated/ui/town-map");
        byte[] Pinned(string root, string relative)
        {
            var path = GeneratedResourceFile.SafePath(root, relative);
            var info = new FileInfo(path);
            Require(info.Exists, "Town Map file size differs: " + relative);
            return File.ReadAllBytes(path);
        }
        using var manifest=JsonDocument.Parse(Pinned(generated,"manifest.json"));
        var m=manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32()==1 && m.GetProperty("assetCount").GetInt32()==10,
            "Town Map manifest profile differs.");

        var payloads=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path=output.GetProperty("path").GetString()!;
                    Require(payloads.TryAdd(path,Pinned(generated,path)),"Duplicate Town Map output.");
        }
        Require(payloads.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds.Select(id => id + ".png")
            .Append("town-map.json")),"Town Map output inventory differs.");
        using var document=JsonDocument.Parse(payloads["town-map.json"]);var d=document.RootElement;
                Require(d.GetProperty("schemaVersion").GetInt32()==1 ,
            "Town Map document profile differs.");
        var result=new PretTownMap(generated,d.Clone(),payloads.ToImmutable(), items ?? SharedItemCatalog.LoadProject(projectRoot), mapNames);
        result.Validate();return result;
    }

    private void Validate()
    {
        Require(Assets.Length==10 && Assets.Select(a=>a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds),"Town Map asset inventory differs.");
        foreach(var asset in Assets)
        {
            Require(asset.Width>0 && asset.Height>0 && asset.Width*asset.Height<=38400 &&
                (outputs[asset.TexturePath]).Length > 0,"Town Map asset metadata differs.");
        }
        Require(Layout.LogicalWidth==240 && Layout.LogicalHeight==160 && Layout.Columns==22 && Layout.Rows==15 &&
            Layout.CellPixels==8 && Layout.CenterOffsetX==36 && Layout.CenterOffsetY==36 &&
            Layout.Cancel==new TownMapPoint(21,13) && Layout.SwitchButton==new TownMapPoint(21,11) &&
            Cells.Length==330 && Positions.Length==21 && Icons.Length==12 && !SwitchAvailable,"Town Map geometry profile differs.");
        for(int i=0;i<Cells.Length;i++)Require(Cells[i].X==i%22 && Cells[i].Y==i/22,"Town Map cell order differs.");
        Require(Gift.SourceItemId==361 && Gift.Pocket=="POCKET_KEY_ITEMS" && Gift.AvailableScene==1 && Gift.ReceivedScene==2 &&
            Gift.HideFlag=="FLAG_HIDE_TOWN_MAP" && Gift.Texts.Count==8,"Town Map gift profile differs.");
        Require(Item.Id.Value=="pret:ITEM_TOWN_MAP" && Item.SourceItemSymbol==Gift.ItemId && Item.SourceItemId==361 &&
            Item.Price==0 && Item.Importance==1 && Item.Registrability==1 &&
            Item.Pocket=="POCKET_KEY_ITEMS" && Item.Type=="ITEM_TYPE_BAG_MENU" && Item.HoldEffect=="HOLD_EFFECT_NONE" &&
            Item.HoldEffectParam==0 && Item.FieldUseFunction=="FieldUseFunc_TownMap" && Item.BattleUsage==0 &&
            Item.BattleUseFunction=="NULL" && Item.SecondaryId==0 && !Item.CanToss && !Item.CanSell && !Item.CanGive &&
            Asset(ItemIconAssetId).Width==24 && Asset(ItemIconAssetId).Height==24 && !string.IsNullOrWhiteSpace(ItemDescription),
            "Town Map Bag item profile differs.");
        foreach(var source in Positions)
            Require(source.Width>0 && source.Height>0 && source.SectionWidth>0 && source.SectionHeight>0,"Town Map projection metadata differs.");
    }

    public TownMapAsset Asset(string id)=>Assets.Single(a=>a.Id==id);
    public byte[] ReadAssetTexture(string id)=>[..outputs[Asset(id).TexturePath]];

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
    private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}

public sealed record TownMapPoint(int X,int Y);
public sealed record TownMapRect(int X,int Y,int Width,int Height);
public sealed record TownMapAsset(string Id, int Width, int Height, string TexturePath);
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
    int SectionX,int SectionY,int SectionWidth,int SectionHeight,string MapType,TownMapPoint? Override);
