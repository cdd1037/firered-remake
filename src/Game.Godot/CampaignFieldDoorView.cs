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
        var poses=snapshot.Poses.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        var old=poses[CampaignQuestLogRecorder.PlayerId];
        poses[CampaignQuestLogRecorder.PlayerId]=new Pose
        {
            Position=new(doorX*16,(arrival?doorY*16:(doorY+1)*16)+(arrival?sample.WalkPixels:-sample.WalkPixels)),
            Facing=arrival?Game.Domain.World.Direction.South:Game.Domain.World.Direction.North,
            Visible=sample.PlayerVisible,Frame=FieldDoorPresentation.NormalWalkPoseFrame(sample.WalkPixels),
            DrawYOffset=old.DrawYOffset,IsJump=false
        };
        return snapshot with{Poses=poses,Door=art is not null&&sample.DoorFrame>=0?new(art,doorX,doorY,sample.DoorFrame):null};
    }

    private void PlayQuestLogDoorSounds(CampaignQuestLogPlayback playback,int previousScene,double previousFrame)
    {
        if(playback.ActiveTransitionTiming is not {} timing||previousScene!=playback.SceneIndex)return;
        double elapsed=playback.TransitionElapsedFrames;
        double prior=elapsed-(playback.Frame-previousFrame);
        // Audio cues follow crossed source callback boundaries, never redraws.
        bool arrival=elapsed>=timing.SwitchFrame;
        var anchor=arrival?timing.ArrivalDoor:timing.DepartureDoor;
        if(anchor is null)return;
        int first=Math.Max(arrival?timing.SwitchFrame:0,(int)Math.Floor(prior+1e-7)+1);
        int last=(int)Math.Floor(elapsed+1e-7);
        bool cue=false;
        for(int frame=first;frame<=last;frame++)
            cue|=FieldDoorPresentation.SampleRecorded(timing.DepartureDoor is not null,timing.ArrivalDoor is not null,
                timing.SwitchFrame,frame).OpenDoorSoundRequested;
        if(!cue)return;
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
