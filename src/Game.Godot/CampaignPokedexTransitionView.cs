using Game.Content;
using Godot;

public partial class CampaignView
{
    private ulong _pokedexTransitionStart;
    private int _pokedexTransitionKind,_pokedexPendingPage,_pokedexPendingSlot;
    private bool _pokedexTurnPageChanged;
    private ColorRect? _pokedexPageTurnOverlay;
    private ShaderMaterial? _pokedexPageTurnMaterial;
    private bool PokedexTransitionActive=>_pokedexTransitionKind!=0;
    private void DisposePokedexVisualPresentation()
    {
        _pokedexTransitionKind=0;_pokedexAreaIcon=null;
        if(_pokedexPageTurnOverlay is not null)
        {_pokedexPageTurnOverlay.Visible=false;_pokedexPageTurnOverlay.QueueFree();_pokedexPageTurnOverlay=null;}
        _pokedexPageTurnMaterial?.Shader?.Dispose();_pokedexPageTurnMaterial?.Dispose();_pokedexPageTurnMaterial=null;
    }
    private void BeginPokedexPageTurn(int direction,int page,int slot)
    {
        _pokedexPendingPage=page;_pokedexPendingSlot=slot;
        _pokedexTurnPageChanged=false;
        if(direction>0)SourceSound("SE_BALL_TRAY_ENTER");
        _pokedexTransitionKind=direction<0?1:2;_pokedexTransitionStart=Time.GetTicksMsec();
        if(_pokedexPageTurnOverlay is null)
        {
            var shader=new Shader {Code="""
                shader_type canvas_item;
                uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
                uniform int columns[30];
                uniform vec4 blank_color : source_color;
                uniform vec3 fade_color5;
                uniform int fade_level;
                void fragment() {
                    int dst = clamp(int(UV.x * 30.0), 0, 29);
                    int src = columns[dst];
                    vec2 uv = SCREEN_UV;
                    // Convert logical tile columns to the stretched canvas screen span.
                    // Eight physical pixels is wrong when the 240-wide canvas is enlarged.
                    float span = SCREEN_PIXEL_SIZE.x / max(abs(dFdx(UV.x)), 0.0000001);
                    uv.x += float(src - dst) * span / 30.0;
                    vec4 pixel = src == 30 ? blank_color : texture(screen_texture, uv);
                    if (fade_level > 0) {
                        vec3 rgb5 = floor(pixel.rgb * 255.0 / 8.0 + 0.0001);
                        rgb5 = floor((rgb5 * float(16-fade_level) + fade_color5 * float(fade_level)) / 16.0);
                        pixel.rgb = (rgb5 * 8.0 + floor(rgb5 / 4.0)) / 255.0;
                    }
                    COLOR = pixel;
                }
                """};
            _pokedexPageTurnMaterial=new ShaderMaterial{Shader=shader};
            _pokedexPageTurnOverlay=new ColorRect{Position=Vector2.Zero,Size=new(240,160),MouseFilter=Control.MouseFilterEnum.Ignore,
                Material=_pokedexPageTurnMaterial};
            AddChild(_pokedexPageTurnOverlay);
        }
        // Blank source tile 12 is a uniform paper color, read from generated pixels.
        var blank=_pokedexMenu!.Document.Visuals.Sprites[PokedexPalette+"/tile/12"];
        using var image=PokedexTexture(blank.Texture).GetImage();
        _pokedexPageTurnMaterial!.SetShaderParameter("blank_color",image.GetPixel(blank.SourceX,blank.SourceY));
        var fade=_pokedexMenu.Document.Visuals.Sprites[PokedexPalette+"/fade"];
        var rgb=image.GetPixel(fade.SourceX,fade.SourceY);
        _pokedexPageTurnMaterial.SetShaderParameter("fade_color5",new Vector3(Mathf.Floor(rgb.R*255/8),Mathf.Floor(rgb.G*255/8),Mathf.Floor(rgb.B*255/8)));
        _pokedexPageTurnMaterial.SetShaderParameter("fade_level",0);
        _pokedexPageTurnOverlay.Visible=true;
    }
    private void BeginPokedexZoom(bool reverse=false)
    {
        _pokedexTransitionKind=reverse?4:3;_pokedexTransitionStart=Time.GetTicksMsec();
    }
    private void UpdatePokedexTransition()
    {
        if(!PokedexTransitionActive)return;
        int frame=(int)((Time.GetTicksMsec()-_pokedexTransitionStart)*60/1000);
        if(_pokedexTransitionKind<=2)
        {
            var phase=PokedexTurnGeometry.Frame(_pokedexMenu!.Document.Visuals,_pokedexTransitionKind==2,frame);
            if(phase.UseNewPage&&!_pokedexTurnPageChanged)
            {
                _pokedexHabitatPage=_pokedexPendingPage;_pokedexHabitatSlot=_pokedexPendingSlot;SetPokedexHabitatPage();
                _pokedexTurnPageChanged=true;
                if(_pokedexTransitionKind==1)SourceSound("SE_BALL_TRAY_ENTER");
            }
            if(phase.Complete){_pokedexPageTurnOverlay!.Visible=false;_pokedexTransitionKind=0;return;}
            _pokedexPageTurnMaterial!.SetShaderParameter("columns",_pokedexMenu.Document.Visuals.PageTurnColumns[phase.ColumnPhase].ToArray());
            _pokedexPageTurnMaterial.SetShaderParameter("fade_level",phase.FadeLevel);
        }
        else if(frame>=7)
        {
            bool opening=_pokedexTransitionKind==3;_pokedexTransitionKind=0;
            _fieldMenuMode=opening?"pokedex-entry":"pokedex-habitat";
            if(opening)PlayPokedexCry();
        }
    }
    private void DrawPokedexZoom()
    {
        if(_pokedexTransitionKind is not (3 or 4))return;
        int frame=Math.Min(6,(int)((Time.GetTicksMsec()-_pokedexTransitionStart)*60/1000));
        int scale=_pokedexTransitionKind==3?frame:6-frame;
        var slot=_pokedexMenu!.Document.HabitatLayouts[_pokedexList!.Rows.Length-1][_pokedexHabitatSlot];
        var (left,top,width,height,div)=PokedexZoomGeometry.Frame(slot,scale);
        void Tile(int n,int x,int y,bool flip=false)
        {
            var sprite=_pokedexMenu.Document.Visuals.Sprites[PokedexPalette+"/tile/"+n];
            DrawTextureRectRegion(PokedexTexture(sprite.Texture),new(x*8+(flip?8:0),y*8,flip?-8:8,8),
                new(sprite.SourceX,sprite.SourceY,8,8));
        }
        for(int x=left;x<=left+width+1;x++)
        {Tile(x==left||x==left+width+1?4:5,x,top,x==left+width+1);Tile(x==left||x==left+width+1?10:11,x,top+height+1,x==left+width+1);}
        for(int y=top+1;y<=top+height;y++)
            for(int x=left;x<=left+width+1;x++)
                Tile(x==left||x==left+width+1?(y<div?6:y==div?7:9):(y<div?1:y==div?8:2),x,y,x==left+width+1);
    }
}
