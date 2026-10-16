using System.Globalization;
using Game.Application;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private const string EventBirthMap="map.birth-island-exterior",EventSummitMap="map.navel-rock-summit";
    private long _eventFieldSequence=-1;
    private (int FromX,int FromY,int ToX,int ToY,int Ticks)? _eventTriangleMove;
    private double _eventMoveFrames,_eventDestroyFrames=-1,_eventLugiaFrames=-1,_eventWingFrames=-1;
    private int _eventWingCount;
    private bool _eventThunderPlayed;
    private readonly Dictionary<(Texture2D Texture,int Blend),ImageTexture> _eventWhiteTextures=[];
    private static int EventFrame(double frames)=>(int)Math.Floor(frames+1e-7);
    private Vector2 EventIslandCameraFocus(Vector2 playerPosition)=>_session.State.World.Player.MapId.Value==EventSummitMap&&
        _session.State.Actors.TryGetValue("LOCALID_CAMERA",out var camera)&&camera.Active&&camera.Invisible&&_poses.TryGetValue("LOCALID_CAMERA",out var pose)
            ?pose.Position:playerPosition;
    private Vector2 EventIslandCameraOffset
    {
        get
        {
            var lugia=_eventLugiaFrames<0?(X:0,Y:0):EventIslandFieldPresentation.LugiaShake(EventFrame(_eventLugiaFrames));
            return new(lugia.X,lugia.Y+(_eventDestroyFrames<0?0:EventIslandFieldPresentation.RockShake(EventFrame(_eventDestroyFrames))));
        }
    }
    private void SynchronizeEventIslandFieldPresentation(CampaignState? before,CampaignState after)
    {
        if(before?.World.Player.MapId!=after.World.Player.MapId){long seen=_eventFieldSequence;ResetEventIslandFieldPresentation();_eventFieldSequence=seen;}
        if(after.DeoxysRockEffect is null)
        {
            _eventTriangleMove=null;_eventMoveFrames=0;_eventDestroyFrames=-1;_eventThunderPlayed=false;ClearEventWhiteTextures();
        }
        var effects=after.PresentationEffects.Where(e=>e.Opcode is "DeoxysTrianglePaletteResolved" or "DeoxysTriangleMoveResolved" or
            "DeoxysRockDestroyResolved" or "HoOhWingFlapResolved" or "LugiaShakeResolved").OrderBy(e=>e.Sequence).ToArray();
        if(before is null){_eventFieldSequence=effects.LastOrDefault()?.Sequence??-1;return;}
        foreach(var effect in effects.Where(e=>e.Sequence>_eventFieldSequence).ToArray())
        {
            var a=effect.Args;
            switch(effect.Opcode)
            {
                case "DeoxysTrianglePaletteResolved":
                    if(after.World.Player.MapId.Value!=EventBirthMap||a.Length!=3||a[1]!="10"||a[2]!="ApplyGlobalFieldPaletteTint"||
                        !int.TryParse(a[0],out int index)||index is <0 or >10||after.Script.QuestLogState!=0)
                        throw new InvalidDataException("Unsupported event triangle palette/tint context.");
                    break;
                case "DeoxysTriangleMoveResolved":
                    if(after.World.Player.MapId.Value!=EventBirthMap||a.Length!=6||a[0]!=BirthIslandTriangleRules.RockActor||after.DeoxysRockEffect?.Kind!="move")
                        throw new InvalidDataException("Event triangle movement context differs.");
                    int[] p=a.Skip(1).Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();
                    EventIslandFieldPresentation.TrianglePosition(p[0],p[1],p[2],p[3],p[4],0);
                    _eventTriangleMove=(p[0],p[1],p[2],p[3],p[4]);_eventMoveFrames=0;
                    break;
                case "DeoxysRockDestroyResolved":
                    if(after.World.Player.MapId.Value!=EventBirthMap||after.DeoxysRockEffect?.Kind!="destroy"||
                        !a.SequenceEqual(new[]{"122","4","21","4","16","12","RGB_WHITE","SE_THUNDER2","SE_THUNDER"}))
                        throw new InvalidDataException("Event rock destruction context differs.");
                    _eventDestroyFrames=0;_eventThunderPlayed=false;SourceSound("SE_THUNDER2");
                    break;
                case "HoOhWingFlapResolved":
                    if(after.World.Player.MapId.Value!=EventSummitMap||!a.SequenceEqual(new[]{"3","35","SE_M_WING_ATTACK"}))
                        throw new InvalidDataException("Ho-Oh wing cue context differs.");
                    _eventWingFrames=0;_eventWingCount=1;SourceSound("SE_M_WING_ATTACK");
                    break;
                case "LugiaShakeResolved":
                    if(after.World.Player.MapId.Value!="map.navel-rock-base"||!a.SequenceEqual(new[]{"0","3","4","2"}))
                        throw new InvalidDataException("Lugia shake context differs.");
                    _eventLugiaFrames=0;SourceSound("SE_M_STRENGTH");
                    break;
                default:throw new InvalidDataException("Unhandled event field callback: "+effect.Opcode);
            }
            _eventFieldSequence=Math.Max(_eventFieldSequence,effect.Sequence);
        }
    }
    private void ProcessEventIslandFieldPresentation(double delta)
    {
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        if(_eventTriangleMove is not null)_eventMoveFrames+=delta*60;
        if(_eventDestroyFrames>=0)
        {
            _eventDestroyFrames+=delta*60;
            if(!_eventThunderPlayed&&_eventDestroyFrames+1e-7>=EventIslandFieldPresentation.FragmentFrame)
            {SourceSound("SE_THUNDER");_eventThunderPlayed=true;}
        }
        if(_eventLugiaFrames>=0){_eventLugiaFrames+=delta*60;if(_eventLugiaFrames>=8)_eventLugiaFrames=-1;}
        if(_eventWingFrames>=0)
        {
            _eventWingFrames+=delta*60;
            int count=EventIslandFieldPresentation.WingCount(EventFrame(_eventWingFrames));
            while(_eventWingCount<count){SourceSound("SE_M_WING_ATTACK");_eventWingCount++;}
            if(_eventWingCount==3)_eventWingFrames=-1;
        }
    }
    private bool DrawEventIslandActor(string id,SpriteAsset asset,Pose pose,Vector2 camera)
    {
        if(_session.State.World.Player.MapId.Value==EventSummitMap&&id=="LOCALID_NAVEL_ROCK_HO_OH"&&pose.Frame>0)
        {
            int frame=(pose.Frame-1)%2==0?3:4;
            DrawTextureRectRegion(asset.Texture,new(pose.Position.Floor()+new Vector2(8,8)-asset.Anchor-camera,new(asset.Width,asset.Height)),new(frame*asset.Width,0,asset.Width,asset.Height));
            return true;
        }
        if(_session.State.World.Player.MapId.Value!=EventBirthMap||id!=BirthIslandTriangleRules.RockActor)return false;
        if(_eventDestroyFrames>=EventIslandFieldPresentation.FragmentFrame)return true;
        int state=_session.BirthIslandTrianglePalette??throw new InvalidDataException("Triangle palette projection missing.");
        if(_session.State.Script.QuestLogState!=0)throw new InvalidDataException("Unsupported global field palette tint in event island.");
        var position=pose.Position;
        if(_eventTriangleMove is {} move)
        {
            var p=EventIslandFieldPresentation.TrianglePosition(move.FromX,move.FromY,move.ToX,move.ToY,move.Ticks,EventFrame(_eventMoveFrames));
            position=new(p.X,p.Y);
        }
        DrawTexture(_eventTriangleTextures[state],position.Floor()+new Vector2(8,8)-asset.Anchor-camera);return true;
    }
    private void DrawEventIslandFragments(Vector2 camera)
    {
        if(_eventDestroyFrames<EventIslandFieldPresentation.FragmentFrame||!_poses.TryGetValue(BirthIslandTriangleRules.RockActor,out var rock))return;
        for(int i=0;i<4;i++)
        {
            var offset=EventIslandFieldPresentation.FragmentOffset(i,EventFrame(_eventDestroyFrames));
            var center=rock.Position+new Vector2(8,-4)+new Vector2(offset.X,offset.Y)-camera;
            if(center.X>=-4&&center.X<=244&&center.Y>=-4&&center.Y<=164)DrawTexture(_eventRockFragments[i],center-new Vector2(4,4));
        }
    }
    private Texture2D EventIslandPaletteTexture(Texture2D original,bool background)
    {
        int blend=!background||_eventDestroyFrames<0?0:EventIslandFieldPresentation.BackgroundWhiteBlend(EventFrame(_eventDestroyFrames));
        if(blend==0)return original;
        if(_eventWhiteTextures.TryGetValue((original,blend),out var cached))return cached;
        using var image=original.GetImage();image.Convert(Image.Format.Rgba8);byte[] pixels=image.GetData();
        for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]!=0)
            for(int c=0;c<3;c++){int v=EventIslandFieldPresentation.BlendChannel(pixels[i+c]>>3,blend);pixels[i+c]=(byte)((v<<3)|(v>>2));}
        using var white=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
        var texture=ImageTexture.CreateFromImage(white);_eventWhiteTextures.Add((original,blend),texture);return texture;
    }
    private void ClearEventWhiteTextures()
    {foreach(var texture in _eventWhiteTextures.Values)texture.Dispose();_eventWhiteTextures.Clear();}
    private void ResetEventIslandFieldPresentation()
    {
        ClearEventWhiteTextures();_eventFieldSequence=-1;_eventTriangleMove=null;_eventMoveFrames=0;
        _eventDestroyFrames=-1;_eventLugiaFrames=-1;_eventWingFrames=-1;_eventWingCount=0;_eventThunderPlayed=false;
    }
}
