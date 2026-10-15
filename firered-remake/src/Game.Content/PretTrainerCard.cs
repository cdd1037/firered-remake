using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated FRLG own-card zero-star art and source display metadata. No campaign state or invented statistics.</summary>
public sealed class PretTrainerCard
{
    public const string Profile = "firered-trainer-card-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private static readonly string[] AssetIds = ["front-male","front-female","back-male","back-female",
        "portrait-red","portrait-leaf","badge-1","badge-2","badge-3","badge-4","badge-5","badge-6","badge-7","badge-8"];
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<TrainerCardAsset> Assets { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public TrainerCardLayout Layout { get; }
    public TrainerCardTexts Texts { get; }
    public TrainerCardRules Rules { get; }
    public ImmutableArray<TrainerCardBadge> Badges => Layout.Badges;
    public JsonElement TrainerCard { get; }
    public string GeneratedDirectory { get; }
    public string TrainerCardSourcePath => Path.Combine(GeneratedDirectory,"trainer-card.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory,"manifest.json");

    private PretTrainerCard(string directory, ImmutableArray<TrainerCardAsset> assets,
        ImmutableArray<PretWorldSourceFile> files, JsonElement document, ImmutableDictionary<string,byte[]> outputs)
    {
        GeneratedDirectory=directory; Assets=assets; SourceFiles=files; TrainerCard=document; outputBytes=outputs;
        var options=ContentJson.CreateOptions();
        Layout=ContentJson.ParseStrict<TrainerCardLayout>(document.GetProperty("layout").GetRawText(),options);
        Texts=ContentJson.ParseStrict<TrainerCardTexts>(document.GetProperty("texts").GetRawText(),options);
        Rules=ContentJson.ParseStrict<TrainerCardRules>(document.GetProperty("rules").GetRawText(),options);
    }

    /// <summary>Verifies both manifests, reviewed lock, every source blob and every generated payload before use.</summary>
    public static PretTrainerCard Load(string projectRoot)
    {
        var project=Path.GetFullPath(projectRoot);
        var directory=Path.Combine(project,"content/generated/trainer-card");
        byte[] ReadPinned(string root,string path)
        {
            var bytes=File.ReadAllBytes(SafeFile(root,path));
            return bytes;
        }
        using var lockDocument=JsonDocument.Parse(ReadPinned(project,"scripts/fetch-trainer-card.lock.json"));
        using var manifest=JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var l=lockDocument.RootElement; var m=manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32()==1 && m.GetProperty("assetCount").GetInt32()==14 &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean(),
            "Trainer Card generated manifest identity differs.");
        var files=ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach(var entry in l.GetProperty("files").EnumerateArray())
        {
            var path=entry.GetProperty("path").GetString()!;            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory=files.ToImmutable();
        var outputs=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path=entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path,ReadPinned(directory,path)),"Duplicate Trainer Card output.");
        }
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds.Select(s => s + ".png")
            .Append("trainer-card.json")),"Trainer Card output inventory differs.");
        using var document=JsonDocument.Parse(outputs["trainer-card.json"]);var a=document.RootElement;
        Require(a.GetProperty("schemaVersion").GetInt32()==1 ,
            "Trainer Card document profile differs.");
        var assets=ContentJson.ParseStrict<ImmutableArray<TrainerCardAsset>>(a.GetProperty("assets").GetRawText(),ContentJson.CreateOptions());
        Require(assets.Length==14 && assets.Select(v=>v.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds),
            "Trainer Card asset inventory differs.");
        foreach(var asset in assets)
        {
            Require(asset.Width>0 && asset.Height>0 && PngRgba8.Decode(outputs[asset.TexturePath], asset.Width, asset.Height).Length==asset.Width*asset.Height*4 ,
                "Trainer Card asset metadata differs.");
            Require(asset.SourcePaths.Length>0 && asset.SourcePaths.All(p=>inventory.Any(f=>f.Path==p)),
                "Trainer Card asset source reference differs.");
        }
        var result=new PretTrainerCard(directory,assets,inventory,a.Clone(),outputs.ToImmutable());
        Require(result.Layout.LogicalWidth==240 && result.Layout.LogicalHeight==160 && result.Layout.FontId==2 &&
            result.Layout.Badges.Length==8 && result.Rules.StarCount==0 && !result.Rules.PlayTimePersisted &&
            !result.Rules.GameStatisticsPersisted,"Trainer Card bounded display profile differs.");
        return result;
    }

    public TrainerCardAsset Asset(string id) => Assets.Single(a=>a.Id==id);
    public byte[] ReadAssetTexture(string id) => [..outputBytes[Asset(id).TexturePath]];
    public byte[] ReadAssetRgba(string id) => PngRgba8.Decode(outputBytes[Asset(id).TexturePath]);
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
    private static string SafeFile(string root,string path)
    {
        if(string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p=>p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Trainer Card path: "+path);
        for(var cursor=new DirectoryInfo(root);cursor is not null;cursor=cursor.Parent)
            if(cursor.LinkTarget is not null) throw new InvalidDataException("Trainer Card directory symlink is unsupported.");
        var result=root;
        foreach(var segment in path.Split('/'))
        {
            result=Path.Combine(result,segment);
            if(new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Trainer Card symlink is unsupported: "+path);
        }
        return result;
    }
}

public sealed record TrainerCardPoint(int X,int Y);
public sealed record TrainerCardRect(int X,int Y,int Width,int Height);
public sealed record TrainerCardBadge(int Index,string Flag,string AssetId,int X,int Y);
public sealed record TrainerCardAsset(string Id, string TexturePath, int Width, int Height, int X, int Y,
    ImmutableArray<string> SourcePaths);
public sealed record TrainerCardTexts(string Name,string Id,string Money,string Currency,string Pokedex,string Time,
    string Colon,string HallOfFame,string LinkBattles,string Trades,string UnionRoom,string BerryCrush);
public sealed record TrainerCardLayout(int LogicalWidth,int LogicalHeight,int FontId,int TextColor,int ShadowColor,
    TrainerCardPoint TextWindow,TrainerCardPoint Name,TrainerCardPoint Id,TrainerCardPoint MoneyLabel,int MoneyRightX,
    TrainerCardPoint PokedexLabel,int PokedexRightX,TrainerCardPoint TimeLabel,TrainerCardPoint TimeHours,
    TrainerCardPoint TimeColon,TrainerCardPoint TimeMinutes,TrainerCardPoint BackName,TrainerCardRect PortraitWindow,
    int DecimalCellWidth,ImmutableArray<TrainerCardBadge> Badges);
public sealed record TrainerCardBackRow(string Id,int X,int Y,int ValueX,string Gate);
public sealed record TrainerCardRules(int TrainerIdMask,int TrainerIdDigits,int MaxMoney,int KantoDexCount,int NationalDexCount,
    string PokedexVisibleFlag,int NationalDexMagic,string NationalDexVariable,int NationalDexVariableValue,string NationalDexFlag,
    int StarCount,bool PlayTimePersisted,bool GameStatisticsPersisted,ImmutableArray<TrainerCardBackRow> BackRows);
