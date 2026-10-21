using Game.Application;
using Game.Content;
using Game.Presentation.BattleAnimations;
using Godot;

public partial class CampaignView
{
    private FieldForestPreviewLayer? _forestPreviewLayer;
    private BackBufferCopy? _forestPreviewCopy;
    private ShaderMaterial? _forestPreviewMaterial;
    private ImageTexture? _forestPreviewTexture;
    private (MapPreviewEntry Entry,int Blend)? _forestPreviewColorState;
    private readonly byte[] _forestPreviewPixels=new byte[240*160*4];

    private void DrawForestPreview(MapPreviewEntry entry,int blend,int alphaA,int alphaB,bool showName)
    {
        if(_forestPreviewLayer is null)
        {
            var shader=new Shader{Code="""
                shader_type canvas_item;
                uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
                uniform int alpha_a;
                uniform int alpha_b;
                void fragment() {
                    vec4 foreground = COLOR;
                    // Transparent texels leave prior BG0 primitives (including the name window) intact.
                    if (foreground.a < 0.5) { discard; }
                    vec4 field = texture(screen_texture, SCREEN_UV);
                    vec3 a = floor(foreground.rgb * 255.0 / 8.0 + 0.0001);
                    vec3 b = floor(field.rgb * 255.0 / 8.0 + 0.0001);
                    vec3 value = min(vec3(31.0), floor((a * float(alpha_a) + b * float(alpha_b)) / 16.0));
                    vec3 rgb = (value * 8.0 + floor(value / 4.0)) / 255.0;
                    COLOR = vec4(rgb, 1.0);
                }
                """};
            _forestPreviewMaterial=new ShaderMaterial{Shader=shader};
            _forestPreviewCopy=new BackBufferCopy{CopyMode=BackBufferCopy.CopyModeEnum.Viewport};AddChild(_forestPreviewCopy);
            _forestPreviewLayer=new FieldForestPreviewLayer{Material=_forestPreviewMaterial};AddChild(_forestPreviewLayer);
        }
        var art=entry.Art;
        if(_forestPreviewColorState!=(entry,blend))
        {
        for(int y=0;y<160;y++)for(int x=0;x<240;x++)
        {
            int index=art.Indices[y*art.Width+x],offset=(y*240+x)*4;
            ushort color=SourcePaletteMath.Blend(art.Palette[index],blend,0);
            for(int c=0;c<3;c++){int value=(color>>(c*5))&31;_forestPreviewPixels[offset+c]=(byte)((value<<3)|(value>>2));}
            _forestPreviewPixels[offset+3]=(byte)((index&15)==0?0:255);
        }
        using var image=Image.CreateFromData(240,160,false,Image.Format.Rgba8,_forestPreviewPixels);
        if(_forestPreviewTexture is null)_forestPreviewTexture=ImageTexture.CreateFromImage(image);else _forestPreviewTexture.Update(image);
        _forestPreviewColorState=(entry,blend);
        }
        ushort Fade(int index)=>SourcePaletteMath.Blend(art.Palette[index],blend,0);
        var key=(Fade(20),Fade(19));
        if(!_mapPreviewFonts.TryGetValue(key,out var font))
        {
            _mapPreviewFont??=SourceGameFont.Load(Path.Combine(_root,"content/generated/ui/font"));
            _mapPreviewFonts.Add(key,font=_mapPreviewFont.CreateColorVariant(CaveColor(key.Item1),CaveColor(key.Item2)));
        }
        _forestPreviewMaterial!.SetShaderParameter("alpha_a",alphaA);_forestPreviewMaterial.SetShaderParameter("alpha_b",alphaB);
        _forestPreviewLayer.Picture=_forestPreviewTexture;_forestPreviewLayer.TextFont=font;
        _forestPreviewLayer.Label=entry.Name;_forestPreviewLayer.WindowColor=CaveColor(Fade(17));_forestPreviewLayer.ShowName=showName;
        _forestPreviewLayer.Visible=true;_forestPreviewCopy!.Visible=true;_forestPreviewLayer.QueueRedraw();
    }
    private void ClearForestPreview()
    {
        if(_forestPreviewLayer is {} layer){layer.Visible=false;layer.QueueFree();_forestPreviewLayer=null;}
        if(_forestPreviewCopy is {} copy){copy.Visible=false;copy.QueueFree();_forestPreviewCopy=null;}
        _forestPreviewTexture?.Dispose();_forestPreviewTexture=null;_forestPreviewColorState=null;
        _forestPreviewMaterial?.Shader?.Dispose();_forestPreviewMaterial?.Dispose();_forestPreviewMaterial=null;
    }
}
