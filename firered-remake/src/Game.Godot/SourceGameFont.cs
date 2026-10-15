using System.Text;
using System.Text.Json;
using Godot;

/// <summary>Local, pinned English FireRed bitmap glyphs and source advances. No system-font substitution.</summary>
public sealed class SourceGameFont : IDisposable
{
    /// <summary>SourceFloor retains the importer golden. MgbaBitReplicated matches the captured emulator RGB555 output.</summary>
    public enum ColorProfile { SourceFloor, MgbaBitReplicated }

    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public const int LevelPrefixCodepoint = 0xE000;
    private readonly HashSet<int> _characters;
    private readonly HashSet<int> _warned = [];
    private bool _disposed;

    public FontFile Font { get; }
    public int Size { get; }
    public int Baseline => Size;
    public int GlyphHeight => Size;
    public int DialogueLineStep => Size + 1;
    public int MenuLineStep => 16;
    /// <summary>Presentation-only alias for small source glyph LV_2 (F9 05), not ordinary letters L+v.</summary>
    public string LevelPrefix => Supports(LevelPrefixCodepoint) ? "\uE000"
        : throw new InvalidOperationException("LV_2 is supported only by the source small font.");

    private SourceGameFont(FontFile font, int size, HashSet<int> characters)
    {
        Font = font;
        Size = size;
        _characters = characters;
    }

    /// <summary>Load neutral source colors or independently recolor the original foreground/shadow masks.</summary>
    public static SourceGameFont Load(string directory, string face = "normal", Color? foreground = null, Color? shadow = null,
        ColorProfile colorProfile = ColorProfile.SourceFloor)
    {
        if (face is not ("normal" or "small"))
            throw new ArgumentException("Source font face must be normal or small.", nameof(face));
        if (colorProfile is not (ColorProfile.SourceFloor or ColorProfile.MgbaBitReplicated))
            throw new ArgumentOutOfRangeException(nameof(colorProfile));
        var catalogBytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "source-font.json"));
        using var document = JsonDocument.Parse(catalogBytes);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Source font identity differs.");
        var source = root.GetProperty("fonts").EnumerateArray().Single(f => f.GetProperty("id").GetString() == face);
        var png = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, face + ".png"));
        using var decoded = new Image();
        if (decoded.LoadPngFromBuffer(png) != Error.Ok
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
            if (face == "small")
            {
                var levelBytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "level-prefix.json"));
                using var levelDocument = JsonDocument.Parse(levelBytes);
                var level = levelDocument.RootElement;
                if (level.GetProperty("sourceGlyphId").GetInt32() != 0x105
                    || level.GetProperty("codepoint").GetInt32() != LevelPrefixCodepoint)
                    throw new InvalidDataException("Source LV_2 identity differs.");
                var levelPng = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "level-prefix-small.png"));
                using var levelDecoded = new Image();
                if (levelDecoded.LoadPngFromBuffer(levelPng) != Error.Ok || levelDecoded.GetFormat() != Image.Format.Rgba8
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
            return new SourceGameFont(font, size, characters);
        }
        catch
        {
            font.Dispose();
            throw;
        }
    }

    public bool Supports(int codepoint) => _characters.Contains(codepoint);

    /// <summary>Explicit fallback to the real source question mark. Reports each unsupported codepoint.</summary>
    public string PrepareText(string text, Action<int>? reportUnsupported = null)
    {
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
        Font.Dispose();
    }
}
