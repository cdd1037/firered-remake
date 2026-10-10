using Game.Application;
using Game.Content;
using Godot;

/// <summary>Original Gym/Cut assets with presentation clocks only. Command acknowledgements own state changes.</summary>
public partial class CampaignView
{
    private Texture2D? _fieldCutRed, _fieldCutGreen, _fieldCutStreaks;
    private string? _fieldCutWait;
    private double _fieldCutFrames;
    private bool _fieldCutCry;
    private bool FieldCutVisualComplete => _session.State.CutEffect is {} cut && FieldCutFrame >=
        (cut.Phase==FieldCutSourcePresentation.ShowMon?FieldCutSourcePresentation.ShowMonCueFrames:FieldCutSourcePresentation.TreeCutCueFrames);
    private int FieldCutFrame => (int)Math.Floor(_fieldCutFrames+1e-7);
    private int FieldCutPlayerFrame
    {
        get
        {
            int tick=FieldCutFrame;
            for(int i=0;i<FieldCutSourcePresentation.PlayerFrameDurations.Length;i++)
            {int duration=FieldCutSourcePresentation.PlayerFrameDurations[i];if(tick<duration)return i;tick-=duration;}
            return FieldCutSourcePresentation.PlayerFrameDurations.Length-1;
        }
    }
    private int FieldCutTreeFrame => Math.Min(FieldCutSourcePresentation.TreeAnimationFrames-1,
        FieldCutFrame/FieldCutSourcePresentation.TreeFrameDuration);
    private bool FieldCutTreeVisible
    {
        get
        {
            int blink=FieldCutFrame-FieldCutSourcePresentation.TreeAnimationFrames*FieldCutSourcePresentation.TreeFrameDuration;
            return blink<0 || blink<FieldCutSourcePresentation.TreeBlinkFrames && (blink&1)==1;
        }
    }
    private int FieldCutBannerAge => FieldCutFrame-FieldCutSourcePresentation.PlayerFrameDurations.Sum();
    private Rect2 FieldCutBannerWindow
    {
        get
        {
            int age=FieldCutBannerAge;
            if(age<0||age>=FieldCutSourcePresentation.ShowMonCueFrames-FieldCutSourcePresentation.PlayerFrameDurations.Sum())return new();
            if(age<FieldCutSourcePresentation.OutdoorBannerOpenFrames)
            {
                int step=age;
                int x=Math.Max(0,240-step*16),top=Math.Max(40,80-step*2),bottom=Math.Min(120,81+step*2);
                return new(x,top,240-x,bottom-top);
            }
            int closing=age-FieldCutSourcePresentation.OutdoorBannerOpenFrames-FieldCutSourcePresentation.MonSlideInFrames-
                FieldCutSourcePresentation.MonHoldFrames-FieldCutSourcePresentation.MonSlideOutFrames;
            int y=closing<0?40:Math.Min(80,40+closing*6);
            int end=closing<0?120:Math.Max(81,120-closing*6);
            return new(0,y,240,end-y);
        }
    }
    private int FieldCutMonX
    {
        get
        {
            int slide=FieldCutBannerAge-FieldCutSourcePresentation.OutdoorBannerOpenFrames;
            if(slide<0)return FieldCutSourcePresentation.MonStartX;
            if(slide<FieldCutSourcePresentation.MonSlideInFrames)
                return Math.Max(FieldCutSourcePresentation.MonCenterX,FieldCutSourcePresentation.MonStartX-slide*FieldCutSourcePresentation.MonSlidePixelsPerFrame);
            int exit=slide-FieldCutSourcePresentation.MonSlideInFrames-FieldCutSourcePresentation.MonHoldFrames;
            return exit<0?FieldCutSourcePresentation.MonCenterX:FieldCutSourcePresentation.MonCenterX-Math.Min(exit,FieldCutSourcePresentation.MonSlideOutFrames-1)*FieldCutSourcePresentation.MonSlidePixelsPerFrame;
        }
    }

    private void LoadVermilionGymPresentation()
    {
        if(!_catalog.VermilionGymEnabled)return;
        var source=PretVermilionGymAssets.Load(_root);
        foreach(var portrait in source.Portraits)
        {
            using var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,portrait.Rgba);
            _forestTrainerPortraits.Add(portrait.Id,ImageTexture.CreateFromImage(image));
        }
        var icon=source.Document.Icon;
        using(var image=Image.CreateFromData(icon.Width,icon.Height,false,Image.Format.Rgba8,PretPewterMachines.IconRgba(icon)))
            _earlyItemIcons.Add(icon.Item,ImageTexture.CreateFromImage(image));
        var field=PretVermilionGymFieldMoveArt.Load(_root);
        Texture2D Sheet(string id,int width,int height)
        {
            using var image=Image.CreateFromData(width,height,false,Image.Format.Rgba8,field.ReadRgba(id));
            return ImageTexture.CreateFromImage(image);
        }
        _fieldCutRed=Sheet("red-field-move",80,32);_fieldCutGreen=Sheet("green-field-move",80,32);
        _fieldCutStreaks=Sheet("outdoor-streaks",256,80);
    }

    private void SynchronizeFieldCutPresentation(CampaignState? before,CampaignState after)
    {
        string? wait=after.CutEffect is null?null:after.Script.Wait?.CorrelationId;
        if(wait!=_fieldCutWait||before?.CutEffect?.Phase!=after.CutEffect?.Phase)
        { _fieldCutWait=wait;_fieldCutFrames=0;_fieldCutCry=false; }
        if(after.CutEffect is not null){_held=null;_repeat=0;}
    }

    private void ProcessFieldCutPresentation(double delta)
    {
        if(_session.State.CutEffect is not {} cut)return;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        int duration=cut.Phase==FieldCutSourcePresentation.ShowMon?FieldCutSourcePresentation.ShowMonCueFrames:FieldCutSourcePresentation.TreeCutCueFrames;
        _fieldCutFrames=Math.Min(duration,_fieldCutFrames+delta*60);
        if(cut.Phase==FieldCutSourcePresentation.ShowMon&&!_fieldCutCry&&FieldCutFrame>=
            FieldCutSourcePresentation.PlayerFrameDurations.Sum()+FieldCutSourcePresentation.OutdoorBannerOpenFrames+FieldCutSourcePresentation.MonSlideInFrames)
        { _fieldCutCry=true;SourceCry(_session.State.Party[cut.PartyIndex].Pokemon); }
    }

    private void DrawFieldCutActor(string id,SpriteAsset asset,Pose pose,Vector2 camera)
    {
        var cut=_session.State.CutEffect;
        if((cut?.Phase==FieldCutSourcePresentation.ShowMon||_session.State.FlashEffect?.Phase==FieldFlashSourcePresentation.ShowMon)&&id=="LOCALID_PLAYER")
        {
            var position=pose.Position.Floor()+new Vector2(8,8)-asset.Anchor-camera;
            DrawTextureRectRegion(_session.State.PlayerGender==0?_fieldCutRed!:_fieldCutGreen!,new(position,new(16,32)),new((_session.State.FlashEffect is null?FieldCutPlayerFrame:FieldFlashPlayerFrame)*16,0,16,32));
            return;
        }
        if(cut?.Phase==FieldCutSourcePresentation.TreeCut&&id==cut.ActorId)
        {
            if(!FieldCutTreeVisible)return;
            var position=pose.Position.Floor()+new Vector2(8,8)-asset.Anchor-camera;
            DrawTextureRectRegion(asset.Texture,new(position,new(asset.Width,asset.Height)),new(FieldCutTreeFrame*asset.Width,0,asset.Width,asset.Height));
            return;
        }
        DrawSsAnneActor(id,asset,pose,camera);
    }

    private void DrawFieldCutBanner()
    {
        if(_session.State.CutEffect is not {Phase:FieldCutSourcePresentation.ShowMon} cut)return;
        var window=FieldCutBannerWindow;if(window.Size.X<=0||window.Size.Y<=0)return;
        // Source BG0HOFS subtracts 16 every visual tick. Wrap at the original 256px tilemap width.
        int left=(int)window.Position.X,right=(int)window.End.X,y=(int)window.Position.Y,height=(int)window.Size.Y;
        int scroll=FieldCutBannerAge*-16;
        for(int x=left;x<right;)
        {
            int sourceX=((x+scroll)%256+256)%256;int width=Math.Min(right-x,256-sourceX);
            DrawTextureRectRegion(_fieldCutStreaks!,new(x,y,width,height),new(sourceX,y-40,width,height));x+=width;
        }
        int monAge=FieldCutBannerAge-FieldCutSourcePresentation.OutdoorBannerOpenFrames;
        if(monAge<0||monAge>=FieldCutSourcePresentation.MonSlideInFrames+FieldCutSourcePresentation.MonHoldFrames+FieldCutSourcePresentation.MonSlideOutFrames)return;
        var mon=_session.State.Party[cut.PartyIndex].Pokemon;
        // The source OBJ is not clipped by the BG0 reveal window.
        DrawTexture(_battleArt[(mon.SourceSpeciesId,"front",mon.IsShiny)].Texture,new(FieldCutMonX-32,FieldCutSourcePresentation.MonY-32));
    }
}
