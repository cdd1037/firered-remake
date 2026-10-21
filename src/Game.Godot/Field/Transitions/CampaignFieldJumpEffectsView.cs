using Game.Application;
using Game.Domain.World;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private readonly List<CampaignFieldJumpCue> _fieldJumpCues=[];
    private readonly CampaignFieldGrassLandingEffects _fieldGrass=new();
    private double _fieldGrassFrame;
    private Texture2D _fieldGrassCoverTexture=null!,_fieldGrassImpactTexture=null!;
    private readonly Texture2D[] _fieldJumpShadows=new Texture2D[4];

    private void LoadFieldJumpEffects()
    {
        _fieldGrassCoverTexture=Texture(System.IO.Path.Combine(_root,"content/generated/sprites/overworld/field-tall-grass.png"));
        _fieldGrassImpactTexture=Texture(System.IO.Path.Combine(_root,"content/generated/sprites/overworld/field-jump-tall-grass.png"));
        string[] names=["s","m","l","xl"];
        for(int i=0;i<names.Length;i++)
            _fieldJumpShadows[i]=Texture(System.IO.Path.Combine(_root,"content/generated/sprites/overworld/field-shadow-"+names[i]+".png"));
    }

    private void SynchronizeFieldJumpEffects(CampaignState? before,CampaignState after,bool normalStep)
    {
        if(before is null||before.World.Player.MapId!=after.World.Player.MapId||_fieldWarpSynchronizing)
        { _fieldJumpCues.Clear();ReconstructFieldGrass(after); }
        else foreach(var actor in after.Actors.Values.Where(a=>a.Active&&(a.LocalId!="LOCALID_CAMERA"||a.Visible)))
            if(!before.Actors.TryGetValue(actor.LocalId,out var old)||!old.Active)
            {
                var origin=_poses.TryGetValue(actor.LocalId,out var pose)?pose.Position:new Vector2(actor.X*16,actor.Y*16);
                _fieldGrass.QueueSpawn(actor.LocalId,(int)origin.X/16,(int)origin.Y/16,_fieldGrassFrame);
            }
        if(before is not null&&!normalStep&&before.Motions.SequenceEqual(after.Motions))return;
        foreach(var track in _tracks)
        {
            _fieldJumpCues.RemoveAll(c=>c.ActorId==track.Actor&&c.ElapsedFrames<c.Duration);
            double start=0;
            foreach(var clip in track.Clips)
            {
                _fieldGrass.QueueMovement(track.Actor,(int)clip.From.X/16,(int)clip.From.Y/16,(int)clip.To.X/16,(int)clip.To.Y/16,
                    clip.Action,(int)Math.Round(clip.Duration*60),_fieldGrassFrame+start*60);
                if(CampaignFieldJumpEffects.IsOrdinaryJump(clip.Action))
                {
                    _fieldJumpCues.Add(new(track.Actor,after.World.Player.MapId.Value,
                        (int)clip.From.X/16,(int)clip.From.Y/16,(int)clip.To.X/16,(int)clip.To.Y/16,
                        clip.Action,(int)Math.Round(clip.Duration*60),-start*60));
                }
                start+=clip.Duration;
            }
        }
    }

    private void SeedFieldGrass(CampaignFieldGrassLandingEffects grass,CampaignState state,bool presented=false)
    {
        void Spawn(string id,int x,int y)
        {
            if(presented&&_poses.TryGetValue(id,out var pose)){x=(int)pose.Position.X/16;y=(int)pose.Position.Y/16;}
            grass.QueueSpawn(id,x,y,0);
        }
        Spawn(CampaignQuestLogRecorder.PlayerId,state.World.Player.X,state.World.Player.Y);
        foreach(var actor in state.Actors.Values.Where(a=>a.Active&&(a.LocalId!="LOCALID_CAMERA"||a.Visible)))
            Spawn(actor.LocalId,actor.X,actor.Y);
    }

    private void ReconstructFieldGrass(CampaignState state)
    {
        _fieldGrass.Clear();_fieldGrassFrame=0;SeedFieldGrass(_fieldGrass,state,true);
    }

    private IReadOnlyList<CampaignFieldGrassLandingCover> FreshFieldGrass(CampaignState state)
    {
        var grass=new CampaignFieldGrassLandingEffects();SeedFieldGrass(grass,state);
        grass.AdvanceTo(1,(x,y)=>JumpBehavior(state.World.Player.MapId,state.MapLayout,state.MapEdits?.Values??[],x,y),_=>true);
        return grass.Covers;
    }

    private void AdvanceFieldJumpEffects(double seconds)
    {
        if(_fieldDisplayReturn.Suspended)return;
        _fieldGrassFrame+=seconds*60;
        _fieldGrass.AdvanceTo(_fieldGrassFrame,(x,y)=>JumpBehavior(_session.State.World.Player.MapId,_session.State.MapLayout,_session.State.MapEdits?.Values??[],x,y),
            id=>id==CampaignQuestLogRecorder.PlayerId||_session.State.Actors.TryGetValue(id,out var actor)&&actor.Active);
        for(int i=0;i<_fieldJumpCues.Count;i++)_fieldJumpCues[i]=_fieldJumpCues[i] with{ElapsedFrames=_fieldJumpCues[i].ElapsedFrames+seconds*60};
        _fieldJumpCues.RemoveAll(c=>c.ElapsedFrames>=c.Duration+CampaignFieldJumpEffects.LandingLifetimeFrames);
    }

    private void DrawLiveFieldJumpEffects(Vector2 camera)
    {
        SpriteAsset? Art(string id)=>id==CampaignQuestLogRecorder.PlayerId?CurrentPlayerSprite:
            _session.State.Actors.TryGetValue(id,out var actor)?_art.Npcs.GetValueOrDefault(ResolveRegionGraphics(actor.GraphicsId)):null;
        DrawJumpEffects(_fieldJumpCues,_poses,camera,(x,y)=>JumpBehavior(_session.State.World.Player.MapId,_session.State.MapLayout,_session.State.MapEdits?.Values??[],x,y),Art,texture=>texture);
    }

    private void DrawFieldJumpEffects(IReadOnlyList<CampaignFieldJumpCue> cues,CampaignQuestLogMapProjection projection,
        IReadOnlyDictionary<string,Pose> poses,Vector2 camera,MapArt art,Func<Texture2D,Texture2D> palette)
    {
        var map=new MapId(projection.Map);
        int? Behavior(int x,int y)=>JumpBehavior(map,projection.Layout,projection.Tiles,x,y);
        SpriteAsset? Art(string id)
        {
            var actor=projection.Actors.FirstOrDefault(a=>a.Id==id);
            if(actor is null)return null;
            if(id!=CampaignQuestLogRecorder.PlayerId)return art.Npcs.GetValueOrDefault(actor.Graphics);
            int gender=CampaignQuestLogAvatar.Gender(actor.Graphics,_session.State.PlayerGender);
            return CampaignQuestLogAvatar.IsBike(actor.Graphics)?_cityCycleSprites.GetValueOrDefault(gender):gender==1?_femalePlayer:art.Player;
        }
        DrawJumpEffects(cues,poses,camera,Behavior,Art,palette);
    }

    private int? JumpBehavior(MapId map,string? layout,IEnumerable<FieldMetatileEdit> tiles,int x,int y)
        =>tiles.FirstOrDefault(t=>t.X==x&&t.Y==y) is {} tile
            ?_catalog.MetatileDefinition(map,tile.SourceMetatileId)?.Behavior
            :(layout is {} selected?_catalog.MapLayout(map,selected):null)?.SourceBehaviorAt(x,y)??_catalog.Maps[map].SourceBehaviorAt(x,y);

    private void DrawJumpEffects(IEnumerable<CampaignFieldJumpCue> cues,IReadOnlyDictionary<string,Pose> poses,
        Vector2 camera,Func<int,int,int?> behavior,Func<string,SpriteAsset?> art,Func<Texture2D,Texture2D> palette)
    {
        foreach(var cue in cues)
        {
            if(cue.ElapsedFrames<0)continue;
            if(CampaignFieldJumpEffects.ShadowVisible(cue,behavior)&&poses.TryGetValue(cue.ActorId,out var pose)&&art(cue.ActorId) is {} asset)
            {
                var texture=palette(_fieldJumpShadows[asset.ShadowSize]);
                var geometry=CampaignFieldJumpEffects.ShadowGeometry(asset.ShadowSize);
                var center=new Vector2(geometry.CenterX,geometry.CenterY);
                DrawTexture(texture,pose.Position+center-texture.GetSize()/2-camera);
            }
            int frame=CampaignFieldJumpEffects.DustFrame(cue,behavior(cue.ToX,cue.ToY));
            if(frame>=0)
                DrawTextureRectRegion(palette(_fieldStrengthDust!),new(new Vector2(cue.ToX*16,cue.ToY*16+8)-camera,new(16,8)),new(0,frame*8,16,8));
        }
    }
    private void DrawFieldActorsWithGrass((string Id,SpriteAsset Asset,Pose Pose)[] actors,
        IReadOnlyList<CampaignFieldGrassLandingCover> covers,IEnumerable<CampaignFieldJumpCue> jumps,Vector2 camera,
        Func<int,int,int> elevation,Func<int,int,int?> behavior,Func<Texture2D,Texture2D> palette,
        Action<string,SpriteAsset,Pose> drawActor)
    {
        if(covers.Count==0&&!jumps.Any(j=>CampaignFieldJumpEffects.GrassImpactFrame(j,behavior(j.ToX,j.ToY))>=0))
        {
            foreach(var actor in actors.OrderBy(a=>a.Pose.Position.Y).ThenBy(a=>a.Id,StringComparer.Ordinal))drawActor(actor.Id,actor.Asset,actor.Pose);
            return;
        }
        CampaignFieldGrassActorGeometry Geometry((string Id,SpriteAsset Asset,Pose Pose) actor)
        {
            var center=actor.Pose.Position.Floor()+new Vector2(8,8)-actor.Asset.Anchor+new Vector2(actor.Asset.Width/2,actor.Asset.Height/2);
            int z=elevation((int)actor.Pose.Position.X/16,(int)actor.Pose.Position.Y/16);
            return new((int)center.X,(int)center.Y,actor.Asset.Height,
                CampaignFieldGrassLandingEffects.Subpriority(z,(int)center.Y,actor.Asset.Height,-(int)camera.Y,1));
        }
        var geometry=actors.Select(Geometry).ToArray();
        var paints=new List<(int X,int Y,int Height,int Frame,int Priority,bool Cover)>();
        foreach(var cover in covers)
        {
            int x=cover.X*16+8,y=cover.Y*16+8;
            int priority=CampaignFieldGrassLandingEffects.Subpriority(elevation(cover.X,cover.Y),y,16,-(int)camera.Y,cover.DepthOffset);
            priority=CampaignFieldGrassLandingEffects.AdjustCoverSubpriority(x,y,priority,geometry);
            paints.Add((x,y,16,cover.Frame,priority,true));
        }
        foreach(var jump in jumps)
        {
            int frame=CampaignFieldJumpEffects.GrassImpactFrame(jump,behavior(jump.ToX,jump.ToY));
            if(frame<0)continue;
            int x=jump.ToX*16+8,y=jump.ToY*16+12;
            paints.Add((x,y,8,frame,CampaignFieldGrassLandingEffects.Subpriority(elevation(jump.ToX,jump.ToY),y,8,-(int)camera.Y,0),false));
        }
        // Actor order is unchanged. Grass inserts using the source subpriority and
        // top-Y tie break; the cover alone may move behind a southward actor.
        paints=paints.OrderByDescending(p=>p.Priority).ThenBy(p=>p.Y-p.Height/2).ToList();
        void Paint((int X,int Y,int Height,int Frame,int Priority,bool Cover) p)=>DrawTextureRectRegion(
            palette(p.Cover?_fieldGrassCoverTexture:_fieldGrassImpactTexture),
            new(new Vector2(p.X-8,p.Y-p.Height/2)-camera,new(16,p.Height)),new(0,p.Frame*p.Height,16,p.Height));
        foreach(var actor in actors.OrderBy(a=>a.Pose.Position.Y).ThenBy(a=>a.Id,StringComparer.Ordinal))
        {
            var shape=Geometry(actor);
            int actorPriority=CampaignFieldGrassLandingEffects.ObjectPriority(elevation((int)actor.Pose.Position.X/16,(int)actor.Pose.Position.Y/16));
            for(int j=0;j<paints.Count;)
            {
                var p=paints[j];
                if(actorPriority<2||actorPriority==2&&(p.Priority>shape.Subpriority||p.Priority==shape.Subpriority&&p.Y-p.Height/2<=shape.CenterY-shape.Height/2))
                {Paint(p);paints.RemoveAt(j);}else j++;
            }
            drawActor(actor.Id,actor.Asset,actor.Pose);
        }
        foreach(var p in paints)Paint(p);
    }

}
