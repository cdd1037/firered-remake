using System.Text.Json;
using Godot;

/// <summary>Pinned source pixels for settled lab single battles and ordinary field dialogue.</summary>
public sealed class LabBattleUiSkin : IDisposable
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    private readonly Dictionary<string, ImageTexture> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect2> _rects = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string,Texture2D>? _userWindows;
    /// <summary>Borrow authenticated selected-frame textures; the view owns their lifetime.</summary>
    public void SetUserWindowAssets(IReadOnlyDictionary<string,Texture2D>? textures)=>_userWindows=textures;
    public static readonly Rect2 LogicalRect = new(0, 0, 240, 160);
    public static readonly Rect2 MessageRect = new(0, 112, 240, 48);
    public static readonly Rect2 DialogueInterior = new(16, 120, 208, 32);
    public static readonly Vector2 DialogueText = new(16, 121);
    public static readonly Vector2 BattleMessageText = new(10, 122);
    public static readonly Vector2 PlayerName = new(142, 75);
    public static readonly Vector2 OpponentName = new(20, 17);
    public static readonly Vector2 PlayerLevelWindow = new(198, 75);
    public static readonly Vector2 OpponentLevelWindow = new(76, 17);
    public static readonly Vector2 PlayerCurrentHpText = new(186, 93);
    public static readonly Vector2 PlayerMaximumHpText = new(206, 93);
    public static readonly Rect2 PlayerCurrentHpWindow = new(182, 88, 24, 16);
    public static readonly Rect2 PlayerMaximumHpWindow = new(206, 88, 16, 16);
    public int DarkenCoefficient { get; private init; }
    public static readonly Color BattleTextColor = new(1, 1, 1);
    public static readonly Color BattleTextShadow = Color.Color8(107, 90, 115);
    public static readonly Color MenuTextColor = Color.Color8(74, 74, 74);
    public static readonly Color MenuTextShadow = Color.Color8(214, 214, 206);
    public static readonly Color DialogueTextColor = Color.Color8(99, 99, 99);
    public static readonly Color DialogueTextShadow = Color.Color8(214, 214, 206);
    public static readonly Color HealthboxTextColor = Color.Color8(66, 66, 66);
    public static readonly Color HealthboxTextShadow = Color.Color8(222, 214, 181);

    public Texture2D Texture(string id) => _textures[id];
    public Rect2 Rect(string id) => _rects[id];
    public static Vector2 ActionText(int choice) => choice switch
    {
        0 => new(136, 122), 1 => new(192, 122), 2 => new(136, 138), 3 => new(192, 138),
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };
    public static Vector2 MoveText(int choice) => choice switch
    {
        0 => new(16, 121), 1 => new(88, 121), 2 => new(16, 137), 3 => new(88, 137),
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };

    public static LabBattleUiSkin Load(string directory, int darkenCoefficient = 0)
    {
        ValidateDarkenCoefficient(darkenCoefficient);
        var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "lab-battle-ui.json"));
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Lab battle UI source identity differs.");
        var result = new LabBattleUiSkin { DarkenCoefficient = darkenCoefficient };
        try
        {
            foreach (var entry in root.GetProperty("assets").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString()!;
                var relative = entry.GetProperty("texturePath").GetString()!;
                if (relative != id + ".png" || relative.Contains('/') || relative.Contains('\\') || relative.Contains(".."))
                    throw new InvalidDataException("Unsafe lab UI texture path.");
                var path = System.IO.Path.Combine(directory, relative);
                if (new FileInfo(path).LinkTarget is not null) throw new InvalidDataException("Lab UI texture symlink rejected.");
                var png = System.IO.File.ReadAllBytes(path);
                using var image = new Image();
                if (image.LoadPngFromBuffer(png) != Error.Ok || image.GetFormat() != Image.Format.Rgba8
                    || image.GetWidth() != entry.GetProperty("width").GetInt32()
                    || image.GetHeight() != entry.GetProperty("height").GetInt32())
                    throw new InvalidDataException("Lab UI PNG dimensions/format differ: " + id);
                var pixels = image.GetData();
                // Hash verification always precedes this derived display palette. Original files are untouched.
                if (darkenCoefficient != 0)
                    for (var i = 0; i < pixels.Length; i += 4)
                        if (pixels[i + 3] != 0)
                            for (var channel = 0; channel < 3; channel++)
                                pixels[i + channel] = DarkenChannel(pixels[i + channel], darkenCoefficient);
                using var display = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, pixels);
                result._textures.Add(id, ImageTexture.CreateFromImage(display));
                result._rects.Add(id, new Rect2(entry.GetProperty("x").GetInt32(), entry.GetProperty("y").GetInt32(), image.GetWidth(), image.GetHeight()));
            }
            if (result._textures.Count != 13) throw new InvalidDataException("Incomplete lab UI texture set.");
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    // Coordinates are original 240x160 pixels. The containing view owns the integer scale.
    public void DrawBackground(CanvasItem canvas, Color? tint = null) => Draw(canvas, "background", tint);
    public void DrawBattleMessage(CanvasItem canvas) => Draw(canvas, "battle-message");
    public void DrawActionMenu(CanvasItem canvas) => Draw(canvas, "action-menu");
    public void DrawMoveMenu(CanvasItem canvas) => Draw(canvas, "move-menu");
    public void DrawDialogue(CanvasItem canvas,Func<Texture2D,Texture2D>? palette=null) => Draw(canvas, "dialogue",palette:palette);
    public void Draw(CanvasItem canvas, string id, Color? tint = null,Func<Texture2D,Texture2D>? palette=null)
    {
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        var texture=_userWindows is not null&&_userWindows.TryGetValue(id,out var selected)?selected:_textures[id];
        canvas.DrawTextureRect(palette is null?texture:palette(texture), _rects[id], false, tint ?? Colors.White);
    }

    /// <summary>Settled HP/EXP pixels only. No damage animation, status icon or game-state mutation.</summary>
    public void DrawHealthbox(CanvasItem canvas, bool player, int hp, int maxHp, int expPixels = 0, Color? tint = null, bool showHpLabel = true)
    {
        if (maxHp <= 0 || hp < 0 || hp > maxHp || expPixels is < 0 or > 64)
            throw new ArgumentOutOfRangeException(nameof(hp), "HP/EXP values are outside the supported settled state.");
        var color = tint ?? Colors.White;
        Draw(canvas, player ? "healthbox-player" : "healthbox-opponent", color);
        var origin = player ? new Vector2(158, 88) : new Vector2(36, 30);
        if (showHpLabel) canvas.DrawTexture(_textures["hp-label"], origin, color);
        var pixels = hp == 0 ? 0 : Math.Max(1, (int)((long)hp * 48 / maxHp));
        var bar = pixels > 24 ? "hp-green" : pixels > 9 ? "hp-yellow" : "hp-red";
        DrawBar(canvas, bar, origin + new Vector2(16, 0), 6, pixels, color);
        if (player) DrawBar(canvas, "exp", new Vector2(158, 104), 8, expPixels, color);
    }

    private void DrawBar(CanvasItem canvas, string id, Vector2 origin, int tiles, int pixels, Color tint)
    {
        for (var tile = 0; tile < tiles; tile++)
        {
            var fill = Math.Clamp(pixels - tile * 8, 0, 8);
            canvas.DrawTextureRectRegion(_textures[id], new Rect2(origin + new Vector2(tile * 8, 0), new Vector2(8, 8)), new Rect2(fill * 8, 0, 8, 8), tint);
        }
    }

    /// <summary>The original frame1 nine-tile window. Outer bounds must use whole source tiles.</summary>
    public void DrawStandardFrame(CanvasItem canvas, Rect2 outer)=>DrawWindowFrame(canvas,outer,_textures["frame1"]);
    /// <summary>Source LoadStdWindowFrameGfx actually uses the player's selected USER frame.</summary>
    public void DrawUserFrame(CanvasItem canvas,Rect2 outer,Func<Texture2D,Texture2D>? palette=null)
    {
        var texture=_userWindows is not null&&_userWindows.TryGetValue("frame",out var selected)?selected:_textures["frame1"];
        DrawWindowFrame(canvas,outer,palette is null?texture:palette(texture));
    }
    private static void DrawWindowFrame(CanvasItem canvas, Rect2 outer,Texture2D texture)
    {
        if (outer.Size.X < 24 || outer.Size.Y < 24 || outer.Size.X % 8 != 0 || outer.Size.Y % 8 != 0
            || outer.Position.X % 1 != 0 || outer.Position.Y % 1 != 0)
            throw new ArgumentException("Frame bounds require integer coordinates and whole 8px tiles.", nameof(outer));
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        for (var y = 0; y < (int)outer.Size.Y; y += 8)
        for (var x = 0; x < (int)outer.Size.X; x += 8)
        {
            var sx = x == 0 ? 0 : x == (int)outer.Size.X - 8 ? 16 : 8;
            var sy = y == 0 ? 0 : y == (int)outer.Size.Y - 8 ? 16 : 8;
            canvas.DrawTextureRectRegion(texture, new Rect2(outer.Position + new Vector2(x, y), new Vector2(8, 8)), new Rect2(sx, sy, 8, 8));
        }
    }

    /// <summary>Source BlendPalette toward RGB_BLACK, in five-bit color space. No fade timing.</summary>
    public static Color DarkenColor(Color color, int coefficient = 8)
    {
        ValidateDarkenCoefficient(coefficient);
        if (coefficient == 0 || color.A8 == 0) return color;
        return Color.Color8(DarkenChannel((byte)color.R8, coefficient), DarkenChannel((byte)color.G8, coefficient),
            DarkenChannel((byte)color.B8, coefficient), (byte)color.A8);
    }

    private static byte DarkenChannel(byte color, int coefficient)
    {
        var value = color >> 3;
        value += (-value * coefficient) >> 4;
        return (byte)((value << 3) | (value >> 2));
    }

    private static void ValidateDarkenCoefficient(int coefficient)
    {
        if (coefficient is < 0 or > 16)
            throw new ArgumentOutOfRangeException(nameof(coefficient), "Source palette coefficient must be 0..16.");
    }
    public void Dispose() { foreach (var texture in _textures.Values) texture.Dispose(); _textures.Clear(); _rects.Clear(); }
}
