using System.Text.Json;
using Godot;

/// <summary>Pinned source pixels for the male opening Potion PC and ordinary-item bag pages.</summary>
public sealed class OpeningItemUiSkin : IDisposable
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    private static readonly HashSet<string> AssetIds = ["pc-withdraw-background", "pc-deposit-background", "bag-items-background", "potion", "std-frame", "pocket-right-arrow"];
    private readonly Dictionary<string, ImageTexture> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect2> _rects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);

    public static readonly Rect2 LogicalRect = new(0, 0, 240, 160);
    public static readonly Rect2 PcListWindow = new(56, 8, 152, 96);
    public static readonly Rect2 BagListWindow = new(88, 8, 144, 96);
    public static readonly Vector2 DescriptionText = new(40, 115);
    public static readonly Vector2 PcTitleText = new(8, 9);
    public static readonly Vector2 BagTitleCenter = new(44, 9);
    public static readonly Vector2 DepositTitleCenter = new(40, 9);
    public static readonly Rect2 PcActionFrame = new(168, 96, 72, 64);
    public static readonly Rect2 PcSelectedFrame = new(40, 112, 128, 48);
    public static readonly Rect2 PcResultFrame = new(40, 112, 200, 48);
    public static readonly Vector2 PcSelectedText = new(48, 122);
    public static readonly Vector2 PcResultText = new(48, 122);
    public static readonly Rect2 BattleBagActionFrame = new(168, 120, 72, 40);
    public static readonly Rect2 BagSelectedFrame = new(40, 112, 128, 48);
    public static readonly Vector2 BagSelectedText = new(48, 122);
    public const int RowStep = 16;
    // The source passes spacing values, but its English renderer advances by glyph width alone.
    public const int DescriptionLetterSpacing = 0;
    public const int DescriptionLineStep = 14;
    public const int PcListLetterSpacing = 0;
    public const int BagListLetterSpacing = 0;
    public const int PopupLetterSpacing = 0;
    public const int PopupLineStep = 14;
    public const int PcTitleLineStep = 14;
    public static readonly Color MenuTextColor = Color.Color8(99, 99, 99);
    public static readonly Color MenuTextShadow = Color.Color8(214, 214, 206);
    public static readonly Color DescriptionTextColor = Colors.White;
    public static readonly Color DescriptionTextShadow = Color.Color8(99, 99, 99);
    public static readonly Color TitleTextColor = Colors.White;
    public static readonly Color TitleTextShadow = Color.Color8(99, 99, 99);

    // All text coordinates denote glyph-box top-left, not a baseline.
    public static Vector2 ListText(bool pcWithdraw, int row) => new(pcWithdraw ? 65 : 97, RowY(row));
    public static Vector2 ListCursor(bool pcWithdraw, int row) => new(pcWithdraw ? 57 : 89, RowY(row));
    public static Vector2 QuantityText(bool pcWithdraw, int row) => new(pcWithdraw ? 166 : 198, RowY(row));
    public static Vector2 PcActionText(int row) => new(184, ChoiceY(row, 3, 106));
    public static Vector2 PcActionCursor(int row) => new(176, ChoiceY(row, 3, 106));
    public static Vector2 BattleBagActionText(int row) => new(184, ChoiceY(row, 2, 130));
    public static Vector2 BattleBagActionCursor(int row) => new(176, ChoiceY(row, 2, 130));
    private static int RowY(int row) => ChoiceY(row, 6, 10);
    private static int ChoiceY(int row, int count, int y) => row >= 0 && row < count ? y + row * RowStep : throw new ArgumentOutOfRangeException(nameof(row));

    public Texture2D Texture(string id) => _textures[id];
    public Rect2 Rect(string id) => _rects[id];
    /// <summary>Verified source display text. STR_VAR placeholders remain; terminal wait controls are metadata only.</summary>
    public string Text(string symbol) => _texts[symbol];
    public string PotionName => Text("ITEM_POTION_NAME");
    public string PotionDescription => Text("ITEM_POTION_DESCRIPTION");
    public string FormatText(string symbol, string? strVar1 = null, string? strVar2 = null)
    {
        var text = Text(symbol);
        if (strVar1 is not null) text = text.Replace("{STR_VAR_1}", strVar1, StringComparison.Ordinal);
        if (strVar2 is not null) text = text.Replace("{STR_VAR_2}", strVar2, StringComparison.Ordinal);
        if (text.Contains("{STR_VAR_", StringComparison.Ordinal)) throw new ArgumentException("Source text requires its placeholder values.");
        return text;
    }

    public static OpeningItemUiSkin Load(string directory)
    {
        var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "item-ui.json"));
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
)
            throw new InvalidDataException("Opening item UI source identity differs.");
        var result = new OpeningItemUiSkin();
        try
        {
            foreach (var entry in root.GetProperty("texts").EnumerateObject()) result._texts.Add(entry.Name, entry.Value.GetString()!);
            if (result._texts.Count != 33) throw new InvalidDataException("Incomplete opening item UI text set.");
            foreach (var entry in root.GetProperty("assets").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString()!;
                var relative = entry.GetProperty("texturePath").GetString()!;
                if (!AssetIds.Contains(id) || relative != id + ".png" || relative.Contains('/') || relative.Contains('\\') || relative.Contains(".."))
                    throw new InvalidDataException("Unsafe opening item UI texture path.");
                var path = System.IO.Path.Combine(directory, relative);
                if (new FileInfo(path).LinkTarget is not null) throw new InvalidDataException("Opening item UI texture symlink rejected.");
                var png = System.IO.File.ReadAllBytes(path);
                using var image = new Image();
                if (image.LoadPngFromBuffer(png) != Error.Ok || image.GetFormat() != Image.Format.Rgba8
                    || image.GetWidth() != entry.GetProperty("width").GetInt32()
                    || image.GetHeight() != entry.GetProperty("height").GetInt32())
                    throw new InvalidDataException("Opening item UI PNG dimensions/format differ: " + id);
                result._textures.Add(id, ImageTexture.CreateFromImage(image));
                result._rects.Add(id, new Rect2(entry.GetProperty("x").GetInt32(), entry.GetProperty("y").GetInt32(), image.GetWidth(), image.GetHeight()));
            }
            if (!AssetIds.SetEquals(result._textures.Keys)) throw new InvalidDataException("Incomplete opening item UI texture set.");
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    // The containing view owns scale, text, quantity, selection cursor, popup state and inventory mutation.
    public void DrawPcWithdraw(CanvasItem canvas) => Draw(canvas, "pc-withdraw-background");
    public void DrawPcDeposit(CanvasItem canvas) => Draw(canvas, "pc-deposit-background");
    public void DrawBag(CanvasItem canvas) => Draw(canvas, "bag-items-background");
    public void DrawPotion(CanvasItem canvas) => Draw(canvas, "potion");
    public void DrawPocketRightArrow(CanvasItem canvas) => Draw(canvas, "pocket-right-arrow");
    public void Draw(CanvasItem canvas, string id)
    {
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        canvas.DrawTextureRect(_textures[id], _rects[id], false);
    }

    /// <summary>Source blue std window for selected-item/result messages. Action choices use LabBattleUiSkin frame1.</summary>
    public void DrawStandardFrame(CanvasItem canvas, Rect2 outer)
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
            canvas.DrawTextureRectRegion(_textures["std-frame"], new Rect2(outer.Position + new Vector2(x, y), new Vector2(8, 8)), new Rect2(sx, sy, 8, 8));
        }
    }
    public void Dispose() { foreach (var texture in _textures.Values) texture.Dispose(); _textures.Clear(); _rects.Clear(); _texts.Clear(); }
}
