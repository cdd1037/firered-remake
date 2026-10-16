using Game.Application;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private CampaignQuestLogPlayback? _questLogPlayback;
    private ImageTexture? _questLogWindow;
    private SourceGameFont? _questLogFont;
    private Color QuestLogColor(int index)
    {
        var color=_catalog.QuestLogSource.WindowPalette[index];
        return Color.Color8((byte)color[0],(byte)color[1],(byte)color[2]);
    }
    private void EnsureQuestLogArt()
    {
        if(_questLogWindow is not null)return;
        var pixels=new byte[8*24*4];var source=_catalog.QuestLogSource;
        for(int i=0;i<source.WindowPixels.Length;i++)
        {
            var color=source.WindowPalette[source.WindowPixels[i]];
            for(int component=0;component<3;component++)pixels[i*4+component]=(byte)color[component];
            pixels[i*4+3]=255;
        }
        using var image=Image.CreateFromData(8,24,false,Image.Format.Rgba8,pixels);
        _questLogWindow=ImageTexture.CreateFromImage(image);
        _questLogFont=SourceGameFont.Load(Path.Combine(_root,"content/generated/ui/font"),foreground:QuestLogColor(1),shadow:QuestLogColor(12));
    }
    private bool QuestLogPlaybackActive=>_questLogPlayback is not null;
    private void StartQuestLogPlayback()
    {
        _questLogPlayback=_session.State.QuestLog is {} log?new(log,_session.State.PlayerName,_catalog.QuestLogSource,CampaignQuestLogRecorder.SavedScene(_session.State,_catalog.QuestLogSource)):null;
        _held=null;_repeat=0;ResetPlayTimeHost();QueueRedraw();
    }
    private void ResetQuestLogPlayback(){_questLogPlayback=null;ResetPlayTimeHost();}
    private void FinishQuestLogPlayback()
    {
        _questLogPlayback=null;_held=null;_repeat=0;ResetCampaignButtonInput();ResetPlayTimeHost();QueueRedraw();
        // Map/session/poses were never replaced, so Continue resumes the exact saved field.
    }
    // The common field-loop early returns exclude menus, Help, recap and effects.
    // Reuse the actual input lock boundary rather than maintaining a Quest whitelist.
    private void ObserveQuestLogFieldStep()
    {
        if(!_busy&&!FieldPresentationInputLocked)_session.ObserveQuestLogFieldFrame(Engine.GetPhysicsFrames());
    }
    private bool TickQuestLogPlayback(double delta)
    {
        if(_questLogPlayback is not {} playback)return false;
        playback.Tick(delta);if(playback.Finished)FinishQuestLogPlayback();QueueRedraw();return true;
    }
    private bool TryQuestLogInput(InputEvent input)
    {
        if(_questLogPlayback is not {} playback)return false;
        bool next=input is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Enter or Key.Space} or InputEventJoypadButton{Pressed:true,ButtonIndex:JoyButton.A} or InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Left};
        bool skip=input is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Escape or Key.Backspace} or InputEventJoypadButton{Pressed:true,ButtonIndex:JoyButton.B} or InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Right};
        if(skip)playback.Skip();else if(next)playback.NextScene();
        if(playback.Finished)FinishQuestLogPlayback();
        GetViewport().SetInputAsHandled();QueueRedraw();return true;
    }
    private bool DrawQuestLogPlayback()
    {
        if(_questLogPlayback is not {} playback)return false;
        EnsureQuestLogArt();
        var id=new MapId(playback.Map);
        var art=GetQuestLogMapArt(playback);
        var actors=playback.Actors.ToArray();var hero=actors.Single(a=>a.Id==CampaignQuestLogRecorder.PlayerId);
        var heroPosition=new Vector2(hero.X*16,hero.Y*16);
        if(playback.Movement(hero.Id) is {} movingHero)heroPosition=new Vector2(movingHero.From.X*16,movingHero.From.Y*16).Lerp(heroPosition,(float)movingHero.Fraction);
        var camera=heroPosition+new Vector2(8-120,8-80);
        DrawRect(new(0,0,240,160),Colors.Black);
        DrawQuestLogLayer(art,camera,0);DrawQuestLogLayer(art,camera,1);
        foreach(var actor in actors.Where(a=>a.Visible).OrderBy(a=>a.Y).ThenBy(a=>a.Id,StringComparer.Ordinal))
        {
            bool player=actor.Id==CampaignQuestLogRecorder.PlayerId;
            int gender=CampaignQuestLogAvatar.Gender(actor.Graphics,_session.State.PlayerGender);
            SpriteAsset? asset=player?(CampaignQuestLogAvatar.IsBike(actor.Graphics)?_cityCycleSprites.GetValueOrDefault(gender):gender==1?_femalePlayer:art.Player):art.Npcs.GetValueOrDefault(actor.Graphics);
            if(asset is null)throw new InvalidDataException("quest_log_actor_art_missing:"+actor.Graphics);
            // Local pose only; the actual saved scene's _poses remain untouched.
            var position=new Vector2(actor.X*16,actor.Y*16);
            var movement=playback.Movement(actor.Id);
            if(movement is {} active)position=new Vector2(active.From.X*16,active.From.Y*16).Lerp(position,(float)active.Fraction);
            var pose=new Pose{Position=position,Facing=actor.Facing,Visible=true,Frame=movement is {} frame?1+(int)(frame.Fraction*3):0};
            if(player&&CampaignQuestLogAvatar.IsSurf(actor.Graphics))
            {
                // Shared source sheet placement; replay owns no live Surf effect or field clock.
                int phase=(int)Math.Floor(playback.Frame+1e-7)/48%2;
                DrawSurfPose(pose,camera,gender,phase,pose.Position,phase);
            }
            else DrawActor(asset,pose,camera,playback.SavedGameVisible?null:asset.Texture);
        }
        DrawQuestLogLayer(art,camera,2);
        DrawRect(new(0,0,240,16),QuestLogColor(15));
        DrawRect(new(0,144,240,16),QuestLogColor(15));
        string header=_catalog.QuestLogSource.Texts["PreviouslyOnYourQuest"];
        if(!playback.SavedGameVisible)header+=(playback.SceneCount-playback.SceneIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceText(_questLogFont!,new(2,2),header);
        if(playback.Caption is {} caption)
        {
            for(int y=0;y<6;y++)for(int x=0;x<30;x++)
                DrawTextureRectRegion(_questLogWindow!,new(x*8,112+y*8,8,8),new(0,(y==0?0:y==5?2:1)*8,8,8));
            var lines=caption.Split('\n');int top=lines.Length==1?17:lines.Length==2?10:3;
            for(int i=0;i<lines.Length;i++)SourceText(_questLogFont!,new(2,112+top+i*16),lines[i]);
        }
        return true;
    }
}
