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
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "898efb9be9d35163e08e2b5c47e22e8b3e59e35aa9bb1159727bffe379adc047";
    public const string ManifestSha256 = "7fe6515e5ad5e1caa915610d2d6972255c002571ced748888dec6722bd5ec8f5";
    public const string RawManifestSha256 = "3857d0cc97ff721e85720293d19e14eaca5b352ad33961ecdb76ee6cf31a09d2";
    public const string TrainerCardSha256 = "9aa01bf9870885feb44b7dbd346b2f2958a37b0d857def3055a3203b3ecac536";
    public const string FullSourceHash = "f73afc4a831b3039dfba545980f28061bce6a1a4b5a29a963158e15b6b3b9d7f";
    private static readonly string[] AssetIds = ["front-male","front-female","back-male","back-female",
        "portrait-red","portrait-leaf","badge-1","badge-2","badge-3","badge-4","badge-5","badge-6","badge-7","badge-8"];
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<TrainerCardAsset> Assets { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
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
        AdditionalSourceHashes=["trainer-card-manifest:"+ManifestSha256,"trainer-card-lock:"+LockSha256,
            ..files.Select(f=>"trainer-card:"+f.Path+":"+f.Sha256)];
    }

    /// <summary>Verifies both manifests, reviewed lock, every source blob and every generated payload before use.</summary>
    public static PretTrainerCard Load(string projectRoot)
    {
        var project=Path.GetFullPath(projectRoot);
        var raw=Path.Combine(project,"content/raw/trainer-card");
        var directory=Path.Combine(project,"content/generated/trainer-card");
        byte[] ReadPinned(string root,string path,string hash,int? length=null)
        {
            var bytes=File.ReadAllBytes(SafeFile(root,path));
            Require(Hash(bytes)==hash && (length is null || bytes.Length==length),"Pinned Trainer Card differs: "+path);
            return bytes;
        }
        _=ReadPinned(raw,"manifest.json",RawManifestSha256);
        using var lockDocument=JsonDocument.Parse(ReadPinned(project,"scripts/fetch-trainer-card.lock.json",LockSha256));
        using var manifest=JsonDocument.Parse(ReadPinned(directory,"manifest.json",ManifestSha256));
        var l=lockDocument.RootElement; var m=manifest.RootElement;
        Require(l.GetProperty("schemaVersion").GetInt32()==1 && l.GetProperty("profile").GetString()==Profile &&
            l.GetProperty("repository").GetString()=="pret/pokefirered" && l.GetProperty("commit").GetString()==SourceCommit &&
            l.GetProperty("tree").GetString()==SourceTree && l.GetProperty("gameEdition").GetString()=="FIRERED" &&
            l.GetProperty("romRevision").GetInt32()==0,"Trainer Card source identity differs.");
        Require(m.GetProperty("schemaVersion").GetInt32()==1 && m.GetProperty("profile").GetString()==Profile &&
            m.GetProperty("sourceCommit").GetString()==SourceCommit && m.GetProperty("tree").GetString()==SourceTree &&
            m.GetProperty("lockSha256").GetString()==LockSha256 && m.GetProperty("fullSourceHash").GetString()==FullSourceHash &&
            m.GetProperty("inputCount").GetInt32()==31 && m.GetProperty("assetCount").GetInt32()==14 &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean(),
            "Trainer Card generated manifest identity differs.");
        var files=ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach(var entry in l.GetProperty("files").EnumerateArray())
        {
            var path=entry.GetProperty("path").GetString()!;var hash=entry.GetProperty("sha256").GetString()!;
            var blob=entry.GetProperty("gitBlobSha1").GetString()!;var length=entry.GetProperty("byteLength").GetInt32();
            var bytes=ReadPinned(raw,path,hash,length);
            Require(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {length}\0"),..bytes]))==blob,
                "Trainer Card Git blob identity differs: "+path);
            files.Add(new(path,blob,length,hash,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory=files.ToImmutable();
        Require(inventory.Length==31 && inventory.Select(f=>f.Path).Distinct(StringComparer.Ordinal).Count()==31,
            "Trainer Card source inventory differs.");
        Require(Hash(Encoding.UTF8.GetBytes(string.Concat(inventory.OrderBy(f=>f.Path,StringComparer.Ordinal)
            .Select(f=>f.Path+"\0"+f.Sha256+"\n"))))==FullSourceHash,"Trainer Card source-set hash differs.");
        var outputs=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path=entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path,ReadPinned(directory,path,entry.GetProperty("sha256").GetString()!,
                entry.GetProperty("byteLength").GetInt32())),"Duplicate Trainer Card output.");
        }
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds.SelectMany(s=>new[]{s+".png",s+".rgba"})
            .Append("trainer-card.json")),"Trainer Card output inventory differs.");
        Require(Hash(outputs["trainer-card.json"])==TrainerCardSha256,"Trainer Card document identity differs.");
        using var document=JsonDocument.Parse(outputs["trainer-card.json"]);var a=document.RootElement;
        Require(a.GetProperty("schemaVersion").GetInt32()==1 && a.GetProperty("profile").GetString()==Profile &&
            a.GetProperty("sourceCommit").GetString()==SourceCommit && a.GetProperty("fullSourceHash").GetString()==FullSourceHash,
            "Trainer Card document profile differs.");
        var assets=ContentJson.ParseStrict<ImmutableArray<TrainerCardAsset>>(a.GetProperty("assets").GetRawText(),ContentJson.CreateOptions());
        Require(assets.Length==14 && assets.Select(v=>v.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds),
            "Trainer Card asset inventory differs.");
        foreach(var asset in assets)
        {
            Require(asset.Width>0 && asset.Height>0 && outputs[asset.RgbaPath].Length==asset.Width*asset.Height*4 &&
                Hash(outputs[asset.RgbaPath])==asset.RgbaSha256 && Hash(outputs[asset.TexturePath])==asset.Sha256,
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
    public byte[] ReadAssetRgba(string id) => [..outputBytes[Asset(id).RgbaPath]];
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
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
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
public sealed record TrainerCardAsset(string Id,string TexturePath,string RgbaPath,int Width,int Height,int X,int Y,
    string Sha256,string RgbaSha256,ImmutableArray<string> SourcePaths);
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
