using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Events;

namespace Game.Content;

/// <summary>FRLG own-card art and source display metadata; achievement inputs belong to the campaign.</summary>
public sealed class PretTrainerCard
{
    public const string Profile = "firered-trainer-card-v2";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private static readonly string[] AssetIds =
        (from stars in Enumerable.Range(0,4) from side in new[] { "front", "back" } from gender in new[] { "male", "female" }
         select side+"-"+gender+(stars==0?"":"-"+stars))
        .Concat(from stars in Enumerable.Range(0,4) from layer in new[] { "background", "panel-front", "panel-back" } from gender in new[] { "male", "female" }
            select layer+"-"+gender+(stars==0?"":"-"+stars))
        .Concat(new[] { "portrait-red", "portrait-leaf", "star" })
        .Concat(Enumerable.Range(1,8).Select(i=>"badge-"+i))
        .Concat(from kind in Enumerable.Range(0,2) from tier in Enumerable.Range(1,4) select "sticker-"+kind+"-"+tier).ToArray();
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<TrainerCardAsset> Assets { get; }
    public ImmutableArray<TrainerCardPage> Pages { get; }
    public TrainerCardLayout Layout { get; }
    public TrainerCardTexts Texts { get; }
    public TrainerCardRules Rules { get; }
    public ImmutableArray<TrainerCardBadge> Badges => Layout.Badges;
    public JsonElement TrainerCard { get; }
    public string GeneratedDirectory { get; }
    public string TrainerCardSourcePath => Path.Combine(GeneratedDirectory,"trainer-card.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory,"manifest.json");

    private PretTrainerCard(string directory, ImmutableArray<TrainerCardAsset> assets, ImmutableArray<TrainerCardPage> pages,
        JsonElement document, ImmutableDictionary<string,byte[]> outputs, IReadOnlyDictionary<string,string> sourceTexts)
    {
        GeneratedDirectory=directory; Assets=assets; Pages=pages; TrainerCard=document; outputBytes=outputs;
        var options=ContentJson.CreateOptions();
        Layout=ContentJson.ParseStrict<TrainerCardLayout>(document.GetProperty("layout").GetRawText(),options);
        var texts=ContentJson.ParseStrict<TrainerCardTexts>(document.GetProperty("texts").GetRawText(),options);
        string Label(string symbol)
        {
            Require(sourceTexts.TryGetValue(symbol,out var value) && !string.IsNullOrWhiteSpace(value),
                "Missing Trainer Card source text: "+symbol);
            var pages=SourceBattleText.Pages(sourceTexts,symbol,ImmutableDictionary<string,string>.Empty);
            Require(pages.Length==1 && pages[0].IndexOfAny(['\r','\n'])<0,
                "Unsupported Trainer Card source text: "+symbol);
            return pages[0];
        }
        // These eight fields are drawn by the own-card view. The four link-only fields
        // remain unconsumed metadata; the shared battle bank owns visible source text.
        Texts=texts with { Name=Label("gText_TrainerCardName"), Id=Label("gText_TrainerCardIDNo"),
            Money=Label("gText_TrainerCardMoney"), Currency=Label("gText_TrainerCardYen"),
            Pokedex=Label("gText_TrainerCardPokedex"), Time=Label("gText_TrainerCardTime"),
            Colon=Label("gText_Colon2"), HallOfFame=Label("gText_HallOfFameDebut") };
        Rules=ContentJson.ParseStrict<TrainerCardRules>(document.GetProperty("rules").GetRawText(),options);
    }

    /// <summary>Loads and validates generated runtime presentation resources without reading original source.</summary>
    public static PretTrainerCard Load(string projectRoot, IReadOnlyDictionary<string,string>? sourceTexts = null)
    {
        var project=Path.GetFullPath(projectRoot);
        var directory=Path.Combine(project,"content/generated/ui/trainer-card");
        byte[] ReadPinned(string root,string path)
        {
            var bytes=File.ReadAllBytes(GeneratedResourceFile.SafePath(root,path));
            return bytes;
        }
        using var manifest=JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m=manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32()==3 && m.GetProperty("assetCount").GetInt32()==AssetIds.Length,
            "Trainer Card generated manifest identity differs.");

        var outputs=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path=entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path,ReadPinned(directory,path)),"Duplicate Trainer Card output.");
        }
        Require(outputs.ContainsKey("trainer-card.json"),"Trainer Card document missing.");
        using var document=JsonDocument.Parse(outputs["trainer-card.json"]);var a=document.RootElement;
        Require(a.GetProperty("schemaVersion").GetInt32()==3 ,
            "Trainer Card document profile differs.");
        var assets=ContentJson.ParseStrict<ImmutableArray<TrainerCardAsset>>(a.GetProperty("assets").GetRawText(),ContentJson.CreateOptions());
        var pages=ContentJson.ParseStrict<ImmutableArray<TrainerCardPage>>(a.GetProperty("pages").GetRawText(),ContentJson.CreateOptions());
        Require(!pages.IsDefaultOrEmpty && pages.Select(p=>p.TexturePath).Distinct(StringComparer.Ordinal).Count()==pages.Length &&
            pages.All(p=>p.Width is >0 and <=1024 && p.Height is >0 and <=1024 &&
                !string.IsNullOrWhiteSpace(p.TexturePath) && Path.GetFileName(p.TexturePath)==p.TexturePath &&
                p.TexturePath.EndsWith(".png",StringComparison.Ordinal)),
            "Trainer Card atlas page metadata differs.");
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(pages.Select(p=>p.TexturePath).Append("trainer-card.json")),
            "Trainer Card output inventory differs.");
        Require(!assets.IsDefaultOrEmpty && assets.Length==AssetIds.Length &&
            assets.Select(v=>v.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds),
            "Trainer Card asset inventory differs.");
        var pageByPath=pages.ToDictionary(p=>p.TexturePath,StringComparer.Ordinal);
        foreach(var asset in assets)
        {
            Require(asset.Width>0 && asset.Height>0 && asset.TexturePath is not null &&
                pageByPath.TryGetValue(asset.TexturePath,out var page) &&
                asset.Region is {} region && region.X>=0 && region.Y>=0 &&
                region.Width==asset.Width && region.Height==asset.Height &&
                (long)region.X+region.Width<=page.Width && (long)region.Y+region.Height<=page.Height &&
                outputs[asset.TexturePath].Length>0,
                "Trainer Card asset metadata differs.");
        }
        Require(assets.Select(asset=>asset.TexturePath).ToHashSet(StringComparer.Ordinal).SetEquals(pageByPath.Keys),
            "Trainer Card atlas page has no assets.");
        var result=new PretTrainerCard(directory,assets,pages,a.Clone(),outputs.ToImmutable(),sourceTexts??PretBattleText.Load(project));
        Require(result.Layout.LogicalWidth==240 && result.Layout.LogicalHeight==160 && result.Layout.FontId==2 &&
            result.Layout.Badges.Length==8 && result.Layout.StatTextRgb.Length==3 && result.Layout.StatShadowRgb.Length==3 && result.Rules.MaximumSupportedStars==3 &&
            result.Rules.StickerVariables.SequenceEqual(new[] { "VAR_HOF_BRAG_STATE", "VAR_EGG_BRAG_STATE" }) &&
            result.Rules.PhotoX==24 && result.Rules.PhotoY==120 && result.Rules.PhotoSpacing==32,"Trainer Card bounded display profile differs.");
        return result;
    }

    public string BackgroundId(bool back,int gender,int stars)
    {
        if(gender is <0 or >1 || stars<0 || stars>Rules.MaximumSupportedStars)throw new ArgumentOutOfRangeException(nameof(stars));
        return (back?"back":"front")+"-"+(gender==0?"male":"female")+(stars==0?"":"-"+stars);
    }
    public static string FormatHallOfFameTime(int packed) => $"{packed >> 16,3}:{(packed >> 8) & 255:D2}:{packed & 255:D2}";
    /// <summary>Original 5-bit palette math, with fixed-point constants truncated exactly as Q_8_8.</summary>
    public static (byte R,byte G,byte B) TintPhoto(byte red,byte green,byte blue,int tint)
    {
        if(tint is <0 or >3)throw new ArgumentOutOfRangeException(nameof(tint));
        int r=red>>3,g=green>>3,b=blue>>3;
        if(tint!=0)
        {
            int gray=(r*76+g*151+b*29)>>8;
            (int rt,int gt,int bt)=tint switch {1=>(0,0,0),2=>(500,330,310),_=>(307,256,240)};
            r=Math.Min(31,rt*gray>>8);g=Math.Min(31,gt*gray>>8);b=Math.Min(31,bt*gray>>8);
        }
        static byte Expand(int value)=>(byte)((value<<3)|(value>>2));
        return(Expand(r),Expand(g),Expand(b));
    }
    public TrainerCardAsset Asset(string id) => Assets.Single(a=>a.Id==id);
    /// <summary>Encoded page bytes; logical images are the asset's exact atlas region.</summary>
    public byte[] ReadTexturePage(string texturePath) => Pages.Any(p=>p.TexturePath==texturePath)
        ? [..outputBytes[texturePath]] : throw new ArgumentException("Unknown Trainer Card page.",nameof(texturePath));

    public static string FormatTrainerId(uint trainerId) => (trainerId & 0xffffu).ToString("D5",CultureInfo.InvariantCulture);
    public string FormatMoney(uint money)
    {
        if(money>Rules.MaxMoney) throw new ArgumentOutOfRangeException(nameof(money));
        return Texts.Currency+money.ToString(CultureInfo.InvariantCulture);
    }
    public TrainerCardPoint MoneyValuePosition(uint money) => new(Layout.MoneyRightX-Layout.DecimalCellWidth*FormatMoney(money).Length,Layout.MoneyLabel.Y);
    public TrainerCardPoint CaughtValuePosition(int caught)
    {
        if(caught is <0 or >386) throw new ArgumentOutOfRangeException(nameof(caught));
        return new(Layout.PokedexRightX-Layout.DecimalCellWidth*caught.ToString(CultureInfo.InvariantCulture).Length,Layout.PokedexLabel.Y);
    }
    /// <summary>Accepts national dex numbers, not internal species IDs. Original dex flags count once, restricted to the selected dex.</summary>
    public static int CountCaught(IEnumerable<int> nationalDexNumbers,bool nationalEnabled) => nationalDexNumbers
        .Where(n=>n>=1 && n<=(nationalEnabled?386:151)).Distinct().Count();
    /// <summary>The source requires all three National Dex gates. A flag alone does not enable it.</summary>
    public static bool IsNationalDexEnabled(byte nationalMagic,ushort nationalDexVariable,bool nationalDexFlag) =>
        nationalMagic==0xb9 && nationalDexVariable==0x6258 && nationalDexFlag;
    private static void Require(bool condition,string message) { if(!condition) throw new InvalidDataException(message); }
}

public sealed record TrainerCardPoint(int X,int Y);
public sealed record TrainerCardRect(int X,int Y,int Width,int Height);
public sealed record TrainerCardBadge(int Index,string Flag,string AssetId,int X,int Y);
public sealed record TrainerCardAsset(string Id, string TexturePath, int Width, int Height, int X, int Y, ImageRegion Region);
public sealed record TrainerCardPage(string TexturePath, int Width, int Height);
public sealed record TrainerCardTexts(string Name,string Id,string Money,string Currency,string Pokedex,string Time,
    string Colon,string HallOfFame,string LinkBattles,string Trades,string UnionRoom,string BerryCrush);
public sealed record TrainerCardLayout(int LogicalWidth,int LogicalHeight,int FontId,int TextColor,int ShadowColor,
    TrainerCardPoint TextWindow,TrainerCardPoint Name,TrainerCardPoint Id,TrainerCardPoint MoneyLabel,int MoneyRightX,
    TrainerCardPoint PokedexLabel,int PokedexRightX,TrainerCardPoint TimeLabel,TrainerCardPoint TimeHours,
    TrainerCardPoint TimeColon,TrainerCardPoint TimeMinutes,TrainerCardPoint BackName,TrainerCardRect PortraitWindow,
    int DecimalCellWidth,ImmutableArray<TrainerCardBadge> Badges,ImmutableArray<byte> StatTextRgb,ImmutableArray<byte> StatShadowRgb);
public sealed record TrainerCardBackRow(string Id,int X,int Y,int ValueX,string Gate);
public sealed record TrainerCardRules(int TrainerIdMask,int TrainerIdDigits,int MaxMoney,int KantoDexCount,int NationalDexCount,
    string PokedexVisibleFlag,int NationalDexMagic,string NationalDexVariable,int NationalDexVariableValue,string NationalDexFlag,
    int MaximumSupportedStars,int PhotoX,int PhotoY,int PhotoSpacing,ImmutableArray<string> StickerVariables,ImmutableArray<TrainerCardBackRow> BackRows);
