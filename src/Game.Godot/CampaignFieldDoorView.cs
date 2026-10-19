using System.Collections.Immutable;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private sealed record FieldDoorPaint(FieldDoorArtEntry Art,int X,int Y,int Frame);
    private GeneratedFieldDoorArt? _fieldDoorArt;

    private FieldDoorArtEntry? FindFieldDoor(CampaignQuestLogMapProjection projection,int x,int y)
    {
        var id=new MapId(projection.Map);
        var source=projection.Layout is {} layout?(PretWorldMap)(_catalog.MapLayout(id,layout)??throw new InvalidDataException("field_door_layout_missing")):_catalog.SourceMaps[id];
        var cell=source.GetSourceCell(x,y);
        if(cell is null)return null;
        int metatile=projection.Tiles.IsDefault?cell.MetatileId:projection.Tiles.FirstOrDefault(t=>t.X==x&&t.Y==y)?.SourceMetatileId??cell.MetatileId;
        return (_fieldDoorArt??=GeneratedFieldDoorArt.Load(Path.Combine(_root,"content/generated")))
            .Find(source.Events.GetProperty("layout").GetString()!,metatile);
    }

    private Texture2D PaintFieldDoorLayer(CampaignResourceCache.Owner owner,Texture2D original,int layer,FieldDoorPaint door)
    {
        var atlas=SceneTextures.NativeTexture(Path.Combine(_root,"content/generated",_fieldDoorArt!.TexturePath));
        var region=door.Art.FrameRegion(door.Frame,layer);
        using var canvas=original.GetImage();
        using var pixels=atlas.GetImage();
        // Source replaces all three layers, including transparent pixels. Restoring
        // closed state simply selects the unchanged captured map art again.
        canvas.BlitRect(pixels,new(region.X,region.Y,region.Width,region.Height),new(door.X*16,door.Y*16-door.Art.Height+16));
        return owner.FromImage(canvas);
    }

    private static FieldSnapshot WithFieldDoorPose(FieldSnapshot snapshot,FieldDoorPresentation.PhaseSample sample,
        FieldDoorArtEntry? art,int doorX,int doorY,bool arrival)
    {
        snapshot=WithFieldWalkPose(snapshot,sample,doorX,arrival?doorY:doorY+1,
            arrival?Game.Domain.World.Direction.South:Game.Domain.World.Direction.North);
        return snapshot with{Door=art is not null&&sample.DoorFrame>=0?new(art,doorX,doorY,sample.DoorFrame):null};
    }

    private static FieldSnapshot WithFieldWalkPose(FieldSnapshot snapshot,FieldDoorPresentation.PhaseSample sample,
        int startX,int startY,Game.Domain.World.Direction facing)
    {
        var poses=snapshot.Poses.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        var old=poses[CampaignQuestLogRecorder.PlayerId];
        var delta=facing switch
        {
            Game.Domain.World.Direction.North=>new Vector2(0,-1),Game.Domain.World.Direction.South=>new Vector2(0,1),
            Game.Domain.World.Direction.West=>new Vector2(-1,0),Game.Domain.World.Direction.East=>new Vector2(1,0),
            _=>throw new InvalidDataException("field_walk_facing_invalid")
        };
        poses[CampaignQuestLogRecorder.PlayerId]=new Pose
        {
            Position=new Vector2(startX*16,startY*16)+delta*sample.WalkPixels,
            Facing=facing,Visible=sample.PlayerVisible,Frame=FieldDoorPresentation.NormalWalkPoseFrame(sample.WalkPixels),
            DrawYOffset=old.DrawYOffset,IsJump=false
        };
        return snapshot with{Poses=poses};
    }

    private static FieldSnapshot WithFieldStairPose(FieldSnapshot snapshot,FieldStairPresentation.PhaseSample sample)
    {
        var poses=snapshot.Poses.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        var old=poses[CampaignQuestLogRecorder.PlayerId];
        poses[CampaignQuestLogRecorder.PlayerId]=new Pose
        {
            Position=old.Position,Facing=old.Facing,Visible=old.Visible,
            Frame=sample.WalkFrame,DrawXOffset=sample.OffsetX,DrawYOffset=sample.OffsetY
        };
        // x2/y2 are sprite offsets. The source camera remains on the logical tile.
        return snapshot with{Poses=poses};
    }

    private void PlayQuestLogTransitionSounds(CampaignQuestLogPlayback playback,int previousScene,double previousFrame)
    {
        if(playback.ActiveTransitionTiming is not {Cause:Game.Domain.World.FieldTransitionCause.PlainWarp or Game.Domain.World.FieldTransitionCause.AnimatedDoor or Game.Domain.World.FieldTransitionCause.NonAnimatedDoor or Game.Domain.World.FieldTransitionCause.StairWarp} timing||previousScene!=playback.SceneIndex)return;
        double elapsed=playback.TransitionElapsedFrames;
        double prior=elapsed-(playback.Frame-previousFrame);
        // Audio cues follow crossed source callback boundaries, never redraws.
        bool arrival=elapsed>=timing.SwitchFrame;
        // A scene can begin on a warp marker at frame0; its immediate cue
        // belongs to the first positive tick, before any later crossed callbacks.
        int first=prior==0?0:Math.Max(0,(int)Math.Floor(prior+1e-7)+1);
        int last=(int)Math.Floor(elapsed+1e-7);
        bool doorCue=false;
        for(int frame=first;frame<=last;frame++)
        {
            var sample=FieldDoorPresentation.SampleRecorded(timing.Cause,timing.Arrival,timing.SwitchFrame,frame,timing.DepartureStairBehavior,timing.ArrivalStairBehavior,timing.EffectiveDeparture,timing.Interlude,timing.ArrivalPalette);
            doorCue|=sample.OpenDoorSoundRequested;
            bool exit=timing.Cause==Game.Domain.World.FieldTransitionCause.PlainWarp&&timing.EffectiveDeparture==Game.Domain.World.FieldTransitionDeparture.Immediate&&frame==0;
            if(timing.EffectiveDeparture==Game.Domain.World.FieldTransitionDeparture.StepOntoWarp&&frame<timing.SwitchFrame&&sample.MusicFadeRequested)
                exit=frame==0||!FieldDoorPresentation.SampleRecorded(timing.Cause,timing.Arrival,timing.SwitchFrame,frame-1,departureKind:timing.EffectiveDeparture,interludeKind:timing.Interlude,arrivalPalette:timing.ArrivalPalette).MusicFadeRequested;
            if(timing.Cause==Game.Domain.World.FieldTransitionCause.StairWarp&&frame==1)exit=true;
            if(exit)SourceSound("SE_EXIT");
        }
        var anchor=arrival?timing.ArrivalDoor:timing.DepartureDoor;
        if(!doorCue||anchor is null)return;
        var projection=new CampaignQuestLogMapProjection(playback.Map,playback.Actors.ToImmutableArray(),playback.Layout,playback.Tiles);
        if(FindFieldDoor(projection,anchor.X,anchor.Y) is {} art)SourceSound(art.SoundEffect);
    }

    private void DrawFieldDoorWipe(FieldDoorPresentation.PhaseSample sample)
    {
        if(!sample.WipeActive)return;
        // Primary field palette entry0 is explicitly loaded RGB_BLACK by source.
        // Disabled WININ layers reveal that backdrop, including over recap UI.
        if(sample.WipeLeft>0)DrawRect(new(0,0,sample.WipeLeft,160),Colors.Black);
        if(sample.WipeRight<240)DrawRect(new(sample.WipeRight,0,240-sample.WipeRight,160),Colors.Black);
    }
}
