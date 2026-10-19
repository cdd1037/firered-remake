using Game.Application;
using Game.Domain.World;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private readonly List<CampaignFieldJumpCue> _fieldJumpCues=[];
    private readonly Texture2D[] _fieldJumpShadows=new Texture2D[4];

    private void LoadFieldJumpEffects()
    {
        string[] names=["s","m","l","xl"];
        for(int i=0;i<names.Length;i++)
            _fieldJumpShadows[i]=Texture(System.IO.Path.Combine(_root,"content/generated/sprites/overworld/field-shadow-"+names[i]+".png"));
    }

    private void SynchronizeFieldJumpEffects(CampaignState? before,CampaignState after,bool normalStep)
    {
        if(before is null||before.World.Player.MapId!=after.World.Player.MapId)_fieldJumpCues.Clear();
        if(before is not null&&!normalStep&&before.Motions.SequenceEqual(after.Motions))return;
        foreach(var track in _tracks)
        {
            _fieldJumpCues.RemoveAll(c=>c.ActorId==track.Actor&&c.ElapsedFrames<c.Duration);
            double start=0;
            foreach(var clip in track.Clips)
            {
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

    private void AdvanceFieldJumpEffects(double seconds)
    {
        for(int i=0;i<_fieldJumpCues.Count;i++)_fieldJumpCues[i]=_fieldJumpCues[i] with{ElapsedFrames=_fieldJumpCues[i].ElapsedFrames+seconds*60};
        _fieldJumpCues.RemoveAll(c=>c.ElapsedFrames>=c.Duration+CampaignFieldJumpEffects.DustLifetimeFrames);
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
}
