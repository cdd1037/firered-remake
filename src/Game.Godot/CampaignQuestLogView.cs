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
        // Pin both scene and actual saved map; never call LoadMap/Synchronize with recorded data.
        var key=PresentationMapKey(id);_drawMapPins.Add(key);MapCache.SetPins(_drawMapPins.Append(PresentationMapKey(_session.State.World.Player.MapId)));
        var art=GetMapArt(id);
        var actors=playback.Actors.ToArray();var hero=actors.Single(a=>a.Id==CampaignQuestLogRecorder.PlayerId);
        var heroPosition=new Vector2(hero.X*16,hero.Y*16);
        if(playback.MovingFrom is {} movingHero&&movingHero.Id==hero.Id)heroPosition=new Vector2(movingHero.X*16,movingHero.Y*16).Lerp(heroPosition,(float)playback.MovementFraction);
        var camera=heroPosition+new Vector2(8-120,8-80);
        DrawRect(new(0,0,240,160),Colors.Black);
        DrawTexture(art.Bottom,-camera);DrawTexture(art.Middle,-camera);
        foreach(var actor in actors.Where(a=>a.Visible).OrderBy(a=>a.Y).ThenBy(a=>a.Id,StringComparer.Ordinal))
        {
            SpriteAsset? asset=actor.Id==CampaignQuestLogRecorder.PlayerId?(_session.State.PlayerGender==1?_femalePlayer:art.Player):art.Npcs.GetValueOrDefault(actor.Graphics);
            if(asset is null)throw new InvalidDataException("quest_log_actor_art_missing:"+actor.Graphics);
            // Local pose only; the actual saved scene's _poses remain untouched.
            var position=new Vector2(actor.X*16,actor.Y*16);
            if(playback.MovingFrom is {} from&&from.Id==actor.Id)position=new Vector2(from.X*16,from.Y*16).Lerp(position,(float)playback.MovementFraction);
            var pose=new Pose{Position=position,Facing=actor.Facing,Visible=true,Frame=playback.MovingFrom?.Id==actor.Id?1+(int)(playback.MovementFraction*3):0};
            DrawActor(asset,pose,camera);
        }
        DrawTexture(art.Top,-camera);
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
