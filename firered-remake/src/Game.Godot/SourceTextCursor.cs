using System.Security.Cryptography;
using System.Text.Json;
using Godot;

/// <summary>Pinned text.c 10x12 continuation arrows; presentation only, with no font/window replacement.</summary>
public sealed class SourceTextCursor : IDisposable
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceHash = "73f1b1357dbde966d0a6fa52173e5e4a7e68e53c31d0c567b6378a1012d915b1";
    private const string CatalogHash = "ad04f4b3b4cef50a3c8641b1f0190ead752127a9f34d2cc8237f954d5332dfa9";
    public const int Width = 10;
    public const int Height = 12;
    public const int CursorDelay = 8;
    public const int PresentationTicksPerFrame = CursorDelay + 1;
    private static readonly int[] FrameSequence = [0, 1, 2, 1];
    private readonly ImageTexture[] _textures;
    private bool _disposed;

    private SourceTextCursor(ImageTexture[] textures) => _textures = textures;

    /// <summary>Load both original index/palette variants, rejecting missing, changed or unsupported assets.</summary>
    public static SourceTextCursor Load(string directory)
    {
        var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "text-cursor.json"));
        CheckHash(bytes, CatalogHash, "catalog");
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("profile").GetString() != "firered-text-down-arrow-v1"
            || root.GetProperty("sourceCommit").GetString() != SourceCommit
            || root.GetProperty("fullSourceHash").GetString() != SourceHash)
            throw new InvalidDataException("Source cursor identity differs.");
        var textures = new List<ImageTexture>();
        try
        {
            foreach (var id in new[] { "red", "alternate" })
            {
                var asset = root.GetProperty("assets").EnumerateArray().Single(a => a.GetProperty("id").GetString() == id);
                var png = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, id + ".png"));
                CheckHash(png, asset.GetProperty("pngSha256").GetString()!, id + " PNG");
                using var image = new Image();
                if (image.LoadPngFromBuffer(png) != Error.Ok || image.GetFormat() != Image.Format.Rgba8
                    || image.GetWidth() != Width * 3 || image.GetHeight() != Height)
                    throw new InvalidDataException("Source cursor PNG decode failed.");
                CheckHash(image.GetData(), asset.GetProperty("rgbaSha256").GetString()!, id + " RGBA");
                textures.Add(ImageTexture.CreateFromImage(image));
            }
            return new SourceTextCursor(textures.ToArray());
        }
        catch
        {
            foreach (var texture in textures) texture.Dispose();
            throw;
        }
    }

    /// <summary>Tick zero is the first visible sourceX=0 frame; this is not a full source 60fps scheduler.</summary>
    public static int FrameIndex(long presentationTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(presentationTicks);
        return FrameSequence[(int)(presentationTicks / PresentationTicksPerFrame % FrameSequence.Length)];
    }

    /// <summary>Source calls this a Y-coordinate array, but passes it to the blitter's sourceX argument.</summary>
    public static int SourceX(long presentationTicks) => FrameIndex(presentationTicks) * 16;

    /// <summary>Verified raw atlas for diagnostics; ownership stays with this cursor.</summary>
    public Texture2D Texture(bool alternate = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _textures[alternate ? 1 : 0];
    }

    /// <summary>
    /// Draw at the final line's currentX/currentY, in window-adjusted logical pixels; add no further offset.
    /// The caller redraws the window background first, removing the previous phase's transparent pixels.
    /// Field/Oak dialogue uses red; ordinary battle messages use the alternate palette/tile region.
    /// </summary>
    public void Draw(CanvasItem canvas, Vector2 position, long presentationTicks, bool alternate = false)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)
            || position.X != Mathf.Floor(position.X) || position.Y != Mathf.Floor(position.Y))
            throw new ArgumentException("Source cursor requires integral logical pixel coordinates.", nameof(position));
        var frame = FrameIndex(presentationTicks);
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        canvas.DrawTextureRectRegion(Texture(alternate), new Rect2(position, new Vector2(Width, Height)),
            new Rect2(frame * Width, 0, Width, Height));
    }

    private static void CheckHash(byte[] data, string expected, string label)
    {
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(data)), expected, StringComparison.Ordinal))
            throw new InvalidDataException("Source cursor " + label + " hash differs; run the verified cursor import.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var texture in _textures) texture.Dispose();
        _disposed = true;
    }
}
