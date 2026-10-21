using Godot;

public partial class CampaignView
{
    // Three canvas items split only the lower field from the existing foreground.
    // They inherit this view's transform and visibility and all draw before it:
    // lower map -> copied lower map -> horizontal fog -> normal actors/top/UI.
    private Rid _fieldFogLowerCanvas, _fieldFogCopyCanvas, _fieldFogCanvas;
    private Shader? _fieldFogShader;
    private ShaderMaterial? _fieldFogMaterial;
    private bool _fieldFogFrameActive;
    private static readonly Rect2 FieldFogBounds = new(0, 0, 240, 160);

    private void EnsureFieldFogCompositor()
    {
        if (_fieldFogLowerCanvas.IsValid) return;
        try
        {
            _fieldFogShader = new Shader { Code = """
                shader_type canvas_item;
                render_mode unshaded, blend_disabled;
                uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
                uniform int alpha_a;
                uniform int alpha_b;
                void fragment() {
                    vec4 fog = COLOR;
                    vec4 lower = texture(screen_texture, SCREEN_UV);
                    // Recover exact source bytes, then perform the GBA's saturated
                    // two-coefficient sum in RGB555. All arithmetic is integer-valued.
                    vec3 a = floor(floor(fog.rgb * 255.0 + 0.5) / 8.0);
                    vec3 b = floor(floor(lower.rgb * 255.0 + 0.5) / 8.0);
                    vec3 value = min(vec3(31.0), floor((a * float(alpha_a) + b * float(alpha_b)) / 16.0));
                    vec3 rgb = (value * 8.0 + floor(value / 4.0)) / 255.0;
                    COLOR = fog.a < 0.5 ? lower : vec4(rgb, 1.0);
                }
                """ };
            _fieldFogMaterial = new ShaderMaterial { Shader = _fieldFogShader };
            _fieldFogLowerCanvas = CreateFieldFogCanvas(0);
            _fieldFogCopyCanvas = CreateFieldFogCanvas(1);
            _fieldFogCanvas = CreateFieldFogCanvas(2);
            RenderingServer.CanvasItemSetMaterial(_fieldFogCanvas, _fieldFogMaterial.GetRid());
        }
        catch
        {
            DisposeFieldFogCompositor();
            throw;
        }
    }

    private Rid CreateFieldFogCanvas(int drawIndex)
    {
        var item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(item, GetCanvasItem());
        RenderingServer.CanvasItemSetDrawBehindParent(item, true);
        RenderingServer.CanvasItemSetDrawIndex(item, drawIndex);
        RenderingServer.CanvasItemSetDefaultTextureFilter(item, RenderingServer.CanvasItemTextureFilter.Nearest);
        RenderingServer.CanvasItemSetVisible(item, false);
        return item;
    }

    // Call FIRST in CampaignView._Draw, before an early return or a palette/map
    // owner can be retired. Raw canvas commands otherwise survive parent redraws.
    private void ResetFieldFogCanvas()
    {
        _fieldFogFrameActive = false;
        if (_fieldFogLowerCanvas.IsValid)
        {
            RenderingServer.CanvasItemClear(_fieldFogLowerCanvas);
            RenderingServer.CanvasItemSetVisible(_fieldFogLowerCanvas, false);
        }
        if (_fieldFogCopyCanvas.IsValid)
        {
            RenderingServer.CanvasItemSetCopyToBackbuffer(_fieldFogCopyCanvas, false, FieldFogBounds);
            RenderingServer.CanvasItemSetVisible(_fieldFogCopyCanvas, false);
        }
        if (_fieldFogCanvas.IsValid)
        {
            RenderingServer.CanvasItemClear(_fieldFogCanvas);
            RenderingServer.CanvasItemSetVisible(_fieldFogCanvas, false);
        }
    }

    // The caller must draw its backdrop here, not on CampaignView: a parent
    // backdrop would cover these behind-parent field layers and the fog.
    private Rid BeginFieldFog(Color backdrop)
    {
        EnsureFieldFogCompositor();
        ResetFieldFogCanvas();
        _fieldFogFrameActive = true;
        RenderingServer.CanvasItemSetVisible(_fieldFogLowerCanvas, true);
        RenderingServer.CanvasItemAddRect(_fieldFogLowerCanvas, FieldFogBounds, backdrop);
        return _fieldFogLowerCanvas;
    }

    // The caller supplies source positions, coefficients and an already palette-
    // transformed texture. No current-session lookup is allowed here: snapshots
    // share the same compositor with their own grayscale/object-fade authority.
    private void DrawFieldFog(Texture2D texture, IEnumerable<Vector2> positions, int eva, int evb)
    {
        if (!_fieldFogFrameActive) throw new InvalidOperationException("field_fog_lower_target_missing");
        if (eva is < 0 or > 16 || evb is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(eva));
        _fieldFogMaterial!.SetShaderParameter("alpha_a", eva);
        _fieldFogMaterial.SetShaderParameter("alpha_b", evb);
        foreach (var position in positions) texture.Draw(_fieldFogCanvas, position);
        RenderingServer.CanvasItemSetCopyToBackbuffer(_fieldFogCopyCanvas, true, FieldFogBounds);
        RenderingServer.CanvasItemSetVisible(_fieldFogCopyCanvas, true);
        RenderingServer.CanvasItemSetVisible(_fieldFogCanvas, true);
        _fieldFogFrameActive = false;
    }

    // Texture2D's RID overloads preserve AtlasTexture region behavior and can
    // submit another canvas item's commands synchronously from this view's _Draw.
    // A null target uses the original parent draw path without changing its order.
    private void DrawMapTexture(Texture2D texture, Vector2 position, Rid? target = null)
    {
        if (target is {} canvas) texture.Draw(canvas, position);
        else DrawTexture(texture, position);
    }
    private void DrawMapTextureRegion(Texture2D texture, Rect2 destination, Rect2 source, Rid? target = null)
    {
        if (target is {} canvas) texture.DrawRectRegion(canvas, destination, source);
        else DrawTextureRectRegion(texture, destination, source);
    }

    private void DisposeFieldFogCompositor()
    {
        ResetFieldFogCanvas();
        if (_fieldFogCanvas.IsValid) RenderingServer.FreeRid(_fieldFogCanvas);
        if (_fieldFogCopyCanvas.IsValid) RenderingServer.FreeRid(_fieldFogCopyCanvas);
        if (_fieldFogLowerCanvas.IsValid) RenderingServer.FreeRid(_fieldFogLowerCanvas);
        _fieldFogLowerCanvas = _fieldFogCopyCanvas = _fieldFogCanvas = default;
        _fieldFogMaterial?.Dispose(); _fieldFogMaterial = null;
        _fieldFogShader?.Dispose(); _fieldFogShader = null;
    }
}
