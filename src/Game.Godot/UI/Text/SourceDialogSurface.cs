using Godot;

/// <summary>
/// Hosts the shared native text control offscreen so an immediate-mode scene can
/// composite it between its existing frame, menus and fade commands. The viewport
/// and texture are retained; neither glyph layout nor gameplay belongs here.
/// Add to the scene tree before binding Text. Draw only when the host needs it.
/// </summary>
public partial class SourceDialogSurface : SubViewport
{
    public SourceDialogText Text { get; } = new();

    public SourceDialogSurface()
    {
        TransparentBg = true;
        Disable3D = true;
        GuiDisableInput = true;
        HandleInputLocally = false;
        RenderTargetClearMode = ClearMode.Always;
        RenderTargetUpdateMode = UpdateMode.Disabled;
        CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest;
        Size = new(2, 2);
        AddChild(Text);
    }

    public void Draw(CanvasItem host, Vector2 position, SourceGameFont font)
    {
        // Palette variants have identical metrics. Install their glyph masks
        // before switching the font without resetting pagination/reveal state.
        Text.SetDisplayFont(font);
        host.DrawTexture(PrepareTexture(), position);
    }

    public Texture2D PrepareTexture()
    {
        Vector2I size = new((int)Text.Size.X, (int)Text.Size.Y);
        if (Size != size) Size = size;
        RenderTargetUpdateMode = UpdateMode.Once;
        return GetTexture();
    }
}
