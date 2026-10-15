using System.Text.Json;
using Godot;

/// <summary>Small source-derived companions; the existing thirteen skin assets and font atlas stay unchanged.</summary>
public sealed class SourceUiCorrections : IDisposable
{

    private readonly Dictionary<string, ImageTexture> _textures = new(StringComparer.Ordinal);
    private readonly (Color Foreground, Color Shadow)[] _ppColors = new (Color, Color)[4];
    public Color MenuSeparatorColor { get; private set; }

    public static SourceUiCorrections Load(string directory)
    {
        var bytes = File.ReadAllBytes(Path.Combine(directory, "ui-corrections.json"));
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("UI correction source identity differs.");
        var result = new SourceUiCorrections();
        try
        {
            foreach (var (id, width, height) in new[] { ("battle-selection-cursor", 8, 16), ("pc-submenu-background", 240, 160) })
            {
                var entry = root.GetProperty("assets").EnumerateArray().Single(e => e.GetProperty("id").GetString() == id);
                var path = Path.Combine(directory, id + ".png");
                if (new FileInfo(path).LinkTarget is not null) throw new InvalidDataException("UI correction symlink rejected.");
                var png = File.ReadAllBytes(path);                 using var image = new Image();
                if (image.LoadPngFromBuffer(png) != Error.Ok || image.GetFormat() != Image.Format.Rgba8
                    || image.GetWidth() != width || image.GetHeight() != height)
                    throw new InvalidDataException("UI correction image differs.");
                result._textures.Add(id, ImageTexture.CreateFromImage(image));
            }
            static Color ColorFrom(JsonElement value) => Color.Color8(value[0].GetByte(), value[1].GetByte(), value[2].GetByte());
            var pp = root.GetProperty("ppColors");
            if (pp.GetArrayLength() != 4) throw new InvalidDataException("Incomplete PP palettes.");
            for (var i = 0; i < 4; i++) result._ppColors[i] = (ColorFrom(pp[i].GetProperty("foreground")), ColorFrom(pp[i].GetProperty("shadow")));
            result.MenuSeparatorColor = ColorFrom(root.GetProperty("menuSeparatorColor"));
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    /// <summary>battle_message.c:GetCurrentPpToMaxPpState, including the source's special maximum-PP bands.</summary>
    public static int PpPaletteState(int currentPp, int maxPp)
    {
        if (maxPp is < 0 or > 255 || currentPp < 0 || currentPp > maxPp)
            throw new ArgumentOutOfRangeException(nameof(currentPp));
        if (maxPp == currentPp) return 3;
        if (maxPp <= 2) return currentPp > 1 ? 3 : 2 - currentPp;
        if (maxPp <= 7) return currentPp > 2 ? 3 : 2 - currentPp;
        if (currentPp == 0) return 2;
        if (currentPp <= maxPp / 4) return 1;
        if (currentPp > maxPp / 2) return 3;
        return 0;
    }

    public (Color Foreground, Color Shadow) PpColors(int state) => state is >= 0 and < 4 ? _ppColors[state] : throw new ArgumentOutOfRangeException(nameof(state));
    public static Vector2 SelectionCursorPosition(bool moves, int choice)
    {
        if (choice is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(choice));
        return new((moves ? 8 : 128) + (choice & 1) * (moves ? 72 : 56), 120 + (choice & 2) * 8);
    }
    public Texture2D Texture(string id) => _textures[id];
    public void DrawSelectionCursor(CanvasItem canvas, bool moves, int choice)
    {
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        canvas.DrawTexture(_textures["battle-selection-cursor"], SelectionCursorPosition(moves, choice));
    }
    public void DrawPcSubmenuBackground(CanvasItem canvas)
    {
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        canvas.DrawTexture(_textures["pc-submenu-background"], Vector2.Zero);
    }
    public void ClearBattleMenu(CanvasItem canvas) => canvas.DrawRect(LabBattleUiSkin.MessageRect, MenuSeparatorColor);
    public void Dispose() { foreach (var texture in _textures.Values) texture.Dispose(); _textures.Clear(); }
}
