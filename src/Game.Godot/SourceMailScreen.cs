using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Original settled Mail and Easy Chat pixels. This owns disposable display resources only.</summary>
public sealed class SourceMailScreen : IDisposable
{
    private readonly PretMailPresentation _content;
    private readonly string _fontDirectory;
    private readonly string _projectRoot;
    private readonly CampaignResourceCache.Owner _resources;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceGameFont> _fonts = new(StringComparer.Ordinal);
    public PretMailPresentation Content => _content;
    public SourceMailScreen(PretMailPresentation content, string sourceFontDirectory, string projectRoot, CampaignResourceCache.Owner resources)
    { _content = content; _fontDirectory = sourceFontDirectory; _projectRoot = projectRoot; _resources = resources; }
    public Texture2D Texture(string id)
    {
        if (_textures.TryGetValue(id, out var existing)) return existing;
        var asset = _content.Assets[id];
        var texture = _resources.NativeTexture(System.IO.Path.Combine(_projectRoot, asset.PngPath),
            asset.Region is {} r ? new Rect2I(r.X, r.Y, r.Width, r.Height) : null);
        if (texture.GetWidth() != asset.Width || texture.GetHeight() != asset.Height)
            throw new InvalidDataException("mail_original_pixels:" + id);
        _textures.Add(id, texture); return texture;
    }
    public SourceGameFont Font(ImmutableArray<byte> foreground, ImmutableArray<byte> shadow)
    {
        string key = string.Join(',', foreground) + ":" + string.Join(',', shadow);
        if (_fonts.TryGetValue(key, out var font)) return font;
        font = SourceGameFont.Load(_fontDirectory, foreground: Color.Color8(foreground[0], foreground[1], foreground[2]),
            shadow: Color.Color8(shadow[0], shadow[1], shadow[2]));
        AddSpecialGlyphs(font, foreground, shadow);
        _fonts.Add(key, font); return font;
    }
    private void AddSpecialGlyphs(SourceGameFont font, ImmutableArray<byte> foreground, ImmutableArray<byte> shadow)
    {
        const string id = "easy-chat-special-glyphs";
        var asset = _content.Assets[id]; using var original = new Image();
        if (original.LoadPngFromBuffer(_content.PngFiles[id]) != Error.Ok)
            throw new InvalidDataException("mail_special_glyph_pixels");
        var pixels = original.GetData();
        var sourceForeground = _content.EasyChat.GetProperty("textRgb").EnumerateArray().Select(c => c.GetByte()).ToArray();
        var sourceShadow = _content.EasyChat.GetProperty("shadowRgb").EnumerateArray().Select(c => c.GetByte()).ToArray();
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0) continue;
            var color = pixels.AsSpan(i, 3).SequenceEqual(sourceForeground) ? foreground :
                pixels.AsSpan(i, 3).SequenceEqual(sourceShadow) ? shadow : throw new InvalidDataException("mail_special_glyph_role");
            for (int c = 0; c < 3; c++) pixels[i + c] = color[c];
        }
        using var image = Image.CreateFromData(asset.Width, asset.Height, false, Image.Format.Rgba8, pixels);
        var cache = new Vector2I(font.Size, 0); font.Font.SetTextureImage(0, cache, 1, image);
        foreach (var glyph in _content.EasyChat.GetProperty("specialGlyphs").EnumerateArray())
        {
            int codepoint = glyph.GetProperty("codepoint").GetInt32();
            font.Font.SetGlyphAdvance(0, font.Size, codepoint, new(glyph.GetProperty("advance").GetInt32(), 0));
            font.Font.SetGlyphOffset(0, cache, codepoint, new(0, -font.Size));
            font.Font.SetGlyphSize(0, cache, codepoint, new(16, 14));
            font.Font.SetGlyphUVRect(0, cache, codepoint, new(glyph.GetProperty("x").GetInt32(), 0, 16, 14));
            font.Font.SetGlyphTextureIdx(0, cache, codepoint, 1);
        }
    }
    public static string PrepareText(SourceGameFont font, string text)
    {
        var result = new System.Text.StringBuilder();
        foreach (string piece in System.Text.RegularExpressions.Regex.Split(text, "(\\{PK\\}|\\{POKEBLOCK\\})"))
            result.Append(piece switch { "{PK}" => "\uE100", "{POKEBLOCK}" => "\uE101\uE102\uE103\uE104\uE105", _ => font.PrepareText(piece) });
        return result.ToString();
    }
    public SourceGameFont EditorFont => Font(
        _content.EasyChat.GetProperty("textRgb").EnumerateArray().Select(c => c.GetByte()).ToImmutableArray(),
        _content.EasyChat.GetProperty("shadowRgb").EnumerateArray().Select(c => c.GetByte()).ToImmutableArray());
    public SourceGameFont TitleFont => Font(
        _content.EasyChat.GetProperty("titleRgb").EnumerateArray().Select(c => c.GetByte()).ToImmutableArray(),
        _content.EasyChat.GetProperty("titleShadowRgb").EnumerateArray().Select(c => c.GetByte()).ToImmutableArray());
    public static float TextWidth(SourceGameFont font, string text) => font.Font.GetStringSize(PrepareText(font, text), fontSize: font.Size).X;
    public static void Text(CanvasItem canvas, SourceGameFont font, Vector2 position, string text) =>
        canvas.DrawString(font.Font, position + new Vector2(0, font.Baseline), PrepareText(font, text), fontSize: font.Size);
    public void DrawReader(CanvasItem canvas, ushort itemId, int gender, SourceMailMessage? message,
        Func<ushort, string> wordText, Func<int, Texture2D?> icon)
    {
        var paper = _content.Paper(itemId); canvas.DrawTexture(Texture(_content.Background(itemId, gender).Id), Vector2.Zero);
        if (message is null) return;
        if (message.ItemId != itemId) throw new InvalidDataException("mail_reader_item_mismatch");
        var font = Font(paper.TextRgb, paper.ShadowRgb);
        foreach (var line in _content.ReaderLines(itemId, message.Words, wordText)) Text(canvas, font, new(line.X, line.Y), line.Text);
        Text(canvas, font, new(paper.AuthorX, paper.AuthorY), _content.SourceText.Text("gText_From") + message.AuthorName);
        if (paper.IconKind != "none" && PretMailPresentation.IconRenderSpecies(message.Species) is {} renderSpecies)
        {
            var image = icon(renderSpecies) ?? throw new InvalidDataException("mail_species_icon_missing:" + renderSpecies);
            if (image.GetWidth() != 32 || image.GetHeight() != 32) throw new InvalidDataException("mail_species_icon_dimensions");
            canvas.DrawTexture(image, new(paper.IconCenterX - 16, paper.IconCenterY - 16));
        }
    }
    public void DrawEditorFields(CanvasItem canvas, EasyChatPresentationTemplate layout, SourceEasyChatTemplate template,
        ImmutableArray<ushort> words, Func<ushort, string> wordText)
    {
        if (words.IsDefault || words.Length != template.WordCount) throw new InvalidDataException("easy_chat_editor_word_count");
        for (int slot = 0; slot < words.Length; slot++)
        {
            var position = FieldPosition(layout, template, words, wordText, slot);
            if (words[slot] == ushort.MaxValue)
                for (int i = 0; i < 7; i++) canvas.DrawTexture(Texture("easy-chat-underscore"), position + new Vector2(i * 10, 0));
            else Text(canvas, EditorFont, position, wordText(words[slot]));
        }
    }
    public Vector2 FieldPosition(EasyChatPresentationTemplate layout, SourceEasyChatTemplate template,
        ImmutableArray<ushort> words, Func<ushort, string> wordText, int slot)
    {
        if (slot < 0 || slot >= template.WordCount || words.IsDefault || words.Length != template.WordCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        int row = slot / template.Columns;
        float x = layout.FieldX;
        for (int preceding = row * template.Columns; preceding < slot; preceding++)
            x += 17 + (words[preceding] == ushort.MaxValue ? 70 : TextWidth(EditorFont, wordText(words[preceding])));
        return new(x, layout.FieldY + row * 16);
    }
    public void DrawTriangle(CanvasItem canvas, Vector2 center) => canvas.DrawTexture(Texture("easy-chat-triangle-cursor"), center - new Vector2(4, 4));
    public void DrawRectangle(CanvasItem canvas, Vector2 center, int frame)
    {
        if (frame is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(frame));
        var texture = Texture("easy-chat-rectangle-cursor");
        // Source two 64x32 OAMs are centered ±32; the right copy is horizontally flipped.
        var source = new Rect2(0, frame * 32, 64, 32);
        canvas.DrawTextureRectRegion(texture, new(center - new Vector2(64, 16), new Vector2(64, 32)), source);
        canvas.DrawTextureRectRegion(texture, new(center + new Vector2(64, -16), new Vector2(-64, 32)), source);
    }
    public void Dispose()
    {
        _textures.Clear();
        foreach (var font in _fonts.Values) font.Dispose(); _fonts.Clear();
    }
}
