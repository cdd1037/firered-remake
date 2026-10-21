using Game.Domain.Battle;
using Game.Application;
using Game.Content;
using Game.Domain.Capture;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{
    private CaptureBallPresentationAssets _captureBallAssets = null!;
    private Texture2D _captureBallAtlas = null!, _captureParticleAtlas = null!, _captureTrainerAtlas = null!;
    private CaptureBallPresentation? _captureBallPlayback;
    private string? _captureBallPlaybackKey;
    private int _captureBallCueTick = -1;
    private readonly Dictionary<(Texture2D Texture,int Color,int Blend),ImageTexture> _captureTargetTints=[];
    private void ClearCaptureTargetTints()
    { foreach(var texture in _captureTargetTints.Values)texture.Dispose(); _captureTargetTints.Clear(); }
    private void LoadCaptureBallPresentation()
    {
        ClearCaptureTargetTints(); _captureBallPlayback=null; _captureBallPlaybackKey=null;
        _captureBallAssets=CaptureBallPresentationAssets.Load(_root);
        var texture=Texture(_captureBallAssets.TexturePath);
        if(texture.GetWidth()!=192||texture.GetHeight()!=48)throw new InvalidDataException("Capture ball atlas dimensions differ.");
        _captureBallAtlas=texture;
        _captureParticleAtlas=Texture(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_captureBallAssets.TexturePath)!,"particles.png"));
        _captureTrainerAtlas=Texture(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_captureBallAssets.TexturePath)!,"trainers.png"));
        if(_captureTrainerAtlas.GetWidth()!=128||_captureTrainerAtlas.GetHeight()!=320)throw new InvalidDataException("Capture trainer atlas dimensions differ.");
        if(_captureParticleAtlas.GetWidth()!=8||_captureParticleAtlas.GetHeight()!=64)throw new InvalidDataException("Capture particles dimensions differ.");
    }
    private CaptureBallPresentation? WildCaptureBallPlayback(CampaignWildBattle battle)
    {
        if(battle.Phase!=CampaignBattlePhase.Events||WildCurrentEvent(battle) is not SourceCaptureResultEvent result)return null;
        string key=battle.Id+":"+result.Sequence;
        if(_captureBallPlaybackKey!=key)
        {
            ClearCaptureTargetTints();
            var art=SingleBattleArt(battle,WildBattleSide.Opponent,battle.DisplayOpponent);
            _captureBallPlayback=new(result.Ball,result.Outcome,result.AnimationCode,_captureBallAssets.Sine,
                (int)art.Position.X+32,(int)art.Position.Y+32);
            _captureBallPlaybackKey=key;_captureBallCueTick=-1;
        }
        return _captureBallPlayback;
    }
    private int WildCaptureBallTick => (int)Math.Min(int.MaxValue,Math.Floor(_wildElapsed*60+1e-9));
    private bool WildCaptureBallBlocking(CampaignWildBattle battle) => WildCaptureBallPlayback(battle) is {} playback&&!playback.Complete(WildCaptureBallTick);
    private void ProcessWildCaptureBall(CampaignWildBattle battle)
    {
        if(WildCaptureBallPlayback(battle) is not {} playback)return;
        PlayCaptureBallCues(playback,WildCaptureBallTick);
    }
    private void PlayCaptureBallCues(CaptureBallPresentation playback,int tick)
    {
        foreach(var cue in playback.Cues)
            if(cue.Tick>_captureBallCueTick&&cue.Tick<=tick)
            {
                if(cue.Symbol.StartsWith("MUS_",StringComparison.Ordinal))SourceMusic(cue.Symbol);
                else SourceSound(cue.Symbol);
            }
        _captureBallCueTick=Math.Max(_captureBallCueTick,tick);
    }
    private bool DrawCapturedTarget(CampaignWildBattle battle,Texture2D texture,Vector2 position)
    {
        if(WildCaptureBallPlayback(battle) is not {} playback)return false;
        DrawCaptureTargetFrame(texture,position,playback.Sample(WildCaptureBallTick),playback.Ball,playback.TargetBlend(WildCaptureBallTick));
        return true;
    }
    private void DrawCaptureTargetFrame(Texture2D texture,Vector2 position,CaptureBallFrame frame,CaptureBall ball,int blend)
    {
        if(frame.TargetScale<=0)return;
        Vector2 size=new Vector2(texture.GetWidth(),texture.GetHeight())*frame.TargetScale;
        Vector2 center=position+new Vector2(texture.GetWidth()/2f,texture.GetHeight()/2f+frame.TargetOffsetY);
        DrawTextureRect(CaptureTargetTint(texture,ball,blend),new Rect2(center-size/2,size),false);
    }
    private Texture2D CaptureTargetTint(Texture2D source,CaptureBall ball,int blend)
    {
        if(blend==0)return source;
        int color=_captureBallAssets.FadeColors[(int)ball-1];
        var key=(source,color,blend);
        if(_captureTargetTints.TryGetValue(key,out var texture))return texture;
        using var image=source.GetImage(); image.Convert(Image.Format.Rgba8);
        var bytes=image.GetData();
        for(int i=0;i<bytes.Length;i+=4)
            for(int c=0;c<3;c++)
            {
                int channel=((bytes[i+c]>>3)*(16-blend)+((color>>(c*5))&31)*blend)>>4;
                bytes[i+c]=(byte)((channel<<3)|(channel>>2));
            }
        using var tinted=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,bytes);
        texture=ImageTexture.CreateFromImage(tinted);_captureTargetTints.Add(key,texture);return texture;
    }
    private void DrawWildCaptureBall(CampaignWildBattle battle)
    {
        if(WildCaptureBallPlayback(battle) is {} playback)DrawCapturePlayback(playback,WildCaptureBallTick);
    }
    private CaptureBallPresentation? SafariCaptureBallPlayback()
    {
        if(_session.State.SafariBattle is not { Phase:CampaignBattlePhase.Events,PageIndex:1,Capture:null } battle)return null;
        var result=battle.Events.OfType<Game.Domain.Safari.SafariCaptureEvent>().SingleOrDefault()?.Capture;
        if(result?.AnimationCode is not {} code)return null;
        string key="safari:"+battle.Id+":"+battle.State.BattleTurnCounter;
        if(_captureBallPlaybackKey!=key)
        {
            ClearCaptureTargetTints();
            var target=battle.State.Target;
            var art=_battleArt[(PresentedRenderSpecies(target),"front",target.IsShiny)];
            _captureBallPlayback=new(CaptureBall.Safari,result.Outcome,code,_captureBallAssets.Sine,
                (int)art.Position.X+32,(int)art.Position.Y+32,55,_session.State.PlayerGender==0?91:93,_captureBallAssets.TrainerThrowFrames[0].Ticks);
            _captureBallPlaybackKey=key;_captureBallCueTick=-1;
        }
        return _captureBallPlayback;
    }
    private int SafariCaptureBallTick => (int)Math.Min(int.MaxValue,Math.Floor(_safariElapsed*60+1e-9));
    private bool SafariCaptureBallBlocking => SafariCaptureBallPlayback() is {} playback&&!playback.Complete(SafariCaptureBallTick);
    private void DrawSafariCaptureTrainer()
    {
        if(SafariCaptureBallPlayback() is null)
        { DrawTexture(_forestTrainerPortraits[_session.State.PlayerGender==0?"red-back":"leaf-back"],new(48,48)); return; }
        var frame=_captureBallAssets.TrainerFrame(SafariCaptureBallTick);
        DrawTextureRectRegion(_captureTrainerAtlas,new Rect2(80+frame.XOffset,48,64,64),
            new Rect2(_session.State.PlayerGender*64,frame.Frame*64,64,64));
    }
    private void ProcessSafariCaptureBall()
    {
        if(SafariCaptureBallPlayback() is {} playback)PlayCaptureBallCues(playback,SafariCaptureBallTick);
    }
    private void DrawCapturePlayback(CaptureBallPresentation playback,int tick)
    {
        DrawCaptureBallFrame(playback.Ball,playback.Sample(tick));
        foreach(var particle in playback.Particles(tick))
        {
            DrawSetTransform(new Vector2(particle.X,particle.Y),0,new Vector2(particle.FlipH?-1:1,1));
            DrawTextureRectRegion(_captureParticleAtlas,new Rect2(-4,-4,8,8),new Rect2(0,particle.TileFrame*8,8,8));
            DrawSetTransform(Vector2.Zero);
        }
    }
    private void DrawCaptureBallFrame(CaptureBall ball,CaptureBallFrame frame)
    {
        if(!frame.BallVisible)return;
        DrawSetTransform(new Vector2(frame.X,frame.Y),frame.Rotation*Mathf.Tau/256f);
        DrawTextureRectRegion(_captureBallAtlas,new Rect2(-8,-8,16,16),
            new Rect2(_captureBallAssets.Column(ball),frame.TileFrame*16,16,16),
            new Color(frame.BallBrightness,frame.BallBrightness,frame.BallBrightness,frame.BallAlpha));
        DrawSetTransform(Vector2.Zero);
    }
}
