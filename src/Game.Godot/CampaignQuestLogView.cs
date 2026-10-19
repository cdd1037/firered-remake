using System.Collections.Immutable;
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
        ResetQuestLogPlayback();
        _questLogPlayback=_session.State.QuestLog is {} log?new(log,_session.State.PlayerName,_catalog.QuestLogSource,CampaignQuestLogRecorder.SavedScene(_session.State,_catalog.QuestLogSource)):null;
        _held=null;_repeat=0;ResetPlayTimeHost();QueueRedraw();
    }
    private void ResetQuestLogPlayback()
    {
        _questLogPlayback=null;_questFogScene=null;_questFogKey=null;ClearCaveTransitionTexture();ClearQuestLogUiPalette();ClearQuestLogPaletteTextures();ResetQuestLogArt();ResetPlayTimeHost();
    }
    private void FinishQuestLogPlayback()
    {
        if(_questLogPlayback?.SavedGameVisible==true)_fieldFogScene=_questFogScene;
        ResetQuestLogPlayback();_held=null;_repeat=0;ResetCampaignButtonInput();QueueRedraw();
        // Map/session/poses were never replaced, so Continue resumes the exact saved field.
    }
    private void DisposeQuestLogPresentation()
    {
        ResetQuestLogPlayback();
        _questLogWindow?.Dispose();_questLogWindow=null;
        _questLogFont?.Dispose();_questLogFont=null;
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
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        // Visit source callback boundaries even after a slow host frame. Audio
        // belongs to the traversed scene, not only the final redraw's projection.
        double remaining=delta;
        while(remaining>0&&!playback.Finished)
        {
            double step=Math.Min(remaining,1d/60);
            int previousScene=playback.SceneIndex;double previousFrame=playback.Frame;
            EnsureQuestFog(playback);var fogKey=_questFogKey;var camera=QuestFogCamera(playback);
            playback.Tick(step);EnsureQuestFog(playback);
            if(fogKey==_questFogKey&&_questFogScene is {} fog)
                _questFogScene=fog.Advance(step*60,(int)camera.X,(int)camera.Y);
            PlayQuestLogTransitionSounds(playback,previousScene,previousFrame);
            remaining-=step;
        }
        if(playback.Finished)FinishQuestLogPlayback();QueueRedraw();return true;
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
        EnsureQuestLogArt();EnsureQuestFog(playback);
        var actors=playback.Actors.ToImmutableArray();
        var poses=new Dictionary<string,Pose>(StringComparer.Ordinal);
        foreach(var actor in actors)
        {
            var movement=playback.Movement(actor.Id);
            var position=new Vector2(actor.X*16,actor.Y*16);
            if(movement is {} active)position=new Vector2(active.From.X*16,active.From.Y*16).Lerp(position,(float)active.Fraction);
            poses.Add(actor.Id,new Pose{Position=position,Facing=actor.Facing,Visible=actor.Visible,
                DrawYOffset=movement?.DrawYOffset??0,IsJump=movement?.IsJump??false,Frame=movement is {} frame?1+(int)(frame.Fraction*3):0});
        }
        var snapshot=new FieldSnapshot(playback,playback.SceneIndex,playback.ArtRevision,playback.SavedGameVisible,
            new(playback.Map,actors,playback.Layout,playback.Tiles){FlashLevel=playback.FlashLevel},poses,playback.Frame,_session.State.PlayerGender){Fog=_questFogScene,Jumps=playback.JumpEffects.ToArray()};
        var warp=QuestLogFieldWarpSample(playback);
        if(playback.ActiveTransitionTiming is {} timing)
        {
            bool arrival=playback.TransitionElapsedFrames>=timing.SwitchFrame;
            var anchor=arrival?timing.ArrivalDoor:timing.DepartureDoor;
            int stairFrame=(int)Math.Floor(playback.TransitionElapsedFrames+1e-7);
            if(arrival&&timing.ArrivalStairBehavior is {} arrivalStair)
                snapshot=WithFieldStairPose(snapshot,FieldStairPresentation.SampleArrival(arrivalStair,stairFrame-timing.SwitchFrame,timing.ArrivalPalette));
            else if(!arrival&&timing.DepartureStairBehavior is {} departureStair)
                snapshot=WithFieldStairPose(snapshot,FieldStairPresentation.SampleDeparture(departureStair,timing.SwitchFrame,stairFrame));
            else if(anchor is not null)
            {
                bool animated=arrival?timing.Arrival==Game.Domain.World.FieldTransitionArrival.AnimatedDoor:
                    timing.Cause==Game.Domain.World.FieldTransitionCause.AnimatedDoor;
                if(animated)snapshot=WithFieldDoorPose(snapshot,warp,FindFieldDoor(snapshot.Projection,anchor.X,anchor.Y),anchor.X,anchor.Y,arrival);
                else
                {
                    var player=actors.Single(a=>a.Id==CampaignQuestLogRecorder.PlayerId);
                    var facing=arrival?Game.Domain.World.Direction.South:anchor.X<player.X?Game.Domain.World.Direction.West:
                        anchor.X>player.X?Game.Domain.World.Direction.East:anchor.Y<player.Y?Game.Domain.World.Direction.North:Game.Domain.World.Direction.South;
                    snapshot=WithFieldWalkPose(snapshot,warp,arrival?anchor.X:player.X,arrival?anchor.Y:player.Y,facing);
                }
            }
        }
        var currentTiming=playback.ActiveTransitionTiming;
        bool arriving=currentTiming is not null&&playback.TransitionElapsedFrames>=currentTiming.SwitchFrame;
        ushort fadeColor=(arriving&&currentTiming?.Interlude==FieldTransitionInterlude.CaveExit||
            !arriving&&currentTiming?.Interlude==FieldTransitionInterlude.CaveEnter)?(ushort)0x7fff:(ushort)0;
        if(currentTiming is {Interlude:not FieldTransitionInterlude.None} interlude&&
            playback.TransitionElapsedFrames>=interlude.DepartureEndFrame&&playback.TransitionElapsedFrames<interlude.SwitchFrame)
        {
            int elapsed=(int)Math.Floor(playback.TransitionElapsedFrames)-interlude.DepartureEndFrame;
            if(interlude.Interlude==FieldTransitionInterlude.CaveExit)DrawCaveExitFrame(elapsed);
            else DrawCaveEnterFrame(elapsed,interlude.Interlude==FieldTransitionInterlude.CavePreviewEnter);
            return true;
        }
        DrawFieldSnapshot(snapshot,playback.GrayscaleCoefficient,warp.BackgroundBlend,warp.ObjectBlend,fadeColor);
        int barHeight=playback.BarHeight;
        if(barHeight>0)
        {
            DrawRect(new(0,0,240,barHeight),QuestLogUiColor(15,warp.BackgroundBlend,fadeColor));
            DrawRect(new(0,160-barHeight,240,barHeight),QuestLogUiColor(15,warp.BackgroundBlend,fadeColor));
        }
        string header=_catalog.QuestLogSource.Texts["PreviouslyOnYourQuest"];
        if(!playback.SavedGameVisible)header+=(playback.SceneCount-playback.SceneIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if(playback.HeaderVisible)SourceText(QuestLogUiFont(warp.BackgroundBlend,fadeColor),new(2,2),header);
        if(playback.Caption is {} caption)
        {
            for(int y=0;y<6;y++)for(int x=0;x<30;x++)
                DrawTextureRectRegion(QuestLogUiWindow(warp.BackgroundBlend,fadeColor),new(x*8,112+y*8,8,8),new(0,(y==0?0:y==5?2:1)*8,8,8));
            var lines=caption.Split('\n');int top=lines.Length==1?17:lines.Length==2?10:3;
            for(int i=0;i<lines.Length;i++)SourceText(QuestLogUiFont(warp.BackgroundBlend,fadeColor),new(2,112+top+i*16),lines[i]);
        }
        DrawFieldDoorWipe(warp);
        return true;
    }
    private void DrawFieldSnapshot(FieldSnapshot snapshot,int grayscale,int backgroundBlend,int objectBlend,ushort fadeColor=0)
    {
        var projection=snapshot.Projection;
        var hero=snapshot.Poses[CampaignQuestLogRecorder.PlayerId];
        var camera=(hero.Position+new Vector2(8-120,8-80)).Floor();
        var neighbors=FieldSnapshotNeighbors(new MapId(projection.Map)).Where(neighbor=>new Rect2(neighbor.Origin,
            new(_catalog.Maps[neighbor.Map].Width*16,_catalog.Maps[neighbor.Map].Height*16)).Intersects(new(camera,new(240,160)))).ToArray();
        PrepareFieldSnapshotPalette(snapshot,grayscale,backgroundBlend,objectBlend,neighbors,fadeColor);
        var art=GetFieldSnapshotMapArt(snapshot,neighbors);
        int backdrop=fadeColor==0x7fff?(31*backgroundBlend)>>4:0;
        byte background=(byte)((backdrop<<3)|(backdrop>>2));
        var backdropColor=Color.Color8(background,background,background);
        Rid? lowerTarget=snapshot.Fog is null?null:BeginFieldFog(backdropColor);
        if(lowerTarget is null)DrawRect(new(0,0,240,160),backdropColor);
        DrawFieldSnapshotLayer(snapshot,art,camera,0,neighbors,lowerTarget);DrawFieldSnapshotLayer(snapshot,art,camera,1,neighbors,lowerTarget);
        if(snapshot.Fog is {} fog)DrawFieldFogSample(fog,FieldSnapshotPaletteTexture(_fieldFogTexture!,false));
        DrawFieldJumpEffects(snapshot.Jumps,projection,snapshot.Poses,camera,art,texture=>FieldSnapshotPaletteTexture(texture,false));
        foreach(var actor in projection.Actors.Where(a=>a.Visible&&snapshot.Poses[a.Id].Visible)
            .OrderBy(a=>snapshot.Poses[a.Id].Position.Y).ThenBy(a=>a.Id,StringComparer.Ordinal))
        {
            bool player=actor.Id==CampaignQuestLogRecorder.PlayerId;
            int gender=CampaignQuestLogAvatar.Gender(actor.Graphics,snapshot.PlayerGender);
            SpriteAsset? asset=player?(CampaignQuestLogAvatar.IsBike(actor.Graphics)?_cityCycleSprites.GetValueOrDefault(gender):gender==1?_femalePlayer:art.Player):art.Npcs.GetValueOrDefault(actor.Graphics);
            if(asset is null)throw new InvalidDataException("field_snapshot_actor_art_missing:"+actor.Graphics);
            var pose=snapshot.Poses[actor.Id];
            if(player&&CampaignQuestLogAvatar.IsSurf(actor.Graphics))
            {
                int phase=(int)Math.Floor(snapshot.Frame+1e-7)/48%2;
                DrawSurfPose(pose,camera,gender,phase,pose.Position,new CampaignFieldMovementPose(0,pose.DrawYOffset,pose.IsJump).RiderYOffset(phase),
                    textureTransform:texture=>FieldSnapshotPaletteTexture(texture,false));
            }
            else
            {
                var original=snapshot.SavedGameVisible?SixIslandPaletteTexture(asset.Texture,false):asset.Texture;
                DrawActor(asset,pose,camera,FieldSnapshotPaletteTexture(original,false));
            }
        }
        DrawFieldSnapshotLayer(snapshot,art,camera,2,neighbors);
        if(projection.FlashLevel is >0)DrawFieldFlashMask(FieldFlashSourcePresentation.Radius(projection.FlashLevel.Value));
    }
}
