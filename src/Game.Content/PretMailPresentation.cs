using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record MailPresentationAsset(string Id, int Width, int Height, string PngPath, bool Shared, ImageRegion? Region = null);
public sealed record MailStationery(ushort ItemId, string ItemSymbol, string Name,
    ImmutableArray<string> BackgroundIds, string ItemIconId, int MessageX, int MessageY, int LineHeight,
    ImmutableArray<int> WordsPerLine, int AuthorX, int AuthorY, string AuthorPrefix,
    ImmutableArray<byte> TextRgb, ImmutableArray<byte> ShadowRgb, string IconKind, int IconCenterX, int IconCenterY);
public sealed record MailReaderLine(int X, int Y, string Text);
public sealed record EasyChatPresentationTemplate(string Id, int FrameLeft, int FrameTop, int FrameWidth, int FrameHeight,
    int FieldX, int FieldY, ImmutableDictionary<string, string> BackgroundIds, int TitleX, int TitleY, int TitleWidth);

/// <summary>All original Mail paper, item metadata and settled Easy Chat surfaces, reusing existing source blobs and icons.</summary>
public sealed class PretMailPresentation
{
    public const string ResourceId = "firered-mail-presentation-v1";
    public const string Base = "content/generated/ui/mail-presentation/";
    public ImmutableArray<MailStationery> Stationery { get; }
    public ImmutableArray<EarlyShopItemDefinition> Items { get; }
    public ImmutableDictionary<string, MailPresentationAsset> Assets { get; }
    public ImmutableDictionary<string, byte[]> PngFiles { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public JsonElement EasyChat { get; }
    public ImmutableDictionary<string, EasyChatPresentationTemplate> Templates { get; }

    private PretMailPresentation(JsonElement document, ImmutableDictionary<string, byte[]> files, SharedItemCatalog items)
    {
        Stationery = Parse<ImmutableArray<MailStationery>>(document.GetProperty("stationery"));
        Items = document.GetProperty("items").Deserialize<ImmutableArray<EarlyShopItemDefinition>>(items.CreateJsonOptions());
        Assets = Parse<ImmutableArray<MailPresentationAsset>>(document.GetProperty("assets")).ToImmutableDictionary(a => a.Id, StringComparer.Ordinal);
        Texts = Parse<ImmutableArray<NpcTradeMailText>>(document.GetProperty("texts")).ToImmutableDictionary(t => t.Symbol, t => t.Text, StringComparer.Ordinal);
        PngFiles = files; EasyChat = document.GetProperty("easyChat").Clone();
        Templates = Parse<ImmutableArray<EasyChatPresentationTemplate>>(document.GetProperty("templates"))
            .ToImmutableDictionary(t => t.Id, StringComparer.Ordinal);
        Need(Templates.Keys.Order().SequenceEqual(new[] { "mail", "profile" }), "easy_chat_presentation_templates");
        foreach (var template in Templates.Values)
        {
            Need(template.FrameLeft >= 1 && template.FrameTop >= 0 && template.FrameWidth > 0 && template.FrameHeight > 0 &&
                template.FrameLeft + template.FrameWidth < 30 && template.FrameTop + template.FrameHeight <= 20 &&
                template.FieldX == template.FrameLeft * 8 + 17 && template.FieldY == template.FrameTop * 8 + 2 &&
                template.TitleX >= 0 && template.TitleY >= 0 && template.TitleWidth > 0 && template.TitleX + template.TitleWidth <= 240 &&
                template.BackgroundIds.Keys.Order().SequenceEqual(new[] { "fields", "groups", "words" }) &&
                template.BackgroundIds.Values.All(id => Assets.TryGetValue(id, out var asset) && asset is { Width: 240, Height: 160 }),
                "easy_chat_presentation_layout");
        }
        foreach (var key in new[] { "titleRgb", "titleShadowRgb" })
            Need(EasyChat.GetProperty(key).GetArrayLength() == 3 && EasyChat.GetProperty(key).EnumerateArray().All(c => c.TryGetByte(out _)),
                "easy_chat_title_palette");
        Need(Stationery.Length == 12 && Stationery.Select(s => (int)s.ItemId).SequenceEqual(Enumerable.Range(121, 12)) &&
            Stationery.All(s => s.ItemSymbol == SourceMailRules.ItemSymbol(s.ItemId) && s.BackgroundIds.Length == 2 &&
                s.WordsPerLine.SequenceEqual(new[] { 2,2,2,2,1 }) && s.MessageX == 32 && s.MessageY == 35 && s.LineHeight == 16 &&
                s.TextRgb.Length == 3 && s.ShadowRgb.Length == 3 && s.AuthorPrefix == "From "), "mail_stationery_layout");
        Need(Items.Length == 12 && Items.Select(i => i.SourceItemId).SequenceEqual(Enumerable.Range(121, 12)),
            "mail_stationery_inventory");
        foreach (var paper in Stationery)
        {
            Need(paper.IconKind == (paper.ItemId == 127 ? "bead" : paper.ItemId == 130 ? "dream" : "none") &&
                paper.IconCenterX == (paper.ItemId == 127 ? 96 : paper.ItemId == 130 ? 40 : 0) &&
                paper.IconCenterY == (paper.IconKind == "none" ? 0 : 128), "mail_stationery_species_icon");
            foreach (string id in paper.BackgroundIds) Need(Assets[id] is { Width: 240, Height: 160 }, "mail_stationery_dimensions");
            Need(Assets[paper.ItemIconId] is { Width: 24, Height: 24 }, "mail_item_icon_dimensions");
        }
    }
    private static T Parse<T>(JsonElement element) => SafariSource.Parse<T>(Encoding.UTF8.GetBytes(element.GetRawText()));
    private static void Need(bool condition, string message) => SafariSource.Require(condition, message);
    public MailStationery Paper(ushort itemId) => Stationery.Single(p => p.ItemId == itemId);
    public MailPresentationAsset Background(ushort itemId, int gender) => gender is 0 or 1
        ? Assets[Paper(itemId).BackgroundIds[gender]] : throw new ArgumentOutOfRangeException(nameof(gender));

    /// <summary>Original conversion omits the separator after FFFF. Empty/space-first lines do not advance the printer.</summary>
    public ImmutableArray<MailReaderLine> ReaderLines(ushort itemId, ImmutableArray<ushort> words, Func<ushort, string> wordText)
    {
        if (words.IsDefault || words.Length != 9) throw new ArgumentException("mail_word_count", nameof(words));
        var paper = Paper(itemId); var result = ImmutableArray.CreateBuilder<MailReaderLine>(); int word = 0, y = paper.MessageY;
        foreach (int count in paper.WordsPerLine)
        {
            var line = new StringBuilder();
            for (int column = 0; column < count; column++)
            {
                ushort value = words[word++];
                if (value == ushort.MaxValue) continue;
                line.Append(wordText(value));
                if (column + 1 < count) line.Append(' ');
            }
            if (line.Length == 0 || line[0] == ' ') continue;
            result.Add(new(paper.MessageX, y, line.ToString())); y += paper.LineHeight;
        }
        return result.ToImmutable();
    }

    /// <summary>Source Mail stores Unown form+30000. The reader uses the source first icon frame, including Deoxys normal icon.</summary>
    public static int? IconRenderSpecies(ushort mailSpecies)
    {
        if (SourceMailComposerRules.IsEncodedUnownSpecies(mailSpecies)) return mailSpecies == SourceMailComposerRules.UnownMailSpeciesBase ? UnownFormRules.SourceSpeciesId : mailSpecies - SourceMailComposerRules.UnownMailSpeciesBase + NameRaterRules.EggSpecies;
        // Raw Unown has undefined uninitialized form data in the source function; canonical authored Mail encodes its form.
        if (mailSpecies == 201) return 201;
        return mailSpecies is > 0 and < 412 ? mailSpecies : null;
    }

    public static PretMailPresentation Load(string projectRoot, string? dependencyRoot = null, SharedItemCatalog? items = null)
    {
        dependencyRoot ??= projectRoot;
        var doc = SafariSource.Parse<JsonElement>(SafariSource.Read(projectRoot, Base + "catalog.json"));
        var png = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var asset in Parse<ImmutableArray<MailPresentationAsset>>(doc.GetProperty("assets")))
        {
            png.Add(asset.Id, SafariSource.Read(asset.PngPath.StartsWith(Base, StringComparison.Ordinal) ? projectRoot : dependencyRoot, asset.PngPath));
        }
        return new(doc, png.ToImmutable(), items ?? SharedItemCatalog.LoadProject(dependencyRoot));
    }
}
