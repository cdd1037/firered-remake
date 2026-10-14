using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Only the seven bytes written by StringCopy are source-defined; the final name-buffer byte is not.</summary>
public sealed record NpcTradeMailPayload(string SourceCommit, int TradeId, int MailNumber,
    ImmutableArray<ushort> Words, string AuthorName, ImmutableArray<byte> AuthorNameBytes,
    uint AuthorTrainerId, ImmutableArray<byte> AuthorTrainerIdBytes, ushort SourceSpeciesId,
    string SourceSpeciesSymbol, ushort SourceItemId, ItemId Item, string SourceItemSymbol,
    PokemonSourceRef ConstructorSource, PokemonSourceRef TradeSource, PokemonSourceRef MessageSource,
    PokemonSourceRef StructureSource, PokemonSourceRef SpeciesSource, PokemonSourceRef ItemSource);
public sealed record NpcTradeMailWord(string Symbol, ushort Value, int Group, int Index, string Text,
    PokemonSourceRef DefinitionSource, PokemonSourceRef TextSource);
public sealed record NpcTradeMailText(string Symbol, string Text, PokemonSourceRef Source);
public sealed record NpcTradeMailAsset(string Id, int PlayerGender, int Width, int Height,
    string RgbaPath, string PngPath, string RgbaSha256, string PngSha256,
    ImmutableArray<string> SourcePaths, PokemonSourceRef GenderPaletteSource);
public sealed record NpcTradeMailReadLayout(int ScreenWidth, int ScreenHeight, int ArrangementType, string FontSymbol,
    int MessageX, int MessageY, int LineHeight, int AuthorX, int AuthorY, string AuthorPrefix,
    ImmutableArray<byte> TextRgb, ImmutableArray<byte> ShadowRgb, bool ShowPokemonIcon,
    ImmutableArray<int> WordsPerLine, ImmutableArray<string> Lines, PokemonSourceRef LayoutSource,
    PokemonSourceRef AttributesSource, PokemonSourceRef WindowsSource, PokemonSourceRef ReadSource,
    PokemonSourceRef BufferSource, PokemonSourceRef PrinterSource, PokemonSourceRef ConversionSource);

/// <summary>Authenticated original Dontae mail payload, five-row READ layout, Fab stationery and item metadata.</summary>
public sealed class PretNpcTradeMail
{
    public const string ResourceId = "firered-npc-trade-mail-v1";
    public const string ManifestSha256 = "dc17f9fe9e4c2faac5ebe297f37f5e3b9878c4fb22059364d665d27bd8a4ca8e";
    public const string LockSha256 = "834f547f2d8e35c6ece25e8624da8f26d4c8191fbc7300c885d05881d63c9ffb";
    public const string FullSourceHash = "637a2726be97f77cbea6a21f5c847616e88677941261fcb049b521abbc1350b6";
    public const string AdditionalItemsSourceSha256 = "c594369d7a0599f01bd31c61dc7cd1006283bae122f85354aca2529f5a48d8d6";
    public static ItemId FabMailId { get; } = new("pret:ITEM_FAB_MAIL");
    public NpcTradeMailPayload Dontae { get; }
    public SourceMailMessage Message { get; }
    public ImmutableArray<NpcTradeMailWord> Words { get; }
    public NpcTradeMailReadLayout ReadLayout { get; }
    public ImmutableArray<NpcTradeMailAsset> Assets { get; }
    public ImmutableDictionary<string, byte[]> AssetFiles { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<NpcTradeMailText> TextSources { get; }
    public NpcTradeItemsDocument Items { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public string GeneratedDirectory { get; }

    private PretNpcTradeMail(SilphSourcePackage source, NpcTradeMailPayload dontae,
        ImmutableArray<NpcTradeMailWord> words, NpcTradeMailReadLayout layout,
        ImmutableArray<NpcTradeMailAsset> assets, ImmutableArray<NpcTradeMailText> texts, NpcTradeItemsDocument items)
    {
        Dontae = dontae;
        // StringCopy defines seven bytes; canonical saves initialize the spare buffer byte to EOS.
        Message = new(dontae.Words, dontae.AuthorName, dontae.AuthorNameBytes.Add(255),
            dontae.AuthorTrainerIdBytes, dontae.SourceSpeciesId, dontae.SourceItemId);
        if (SourceMailRules.ValidateMessage(Message) is {} error) throw new InvalidDataException(error);
        Words = words; ReadLayout = layout; Assets = assets; Items = items;
        TextSources = texts; Texts = texts.ToImmutableDictionary(t => t.Symbol, t => t.Text, StringComparer.Ordinal);
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        SourceFiles = source.Files; GeneratedDirectory = source.Directory;
        AssetFiles = assets.SelectMany(a => new[] { a.RgbaPath, a.PngPath })
            .Concat(items.Icons.SelectMany(i => new[] { i.RgbaPath, i.PngPath }))
            .ToImmutableDictionary(p => p, p => source.Outputs[p], StringComparer.Ordinal);
        AdditionalSourceHashes = ["npc-trade-mail-manifest:" + ManifestSha256, "npc-trade-mail-lock:" + LockSha256,
            .. source.Files.Select(f => "npc-trade-mail:" + f.Path + ":" + f.Sha256)];
    }

    public NpcTradeMailAsset BackgroundFor(int playerGender) => playerGender is 0 or 1
        ? Assets.Single(a => a.PlayerGender == playerGender)
        : throw new ArgumentOutOfRangeException(nameof(playerGender));

    public static PretNpcTradeMail Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot, "npc-trade-mail", ResourceId,
            "scripts/npc-trade-mail.lock.json", LockSha256, 10074, ManifestSha256, 2834, 34, 13);
        var catalog = source.Document("catalog.json"); var contracts = source.Document("contracts.json");
        Need(catalog.GetProperty("resourceId").GetString() == ResourceId &&
            catalog.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit &&
            catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Dontae mail source/catalog identity differs.");
        Need(contracts.GetProperty("retainedTradeSemanticSha256").GetString() == NpcTradePokemonCatalog.SemanticSha256 &&
            contracts.GetProperty("changesToTradeDefinitions").GetArrayLength() == 0 &&
            contracts.GetProperty("newMaps").GetArrayLength() == 0 && contracts.GetProperty("newShops").GetArrayLength() == 0 &&
            contracts.GetProperty("partyMailCapacity").GetInt32() == 6 && contracts.GetProperty("mailboxCapacity").GetInt32() == 10 &&
            contracts.GetProperty("totalMailCapacity").GetInt32() == 16 && contracts.GetProperty("noneMailIndex").GetInt32() == 255,
            "Original mail lifecycle scope differs.");
        var payload = source.Parse<NpcTradeMailPayload>("dontae.json");
        Need(payload.SourceCommit == PretWorldMap.SourceCommit && payload.TradeId == 1 && payload.MailNumber == 0 &&
            payload.AuthorName == "DONTAE" && payload.AuthorTrainerId == 36728 &&
            payload.AuthorTrainerIdBytes.SequenceEqual(new byte[] { 0, 0, 143, 120 }) &&
            payload.AuthorNameBytes.SequenceEqual(new byte[] { 190, 201, 200, 206, 187, 191, 255 }) &&
            payload.SourceSpeciesId == 124 && payload.SourceSpeciesSymbol == "SPECIES_JYNX" &&
            payload.SourceItemId == 131 && payload.SourceItemSymbol == "ITEM_FAB_MAIL" && payload.Item == FabMailId &&
            payload.Words.SequenceEqual(new ushort[] { 0x0E1D, 0x1020, 0x141B, 0x2A7C, 0x0C00, 0x1006, 0x143F, 0x102F, 0x1029 }),
            "Original Dontae mail payload differs.");
        foreach (var reference in new[] { payload.ConstructorSource, payload.TradeSource, payload.MessageSource,
            payload.StructureSource, payload.SpeciesSource, payload.ItemSource }) source.Reference(reference);
        var words = source.Parse<ImmutableArray<NpcTradeMailWord>>("easy-chat-words.json");
        Need(words.Length == 9 && words.Select(w => w.Value).SequenceEqual(payload.Words) &&
            words.All(w => w.Value == ((w.Group << 9) | w.Index)) &&
            words.Select(w => w.Text).SequenceEqual(new[] { "THAT'S", "A", "HEALTHY", "JYNX", "!", "BE", "KIND", "TO", "IT" }),
            "Nine source Easy Chat words differ.");
        foreach (var word in words) { source.Reference(word.DefinitionSource); source.Reference(word.TextSource); }
        var layout = source.Parse<NpcTradeMailReadLayout>("read-layout.json");
        Need(layout.ScreenWidth == 240 && layout.ScreenHeight == 160 && layout.ArrangementType == 1 &&
            layout.FontSymbol == "FONT_NORMAL_COPY_1" && layout.MessageX == 32 && layout.MessageY == 35 && layout.LineHeight == 16 &&
            layout.AuthorX == 136 && layout.AuthorY == 128 && layout.AuthorPrefix == "From " && !layout.ShowPokemonIcon &&
            layout.WordsPerLine.SequenceEqual(new[] { 2, 2, 2, 2, 1 }) &&
            layout.Lines.SequenceEqual(new[] { "THAT'S A", "HEALTHY JYNX", "! BE", "KIND TO", "IT" }) &&
            layout.TextRgb.SequenceEqual(new byte[] { 82, 82, 82 }) && layout.ShadowRgb.SequenceEqual(new byte[] { 206, 206, 206 }),
            "Original Fab Mail five-row READ layout differs.");
        foreach (var reference in new[] { layout.LayoutSource, layout.AttributesSource, layout.WindowsSource,
            layout.ReadSource, layout.BufferSource, layout.PrinterSource, layout.ConversionSource }) source.Reference(reference);
        var assets = SafariSource.Parse<ImmutableArray<NpcTradeMailAsset>>(
            System.Text.Encoding.UTF8.GetBytes(catalog.GetProperty("assets").GetRawText()));
        Need(assets.Length == 2 && assets.Select(a => a.PlayerGender).SequenceEqual(new[] { 0, 1 }), "Fab gender backgrounds differ.");
        foreach (var asset in assets)
        {
            source.Reference(asset.GenderPaletteSource);
            Need(asset.Width == 240 && asset.Height == 160 && asset.SourcePaths.Length == 3 &&
                asset.SourcePaths.All(source.Raw.ContainsKey) && source.Outputs[asset.RgbaPath].Length == 240 * 160 * 4 &&
                SafariSource.Hash(source.Outputs[asset.RgbaPath]) == asset.RgbaSha256 &&
                SafariSource.Hash(source.Outputs[asset.PngPath]) == asset.PngSha256,
                "Original Fab background pixels differ.");
        }
        var items = source.Parse<NpcTradeItemsDocument>("items.json");
        Need(items.SourceCommit == PretWorldMap.SourceCommit && items.Profile == NpcTradeShopCatalog.Profile &&
            items.PreviousItemCount == 126 && items.ItemCount == 127 && items.Additions.Length == 1 &&
            items.Additions[0].Id == FabMailId && items.AdditionsSemanticSha256 == AdditionalItemsSourceSha256 &&
            ContentJson.SourceRecordSha256(items.Additions.ToArray()) == AdditionalItemsSourceSha256 &&
            items.ShopsAdded.IsEmpty && items.Icons.Length == 1, "Fab inventory addition scope differs.");
        source.Reference(items.Additions[0].Source);
        Need(items.Additions[0].SourceMetadata is { HoldEffect: "HOLD_EFFECT_NONE", SecondaryId: 10,
            CanGive: true, CanSell: true, CanToss: true } && items.Additions[0].Type == "ITEM_TYPE_MAIL" &&
            items.Additions[0].FieldUseFunction == "FieldUseFunc_Mail" && items.Additions[0].Price == 50,
            "Original Fab Mail held-item metadata differs.");
        var icon = items.Icons[0];
        Need(icon.Item == FabMailId && icon.Width == 24 && icon.Height == 24 && icon.SourcePaths.Length == 2 &&
            icon.SourcePaths.All(source.Raw.ContainsKey) && source.Outputs[icon.RgbaPath].Length == 24 * 24 * 4 &&
            SafariSource.Hash(source.Outputs[icon.RgbaPath]) == icon.RgbaSha256 &&
            SafariSource.Hash(source.Outputs[icon.PngPath]) == icon.PngSha256, "Original Fab item icon differs.");
        var texts = source.Parse<ImmutableArray<NpcTradeMailText>>("texts.json");
        Need(texts.Length == 27 && texts.Select(t => t.Symbol).Distinct(StringComparer.Ordinal).Count() == 27 &&
            texts.Single(t => t.Symbol == "gText_From").Text == layout.AuthorPrefix, "Source mail UI text scope differs.");
        foreach (var text in texts) source.Reference(text.Source);
        foreach (var entry in contracts.GetProperty("lifecycle").EnumerateArray())
        {
            var reference = entry.GetProperty("source");
            source.Reference(reference.GetProperty("path").GetString()!, reference.GetProperty("sha256").GetString()!,
                reference.GetProperty("startLine").GetInt32(), reference.GetProperty("endLine").GetInt32(),
                reference.GetProperty("symbol").GetString()!);
        }
        return new(source, payload, words, layout, assets, texts, items);
    }

    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
