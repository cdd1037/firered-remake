using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Only the seven bytes written by StringCopy are source-defined; the final name-buffer byte is not.</summary>
public sealed record NpcTradeMailPayload(int TradeId, int MailNumber,
    ImmutableArray<ushort> Words, string AuthorName, ImmutableArray<byte> AuthorNameBytes,
    uint AuthorTrainerId, ImmutableArray<byte> AuthorTrainerIdBytes, ushort SourceSpeciesId,
    string SourceSpeciesSymbol, ushort SourceItemId, ItemId Item, string SourceItemSymbol,
    PokemonSourceRef ConstructorSource, PokemonSourceRef TradeSource, PokemonSourceRef MessageSource,
    PokemonSourceRef StructureSource, PokemonSourceRef SpeciesSource, PokemonSourceRef ItemSource);
public sealed record NpcTradeMailWord(string Symbol, ushort Value, int Group, int Index, string Text,
    PokemonSourceRef DefinitionSource, PokemonSourceRef TextSource);
public sealed record NpcTradeMailText(string Symbol, string Text, PokemonSourceRef Source);
public sealed record NpcTradeMailAsset(string Id, int PlayerGender, int Width, int Height, string TexturePath,
    PokemonSourceRef GenderPaletteSource);
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
    public static ItemId FabMailId { get; } = new("pret:ITEM_FAB_MAIL");
    public SourceMailMessage Message { get; }
    public ImmutableArray<NpcTradeMailWord> Words { get; }
    public NpcTradeMailReadLayout ReadLayout { get; }
    public ImmutableArray<NpcTradeMailAsset> Assets { get; }
    public ImmutableDictionary<string, byte[]> AssetFiles { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<NpcTradeMailText> TextSources { get; }
    public NpcTradeItemsDocument Items { get; }
    public JsonElement Catalog { get; }
    public string GeneratedDirectory { get; }

    private PretNpcTradeMail(SilphSourcePackage source, NpcTradeMailPayload dontae,
        ImmutableArray<NpcTradeMailWord> words, NpcTradeMailReadLayout layout,
        ImmutableArray<NpcTradeMailAsset> assets, ImmutableArray<NpcTradeMailText> texts, NpcTradeItemsDocument items)
    {
        // Load authenticated the original byte metadata. Runtime ownership uses its numeric
        // trainer identity and literal name; it does not retain a second byte authority.
        Message = new(dontae.Words, dontae.AuthorName, dontae.AuthorTrainerId,
            dontae.SourceSpeciesId, dontae.SourceItemId, SourceMailOrigin.NpcTrade);
        if (SourceMailRules.ValidateMessage(Message) is {} error) throw new InvalidDataException(error);
        Words = words; ReadLayout = layout; Assets = assets; Items = items;
        TextSources = texts; Texts = texts.ToImmutableDictionary(t => t.Symbol, t => t.Text, StringComparer.Ordinal);
        Catalog = source.Document("catalog.json");
        GeneratedDirectory = source.Directory;
        AssetFiles = assets.Select(a => a.TexturePath)
            .Concat(items.Icons.Select(i => i.TexturePath))
            .Distinct(StringComparer.Ordinal).ToImmutableDictionary(p => p, p => source.Read(p), StringComparer.Ordinal);
    }

    public NpcTradeMailAsset BackgroundFor(int playerGender) => playerGender is 0 or 1
        ? Assets.Single(a => a.PlayerGender == playerGender)
        : throw new ArgumentOutOfRangeException(nameof(playerGender));

    public static PretNpcTradeMail Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"ui/npc-trade-mail");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "Dontae mail source/catalog identity differs.");
        var payload = source.Parse<NpcTradeMailPayload>("dontae.json");
        Need(payload.TradeId == 1 && payload.MailNumber == 0 &&
            payload.AuthorName == "DONTAE" && payload.AuthorTrainerId == 36728 &&
            payload.AuthorTrainerIdBytes.SequenceEqual(new byte[] { 0,0,143,120 }) &&
            payload.AuthorNameBytes.SequenceEqual(new byte[] { 190,201,200,206,187,191,255 }) &&
            payload.SourceSpeciesId == 124 && payload.SourceSpeciesSymbol == "SPECIES_JYNX" &&
            payload.SourceItemId == 131 && payload.SourceItemSymbol == "ITEM_FAB_MAIL" && payload.Item == FabMailId &&
            payload.Words.SequenceEqual(new ushort[] { 0x0E1D,0x1020,0x141B,0x2A7C,0x0C00,0x1006,0x143F,0x102F,0x1029 }),
            "Original Dontae mail payload differs.");
        var words = source.Parse<ImmutableArray<NpcTradeMailWord>>("easy-chat-words.json");
        Need(words.Length == 9 && words.Select(w => w.Value).SequenceEqual(payload.Words) &&
            words.All(w => w.Value == ((w.Group << 9) | w.Index)) &&
            words.Select(w => w.Text).SequenceEqual(new[] { "THAT'S","A","HEALTHY","JYNX","!","BE","KIND","TO","IT" }),
            "Nine source Easy Chat words differ.");
        var layout = source.Parse<NpcTradeMailReadLayout>("read-layout.json");
        Need(layout.ScreenWidth == 240 && layout.ScreenHeight == 160 && layout.ArrangementType == 1 &&
            layout.FontSymbol == "FONT_NORMAL_COPY_1" && layout.MessageX == 32 && layout.MessageY == 35 && layout.LineHeight == 16 &&
            layout.AuthorX == 136 && layout.AuthorY == 128 && layout.AuthorPrefix == "From " && !layout.ShowPokemonIcon &&
            layout.WordsPerLine.SequenceEqual(new[] { 2,2,2,2,1 }) &&
            layout.Lines.SequenceEqual(new[] { "THAT'S A","HEALTHY JYNX","! BE","KIND TO","IT" }) &&
            layout.TextRgb.SequenceEqual(new byte[] { 82,82,82 }) && layout.ShadowRgb.SequenceEqual(new byte[] { 206,206,206 }),
            "Original Fab Mail five-row READ layout differs.");
        var assets = SafariSource.Parse<ImmutableArray<NpcTradeMailAsset>>(
            System.Text.Encoding.UTF8.GetBytes(catalog.GetProperty("assets").GetRawText()));
        Need(assets.Length == 2 && assets.Select(a => a.PlayerGender).SequenceEqual(new[] { 0,1 }), "Fab gender backgrounds differ.");
        foreach (var asset in assets)
        {
            Need(asset.Width == 240 && asset.Height == 160 &&
                (source.Read(asset.TexturePath)).Length > 0,
                "Original Fab background pixels differ.");
        }
        var items = source.Parse<NpcTradeItemsDocument>("items.json");
        Need(            items.PreviousItemCount == 126 && items.ItemCount == 127 && items.Additions.Length == 1 &&
            items.Additions[0].Id == FabMailId &&
            items.ShopsAdded.IsEmpty && items.Icons.Length == 1, "Fab inventory addition scope differs.");
        Need(items.Additions[0].Item is { HoldEffect: "HOLD_EFFECT_NONE", SecondaryId: 10,
            CanGive: true, CanSell: true, CanToss: true } && items.Additions[0].Type == "ITEM_TYPE_MAIL" &&
            items.Additions[0].FieldUseFunction == "FieldUseFunc_Mail" && items.Additions[0].Price == 50,
            "Original Fab Mail held-item metadata differs.");
        var icon = items.Icons[0];
        Need(icon.Item == FabMailId && icon.Width == 24 && icon.Height == 24 &&
            (source.Read(icon.TexturePath)).Length > 0, "Original Fab item icon differs.");
        var texts = source.Parse<ImmutableArray<NpcTradeMailText>>("texts.json");
        Need(texts.Length == 27 && texts.Select(t => t.Symbol).Distinct(StringComparer.Ordinal).Count() == 27 &&
            texts.Single(t => t.Symbol == "gText_From").Text == layout.AuthorPrefix, "Source mail UI text scope differs.");
        return new(source, payload, words, layout, assets, texts, items);
    }

    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
