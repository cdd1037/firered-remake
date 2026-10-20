using System.Text;
using System.Text.Json;
using Godot;

/// <summary>Original bitmap glyphs plus an optional selected text pack's licensed glyph masks. No system-font substitution.</summary>
public sealed class SourceGameFont : IDisposable
{
    /// <summary>SourceFloor retains the importer golden. MgbaBitReplicated matches the captured emulator RGB555 output.</summary>
    public enum ColorProfile { SourceFloor, MgbaBitReplicated }

    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public const int LevelPrefixCodepoint = 0xE000;
    private readonly HashSet<int> _characters;
    private readonly HashSet<int> _warned = [];
    // Variants share the immutable imported input, never a previously recolored atlas.
    private sealed record SupplementGlyph(int Width, int Height, int Advance, byte[] Roles);
    private sealed record SupplementFace(int Size, Dictionary<int, SupplementGlyph> Glyphs);
    private sealed record SupplementData(byte[] Foreground, byte[] Shadow, SupplementFace Normal, SupplementFace Small);
    private sealed class GlyphPage(int textureIndex)
    {
        internal const int Size = 256;
        internal readonly int TextureIndex = textureIndex;
        internal readonly byte[] Pixels = new byte[Size * Size * 4];
        internal int X, Y;
    }
    private readonly List<GlyphPage> _glyphPages = [];
    private readonly HashSet<int> _installedSupplement = [];
    // These are borrowed by clipped text consumers; this font owns/disposes them.
    private readonly Dictionary<int, ImageTexture> _glyphTextures = [];
    private int _nextTextureIndex;
    private readonly SupplementFace? _supplement;
    private sealed record SourceData(string Face, byte[] Catalog, byte[] Atlas, byte[]? LevelCatalog,
        byte[]? LevelAtlas, SupplementData? Supplement);
    // The selected pack is immutable for this process, like SourceTextPack.Current. Font variants
    // share original masks; a palette variant never rereads or recolors an already recolored atlas.
    private static readonly Lazy<SupplementData?> SelectedSupplement = new(() =>
    {
        string? directory = Game.Content.SourceTextPack.Current.FontDirectory;
        if (directory is null) return null;
        return LoadSupplement(directory);
    });
    private readonly SourceData _source;
    private bool _disposed;
    private readonly byte[] _foreground,_shadow;

    public FontFile Font { get; }
    public int Size { get; }
    public int Baseline => Size;
    public int GlyphHeight => Size;
    public int DialogueLineStep => Size + 1;
    public int MenuLineStep => 16;
    /// <summary>Presentation-only alias for small source glyph LV_2 (F9 05), not ordinary letters L+v.</summary>
    public string LevelPrefix => Supports(LevelPrefixCodepoint) ? "\uE000"
        : throw new InvalidOperationException("LV_2 is supported only by the source small font.");

    private SourceGameFont(FontFile font, int size, HashSet<int> characters, SourceData source,byte[] foreground,byte[] shadow)
    {
        Font = font;
        Size = size;
        _characters = characters;
        _source = source;
        _foreground=foreground;_shadow=shadow;
        _nextTextureIndex = source.Face == "small" ? 2 : 1;
        _supplement = source.Supplement is {} masks ? source.Face == "small" ? masks.Small : masks.Normal : null;
    }

    /// <summary>Load neutral source colors or independently recolor the original foreground/shadow masks.</summary>
    public static SourceGameFont Load(string directory, string face = "normal", Color? foreground = null, Color? shadow = null,
        ColorProfile colorProfile = ColorProfile.SourceFloor)
    {
        if (face is not ("normal" or "small"))
            throw new ArgumentException("Source font face must be normal or small.", nameof(face));
        if (colorProfile is not (ColorProfile.SourceFloor or ColorProfile.MgbaBitReplicated))
            throw new ArgumentOutOfRangeException(nameof(colorProfile));
        byte[] Read(string name) => System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, name));
        var source = new SourceData(face, Read("source-font.json"), Read(face + ".png"),
            face == "small" ? Read("level-prefix.json") : null,
            face == "small" ? Read("level-prefix-small.png") : null, SelectedSupplement.Value);
        return Create(source, foreground, shadow, colorProfile);
    }

    /// <summary>Create an independently owned recolor from retained source masks, without disk reads.
    /// Caller-added FontFile glyphs are not part of the imported source and are not copied.</summary>
    public SourceGameFont CreateColorVariant(Color foreground, Color shadow)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Create(_source, foreground, shadow, ColorProfile.SourceFloor);
    }

    /// <summary>Fade this font's actual display colors, retaining original glyph masks.</summary>
    public SourceGameFont CreatePaletteVariant(int coefficient,ushort target=0)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        byte[] colors=[.._foreground,.._shadow];
        Game.Presentation.QuestLogPalettePresentation.TransformRgba(colors,0,coefficient,target);
        return CreateColorVariant(Color.Color8(colors[0],colors[1],colors[2],colors[3]),
            Color.Color8(colors[4],colors[5],colors[6],colors[7]));
    }

    private static SourceGameFont Create(SourceData input, Color? foreground, Color? shadow, ColorProfile colorProfile)
    {
        string face = input.Face;
        using var document = JsonDocument.Parse(input.Catalog);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Source font identity differs.");
        var source = root.GetProperty("fonts").EnumerateArray().Single(f => f.GetProperty("id").GetString() == face);
        using var decoded = new Image();
        if (decoded.LoadPngFromBuffer(input.Atlas) != Error.Ok
            || decoded.GetFormat() != Image.Format.Rgba8
            || decoded.GetWidth() != source.GetProperty("textureWidth").GetInt32()
            || decoded.GetHeight() != source.GetProperty("textureHeight").GetInt32())
            throw new InvalidDataException("Source font PNG decode failed.");
        var pixels = decoded.GetData();
        var defaultFg = root.GetProperty("foregroundRgba").EnumerateArray().Select(v => v.GetByte()).ToArray();
        var defaultShadow = root.GetProperty("shadowRgba").EnumerateArray().Select(v => v.GetByte()).ToArray();
        // Explicit caller colors already describe the desired display RGB and are never converted again.
        var fg = foreground.HasValue ? Bytes(foreground.Value) : DisplayColor(defaultFg, colorProfile);
        var shade = shadow.HasValue ? Bytes(shadow.Value) : DisplayColor(defaultShadow, colorProfile);
        RecolorPixels(pixels, defaultFg, defaultShadow, fg, shade);
        using var image = Image.CreateFromData(decoded.GetWidth(), decoded.GetHeight(), false, Image.Format.Rgba8, pixels);
        var size = source.GetProperty("pixelSize").GetInt32();
        var cacheSize = new Vector2I(size, 0);
        var font = new FontFile
        {
            FontName = "FireRed source " + face,
            FixedSize = size,
            FixedSizeScaleMode = TextServer.FixedSizeScaleMode.Disable,
            Antialiasing = TextServer.FontAntialiasing.None,
            SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
            MultichannelSignedDistanceField = false,
            GenerateMipmaps = false,
            AllowSystemFallback = false,
            ModulateColorGlyphs = true,
            Oversampling = 1
        };
        try
        {
            font.SetCacheAscent(0, size, size);
            font.SetCacheDescent(0, size, 0);
            font.SetCacheScale(0, size, 1);
            font.SetTextureImage(0, cacheSize, 0, image);
            HashSet<int> characters = [];
            foreach (var glyph in source.GetProperty("glyphs").EnumerateArray())
            {
                var codepoint = glyph.GetProperty("codepoint").GetInt32();
                if (!characters.Add(codepoint)) throw new InvalidDataException("Duplicate source character.");
                var width = glyph.GetProperty("width").GetInt32();
                var height = glyph.GetProperty("height").GetInt32();
                font.SetGlyphAdvance(0, size, codepoint, new Vector2(glyph.GetProperty("advance").GetInt32(), 0));
                font.SetGlyphOffset(0, cacheSize, codepoint, new Vector2(0, -size));
                font.SetGlyphSize(0, cacheSize, codepoint, new Vector2(width, height));
                font.SetGlyphUVRect(0, cacheSize, codepoint, new Rect2(glyph.GetProperty("x").GetInt32(), glyph.GetProperty("y").GetInt32(), width, height));
                font.SetGlyphTextureIdx(0, cacheSize, codepoint, 0);
            }
            // The source charmap assigns both U+2025 and U+2026 to byte 0xB0.
            // Install the same bitmap alias for native editors, which do not call PrepareText.
            if (characters.Contains(0x2026) && characters.Add(0x2025))
            {
                font.SetGlyphAdvance(0, size, 0x2025, font.GetGlyphAdvance(0, size, 0x2026));
                font.SetGlyphOffset(0, cacheSize, 0x2025, font.GetGlyphOffset(0, cacheSize, 0x2026));
                font.SetGlyphSize(0, cacheSize, 0x2025, font.GetGlyphSize(0, cacheSize, 0x2026));
                font.SetGlyphUVRect(0, cacheSize, 0x2025, font.GetGlyphUVRect(0, cacheSize, 0x2026));
                font.SetGlyphTextureIdx(0, cacheSize, 0x2025, font.GetGlyphTextureIdx(0, cacheSize, 0x2026));
            }
            if (face == "small")
            {
                using var levelDocument = JsonDocument.Parse(input.LevelCatalog!);
                var level = levelDocument.RootElement;
                if (level.GetProperty("sourceGlyphId").GetInt32() != 0x105
                    || level.GetProperty("codepoint").GetInt32() != LevelPrefixCodepoint)
                    throw new InvalidDataException("Source LV_2 identity differs.");
                using var levelDecoded = new Image();
                if (levelDecoded.LoadPngFromBuffer(input.LevelAtlas!) != Error.Ok || levelDecoded.GetFormat() != Image.Format.Rgba8
                    || levelDecoded.GetWidth() != 8 || levelDecoded.GetHeight() != 13)
                    throw new InvalidDataException("Source LV_2 PNG decode failed.");
                var levelPixels = levelDecoded.GetData();
                RecolorPixels(levelPixels, defaultFg, defaultShadow, fg, shade);
                using var levelImage = Image.CreateFromData(8, 13, false, Image.Format.Rgba8, levelPixels);
                font.SetTextureImage(0, cacheSize, 1, levelImage);
                font.SetGlyphAdvance(0, size, LevelPrefixCodepoint, new Vector2(8, 0));
                font.SetGlyphOffset(0, cacheSize, LevelPrefixCodepoint, new Vector2(0, -13));
                font.SetGlyphSize(0, cacheSize, LevelPrefixCodepoint, new Vector2(8, 13));
                font.SetGlyphUVRect(0, cacheSize, LevelPrefixCodepoint, new Rect2(0, 0, 8, 13));
                font.SetGlyphTextureIdx(0, cacheSize, LevelPrefixCodepoint, 1);
                characters.Add(LevelPrefixCodepoint);
            }
            if (input.Supplement is {} supplement)
            {
                if (!supplement.Foreground.SequenceEqual(defaultFg) || !supplement.Shadow.SequenceEqual(defaultShadow))
                    throw new InvalidDataException("Supplementary glyph palette differs from source roles.");
                var additional = face == "small" ? supplement.Small : supplement.Normal;
                if (additional.Size != size) throw new InvalidDataException("Supplementary font size differs.");
                foreach (int codepoint in characters)
                    if (additional.Glyphs.ContainsKey(codepoint)) throw new InvalidDataException("Supplement must not replace a source glyph.");
            }
            return new SourceGameFont(font, size, characters, input,fg,shade);
        }
        catch
        {
            font.Dispose();
            throw;
        }
    }

    private static SupplementData LoadSupplement(string directory)
    {
        byte[] Read(string name) => System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, name));
        using var document = JsonDocument.Parse(Read("cjk-font.json"));
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Supplementary font schema differs.");
        var foreground = root.GetProperty("foregroundRgba").EnumerateArray().Select(v => v.GetByte()).ToArray();
        var shadow = root.GetProperty("shadowRgba").EnumerateArray().Select(v => v.GetByte()).ToArray();
        if (foreground.Length != 4 || shadow.Length != 4 || foreground[3] != 255 || shadow[3] != 255 || foreground.SequenceEqual(shadow))
            throw new InvalidDataException("Supplementary font palette is invalid.");
        SupplementFace Face(string id, int expectedSize)
        {
            var source = root.GetProperty("fonts").EnumerateArray().Single(f => f.GetProperty("id").GetString() == id);
            int size = source.GetProperty("pixelSize").GetInt32();
            if (size != expectedSize || source.GetProperty("baseline").GetInt32() != size)
                throw new InvalidDataException("Supplementary glyph baseline differs from source geometry.");
            using var decoded = new Image();
            if (decoded.LoadPngFromBuffer(Read("cjk-" + id + ".png")) != Error.Ok || decoded.GetFormat() != Image.Format.Rgba8 ||
                decoded.GetWidth() != source.GetProperty("textureWidth").GetInt32() ||
                decoded.GetHeight() != source.GetProperty("textureHeight").GetInt32())
                throw new InvalidDataException("Supplementary glyph atlas dimensions or format differ.");
            byte[] pixels = decoded.GetData();
            Dictionary<int, SupplementGlyph> glyphs = [];
            foreach (var glyph in source.GetProperty("glyphs").EnumerateArray())
            {
                int codepoint = glyph.GetProperty("codepoint").GetInt32();
                if (!Rune.IsValid(codepoint) || Rune.IsControl(new Rune(codepoint)) || codepoint is LevelPrefixCodepoint or 0x2025)
                    throw new InvalidDataException("Supplementary glyph is not a display character.");
                int x = glyph.GetProperty("x").GetInt32(), y = glyph.GetProperty("y").GetInt32();
                int width = glyph.GetProperty("width").GetInt32(), height = glyph.GetProperty("height").GetInt32();
                int advance = glyph.GetProperty("advance").GetInt32();
                if (x < 0 || y < 0 || width is < 1 or > 32 || advance is < 1 or > 32 || height != size ||
                    x > decoded.GetWidth() - width || y > decoded.GetHeight() - height)
                    throw new InvalidDataException("Supplementary glyph exceeds its atlas or logical cell.");
                byte[] roles = new byte[width * height];
                for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    var pixel = pixels.AsSpan(((y + row) * decoded.GetWidth() + x + column) * 4, 4);
                    roles[row * width + column] = pixel[3] == 0 ? (byte)0 : pixel.SequenceEqual(foreground) ? (byte)1 :
                        pixel.SequenceEqual(shadow) ? (byte)2 : throw new InvalidDataException("Unknown supplementary glyph pixel role.");
                }
                if (!glyphs.TryAdd(codepoint, new(width, height, advance, roles)))
                    throw new InvalidDataException("Duplicate supplementary glyph.");
            }
            if (glyphs.Count == 0) throw new InvalidDataException("Supplementary font has no glyphs.");
            return new(size, glyphs);
        }
        // Decode once, retain only compact immutable role masks; no colored full atlas per variant.
        return new(foreground, shadow, Face("normal", 14), Face("small", 13));
    }

    /// <summary>Install only requested supplementary glyphs. Native text editors must
    /// call this for committed text and IME preedit text before using the exposed Font.</summary>
    public void EnsureGlyphs(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        if (_supplement is null) return;
        HashSet<GlyphPage> changed = [];
        var cacheSize = new Vector2I(Size, 0);
        foreach (var rune in text.EnumerateRunes())
        {
            if (!_supplement.Glyphs.TryGetValue(rune.Value, out var glyph) || !_installedSupplement.Add(rune.Value)) continue;
            var page = _glyphPages.Count == 0 ? null : _glyphPages[^1];
            if (page is not null && page.X + glyph.Width > GlyphPage.Size) { page.X = 0; page.Y += 16; }
            if (page is null || page.Y + glyph.Height > GlyphPage.Size)
            { page = new(_nextTextureIndex++); _glyphPages.Add(page); }
            int x = page.X, y = page.Y;
            for (int row = 0; row < glyph.Height; row++)
            for (int column = 0; column < glyph.Width; column++)
            {
                byte role = glyph.Roles[row * glyph.Width + column];
                if (role == 0) continue;
                (role == 1 ? _foreground : _shadow).CopyTo(page.Pixels, ((y + row) * GlyphPage.Size + x + column) * 4);
            }
            Font.SetGlyphAdvance(0, Size, rune.Value, new(glyph.Advance, 0));
            Font.SetGlyphOffset(0, cacheSize, rune.Value, new(0, -Size));
            Font.SetGlyphSize(0, cacheSize, rune.Value, new(glyph.Width, glyph.Height));
            Font.SetGlyphUVRect(0, cacheSize, rune.Value, new(x, y, glyph.Width, glyph.Height));
            Font.SetGlyphTextureIdx(0, cacheSize, rune.Value, page.TextureIndex);
            page.X += glyph.Width;
            changed.Add(page);
        }
        foreach (var page in changed)
        {
            using var image = Image.CreateFromData(GlyphPage.Size, GlyphPage.Size, false, Image.Format.Rgba8, page.Pixels);
            Font.SetTextureImage(0, cacheSize, page.TextureIndex, image);
            if (_glyphTextures.TryGetValue(page.TextureIndex, out var clipped)) clipped.Update(image);
        }
        if (changed.Count != 0) Font.EmitChanged();
    }

    /// <summary>Allocate a noncolliding texture for caller-owned special glyph definitions.
    /// The Font retains its image; this method does not dispose the supplied image.</summary>
    public int AddGlyphTexture(Image image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(image);
        int index = _nextTextureIndex++;
        Font.SetTextureImage(0, new(Size, 0), index, image);
        return index;
    }

    /// <summary>Borrow the actual glyph texture for clipped drawing. The font owns it;
    /// callers must not dispose it. Lazy page additions update this same texture.</summary>
    public Texture2D GlyphTexture(int codepoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_installedSupplement.Contains(codepoint) && _supplement?.Glyphs.ContainsKey(codepoint) == true) EnsureGlyphs(new Rune(codepoint).ToString());
        int index = Font.GetGlyphTextureIdx(0, new(Size, 0), codepoint);
        if (index < 0) throw new InvalidDataException("Glyph has no texture.");
        if (!_glyphTextures.TryGetValue(index, out var texture))
        {
            using var image = Font.GetTextureImage(0, new(Size, 0), index);
            texture = ImageTexture.CreateFromImage(image); _glyphTextures.Add(index, texture);
        }
        return texture;
    }

    public bool Supports(int codepoint) => _characters.Contains(codepoint) || codepoint == 0x2025 && _characters.Contains(0x2026) ||
        _supplement?.Glyphs.ContainsKey(codepoint) == true;

    /// <summary>Measure after installing supplementary glyphs; raw Font measurement
    /// cannot discover a glyph which has not yet been requested.</summary>
    public Vector2 MeasureText(string text) => Font.GetStringSize(PrepareText(text), fontSize: Size);

    /// <summary>Explicit fallback to the real source question mark. Reports each unsupported codepoint.</summary>
    public string PrepareText(string text, Action<int>? reportUnsupported = null)
    {
        EnsureGlyphs(text);
        var result = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            // Pinned pret charmap.txt assigns both U+2025 (line 328) and U+2026
            // (line 82) to byte 0xB0. The atlas stores that one source glyph as U+2026.
            if (rune.Value == 0x2025 && Supports(0x2026)) result.Append('…');
            else if (rune.Value == '\n' || Supports(rune.Value)) result.Append(rune.ToString());
            else
            {
                result.Append('?');
                if (reportUnsupported is not null) reportUnsupported(rune.Value);
                else if (_warned.Add(rune.Value)) GD.PushWarning($"Source font has no U+{rune.Value:X4}; using source '?' glyph.");
            }
        }
        return result.ToString();
    }

    private static byte[] Bytes(Color color) => [(byte)color.R8, (byte)color.G8, (byte)color.B8, (byte)color.A8];

    private static void RecolorPixels(byte[] pixels, byte[] defaultFg, byte[] defaultShadow, byte[] fg, byte[] shadow)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0) continue;
            var color = pixels.AsSpan(i, 4).SequenceEqual(defaultFg) ? fg
                : pixels.AsSpan(i, 4).SequenceEqual(defaultShadow) ? shadow
                : throw new InvalidDataException("Unknown source glyph pixel role.");
            color.CopyTo(pixels, i);
        }
    }

    private static byte[] DisplayColor(byte[] sourceFloor, ColorProfile profile)
    {
        if (profile == ColorProfile.SourceFloor) return sourceFloor;
        var result = (byte[])sourceFloor.Clone();
        for (var channel = 0; channel < 3; channel++)
        {
            // Invert the locked floor(rgb5*255/31) palette losslessly, then apply mGBA's captured expansion.
            var five = (sourceFloor[channel] * 31 + 127) / 255;
            if (five * 255 / 31 != sourceFloor[channel])
                throw new InvalidDataException("Source font palette is not the locked RGB555 floor profile.");
            result[channel] = (byte)((five << 3) | (five >> 2));
        }
        return result;
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;
        foreach (var texture in _glyphTextures.Values) texture.Dispose();
        _glyphTextures.Clear();
        _glyphPages.Clear();
        _installedSupplement.Clear();
        Font.Dispose();
    }
}
