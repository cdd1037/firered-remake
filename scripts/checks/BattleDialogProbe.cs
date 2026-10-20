using Godot;

// Compiled only by test-battle-dialog-text.py in a temporary Godot project.
// The production text/font/surface sources are linked, not reimplemented here.
public partial class BattleDialogProbe : Node2D
{
    private SourceGameFont? _font, _palette;
    private bool _usePalette;
    private readonly SourceDialogSurface _surface = new();
    private bool _cover;
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public override async void _Ready()
    {
        try
        {
            string root = System.Environment.GetEnvironmentVariable("FIRERED_PROJECT_ROOT")
                ?? throw new InvalidOperationException("FIRERED_PROJECT_ROOT missing");
            _font = SourceGameFont.Load(Path.Combine(root,"content/generated/ui/font"),
                foreground: Colors.White, shadow: new Color("636363"));
            Check(_font.Supports('喷'), "Actual Chinese font pack missing");
            AddChild(_surface);
            var text = _surface.Text;
            const string rows = "FIRST\nSECOND\nTHIRD\nFOURTH\nFIFTH";
            text.Bind(rows,_font.Font,_font.Size,16,212,wrap:false);
            text.RevealAll();
            Check(text.PageCount==3 && text.RevealComplete,"Instant initial reveal/pages");
            for(int page=0;page<2;page++)
            {
                Check(text.TryAdvanceVisual(true),"Visual advance");
                Check(!text.RevealComplete,"New page resets reveal");
                text.RevealAll();
                Check(text.RevealComplete,"Instant next-page reveal");
            }
            Check(!text.TryAdvanceVisual(true),"Final visual page boundary");
            Check(text.TryTakeSemanticAcknowledgement(true) && !text.TryTakeSemanticAcknowledgement(true),"Control acknowledgement latch");
            text.Bind(rows,_font.Font,_font.Size,16,212,wrap:false);
            Check(text.VisualPage==0,"Explicit same-text restart");
            const string literal="[b]literal[/b] {PLAYER}\n宝𠮷";
            text.Bind(literal,_font.Font,_font.Size,15,198,wrap:false);
            text.RevealAll();
            Check(text.CurrentVisualText==literal && text.Size==new Vector2(198,30),"Literal Unicode/Oak clip");
            string source=_font.PrepareText("喷火龙使出了喷射火焰！敌人的宝可梦受到了攻击！训练家正在等待下一条战斗消息。\nCHARIZARD used FLAMETHROWER!");
            text.Bind(source,_font.Font,_font.Size,16,212);
            text.RevealAll();
            int count=text.PageCount;
            Check(count>=2,"Actual font must exercise wrapping");
            for(int page=0;page<count;page++)
            {
                Check(text.ContinuationPosition.X<=212.01 && text.ContinuationPosition.Y<=16.01,"Actual font line bounds");
                if(page+1<count)Check(text.TryAdvanceVisual(true),"Actual font page advance");
                text.RevealAll();
            }
            text.Bind(source,_font.Font,_font.Size,16,212);
            text.RevealAll();
            GD.Print($"BATTLE_DIALOG_LAYOUT_PASS size={_font.Size} pages={count} CLR={System.Environment.Version} engine={Engine.GetVersionInfo()["string"]}");
            if(DisplayServer.GetName()!="headless")
            {
                await Rendered();
                using var first=_surface.GetTexture().GetImage();
                Check(Ink(first)>20,"Native surface has no rendered font pixels");
                using var screen=GetViewport().GetTexture().GetImage();
                string output=System.Environment.GetEnvironmentVariable("FIRERED_PROBE_IMAGE")!;
                Check(screen.SavePng(output)==Error.Ok,"Screenshot failed");
                Check(text.TryAdvanceVisual(true),"Rendered second-page advance");
                text.RevealAll(); await Rendered();
                using var second=_surface.GetTexture().GetImage();
                Check(Ink(second)>0 && !first.GetData().SequenceEqual(second.GetData()),"Next native page stale/empty");
                int pageBefore=text.VisualPage, revealBefore=text.PageCodepointCount;
                _palette=_font.CreatePaletteVariant(16,0x7fff); _usePalette=true;
                await Rendered();
                using var faded=_surface.GetTexture().GetImage();
                Check(text.VisualPage==pageBefore && text.PageCodepointCount==revealBefore && text.RevealComplete,"Palette swap reset page/reveal");
                Check(Ink(faded)>0 && !second.GetData().SequenceEqual(faded.GetData()),"White palette fade failed to update native glyphs");
                _usePalette=false; await Rendered();
                _palette.Dispose(); _palette=null;
                text.Bind("WWW",_font.Font,_font.Size,16,(int)_font.MeasureText("WWW").X,wrap:false);
                text.RevealAll(); await Rendered();
                using var narrow=_surface.GetTexture().GetImage();
                Check(Ink(narrow)>0 && text.PageCount==1 && text.ContinuationPosition.X<=narrow.GetWidth(),"Exact-width bitmap text layout");
                _cover=true; await Rendered();
                using var covered=GetViewport().GetTexture().GetImage();
                Check(covered.GetPixel(20,130).R==0 && covered.GetPixel(100,140).R==0,"Later parent overlay fails to cover text");
                text.Clear(); await Rendered();
                using var cleared=_surface.GetTexture().GetImage();
                Check(Ink(cleared)==0,"Cleared surface retains glyphs");
                GD.Print("BATTLE_DIALOG_PIXELS_PASS actual bitmap glyphs, next page, white palette/reveal preservation, exact-width layout, clear, parent overlay; image="+output);
            }
            else GD.Print("BATTLE_DIALOG_PIXELS_SKIPPED: headless layout run has no rendering acceptance");
            GetTree().Quit(0);
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private async Task Rendered()
    {
        for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
    }
    private static int Ink(Image image)
    {
        int ink=0;
        for(int y=0;y<image.GetHeight();y++)for(int x=0;x<image.GetWidth();x++)if(image.GetPixel(x,y).A>.01f)ink++;
        return ink;
    }
    public override void _Process(double delta)=>QueueRedraw();
    public override void _Draw()
    {
        DrawRect(new(0,0,240,160),new Color("183890"));
        if(_font is not null)_surface.Draw(this,new(10,122),_usePalette?_palette!:_font);
        if(_cover)DrawRect(new(0,0,240,160),Colors.Black);
    }
    public override void _ExitTree() { _palette?.Dispose(); _font?.Dispose(); }
}
