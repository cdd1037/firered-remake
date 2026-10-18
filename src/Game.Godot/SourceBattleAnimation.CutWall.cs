namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string TwisterLeafTemplate="gTwisterLeafSpriteTemplate",TwisterRockTemplate="gTwisterRockSpriteTemplate",BreakWallTemplate="gBrickBreakWallSpriteTemplate",BreakShardTemplate="gBrickBreakWallShardSpriteTemplate";
    private bool ValidateCutWallCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="createvisualtask"&&name is "AnimTask_IsFuryCutterHitRight" or "AnimTask_GetFuryCutterHitCount")
        {
            if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Fury Cutter predicate takes only source task priority.");
            if(_validatingInvokedProgram&&_cue.FuryCutterCounter is not (>=1 and <=5))Fail(c,"Fury Cutter requires the committed postincrement counter.");
            return true;
        }
        if(c.Op=="createvisualtask"&&name=="AnimTask_ShakeMonInPlace")
        {
            if(a.Length!=7||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >3||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||Number(a[5])<1||Number(a[6])<0)Fail(c,"In-place shake source selector or bounded signed counters differ.");
            return true;
        }
        if(c.Op!="createsprite"||name is not (TwisterLeafTemplate or TwisterRockTemplate or BreakWallTemplate or BreakShardTemplate))return false;
        if(a.Length!=(name==BreakShardTemplate?7:8)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Cut/wall sprite source ABI differs.");
        if(name is TwisterLeafTemplate or TwisterRockTemplate)
        {if(Number(a[3])<1||_script.SineTable.Length!=320)Fail(c,"Twister requires a bounded duration and source sine table.");}
        else if(Number(a[3]) is <0 or >1||name==BreakShardTemplate&&Number(a[4]) is <0 or >3||name==BreakWallTemplate&&(Number(a[6])<1||Number(a[7])<0))Fail(c,"Brick Break source participant, piece or timer differs.");
        ValidateCutWallRecipe(c,name);return true;
    }
    private void ValidateCutWallRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Cut/wall source recipe is missing.");r=_script.SpriteRecipes[name];
        var expected=name switch{TwisterLeafTemplate=>("leaf","AnimMoveTwisterParticle",16,false),TwisterRockTemplate=>("rocks","AnimMoveTwisterParticle",32,false),BreakWallTemplate=>("blue-light-wall","AnimBrickBreakWall",64,true),_=>("torn-metal","AnimBrickBreakWallShard",32,false)};
        if(r.Asset!=expected.Item1||r.Callback!=expected.Item2||r.Width!=expected.Item3||r.Height!=expected.Item3||r.Blend!=expected.Item4||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.FrameLoops.Any(x=>x.Length!=0))Fail(c,"Cut/wall template shape or callback differs.");
        SourceSpriteAnimFrame[][] frames=name switch
        {
            TwisterLeafTemplate=>[new[]{0,4,8,12,16,20,16,12,8,4}.Select(t=>new SourceSpriteAnimFrame(t,5,false,false)).ToArray(),new[]{24,28,32}.Select(t=>new SourceSpriteAnimFrame(t,5,false,false)).ToArray()],
            TwisterRockTemplate=>[[new(64,1,false,false)],[new(80,1,false,false)]],
            _=>[[new(0,1,false,false)]]
        };
        int[] loops=name switch{TwisterLeafTemplate=>[0,-1],TwisterRockTemplate=>[-1,-1],_=>[0]};
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(loops))Fail(c,"Cut/wall source frame table shape differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Cut/wall source frame program differs.");
    }
    private bool TryCreateCutWallTask(SourceAnimationCommand c)
    {
        switch(c.Args[0])
        {
            case "AnimTask_IsFuryCutterHitRight":_animationArgs[7]=(short)(_cue.FuryCutterCounter!.Value&1);return true;
            case "AnimTask_GetFuryCutterHitCount":_animationArgs[7]=_cue.FuryCutterCounter!.Value;return true;
            default:return false;
        }
    }
    // Source GetAnimBattlerSpriteId checks presence for attacker/target, but
    // visibility for partner selectors. Hidden-present actors still move.
    private AnimationSlot? ResolveInPlaceShakeParticipant(int selector)
    {
        int slot=selector switch{0=>_cue.Actor.Value,1=>_cue.Target.Value,2=>_cue.Actor.Value^2,3=>_cue.Target.Value^2,_=>throw new InvalidDataException("Unknown in-place shake selector.")};
        return _battlers.ContainsKey(slot)&&(selector<2||IsSourceBattlerVisible(slot))?new(slot):null;
    }
    private bool TryCreateCutWallSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (TwisterLeafTemplate or TwisterRockTemplate or BreakWallTemplate or BreakShardTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Cut/wall sprite graphics have not been loaded.");
        _sprites.Add(new CutWallSprite(slot,priority,r,Number(c.Args[1]),this));return true;
    }
    private sealed class CutWallSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly short[] _data=new short[8];
        private PixelPoint _position,_offset;private int _tileOffset,_oamPriority=2;
        public CutWallSprite(int slot,int priority,SourceSpriteRecipe r,int participant,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r,0);var a=o._animationArgs;_position=o._battlers[(participant==0?o._cue.Actor:o._cue.Target).Value].Center(o._doubles,true);
            if(r.Template is TwisterLeafTemplate or TwisterRockTemplate)
            {
                if(o._doubles)_position=o.AverageElementalTarget(true);_position=new(_position.X,unchecked((short)(_position.Y+32)));Array.Copy(a,_data,5);
            }
            else
            {
                var b=o._battlers[(a[0]==0?o._cue.Actor:o._cue.Target).Value].Base(o._doubles);
                if(r.Template==BreakWallTemplate){_position=new(unchecked((short)(b.X+a[1])),unchecked((short)(b.Y+a[2])));_data[1]=a[3];_data[2]=a[4];}
                else{_position=new(unchecked((short)(b.X+a[2])),unchecked((short)(b.Y+a[3])));_tileOffset=a[1]*16;_data[6]=(short)((a[1]&1)==0?-3:3);_data[7]=(short)(a[1]<2?-3:3);}
            }
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile+_tileOffset,true,_frames.Frame.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_frames.Frame.VFlip,OamPriority=_oamPriority};
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override void Step(SourceBattleAnimation o)
        {
            var d=_data;
            if(_recipe.Template is TwisterLeafTemplate or TwisterRockTemplate)
            {
                if(d[1]==255||d[1]>0){_position=new(_position.X,unchecked((short)(_position.Y-2)));if(d[1]!=255)d[1]-=2;}
                d[5]+=d[2];if(d[0]<d[4])d[5]+=d[2];d[5]&=255;
                _offset=new(unchecked((short)((o._script.SineTable[d[5]+64]*d[3])>>8)),unchecked((short)((o._script.SineTable[d[5]]*5)>>8)));
                _oamPriority=(o.BattlerBackgroundPriority(o._cue.Target.Value)+(d[5]<128?-1:1))&3;if(--d[0]==0)Finished=true;
            }
            else if(_recipe.Template==BreakWallTemplate)
            {
                if(d[0]==0){if(--d[1]==0){if(d[2]==0)Finished=true;else d[0]++;}}
                else{if(++d[1]>1){d[1]=0;d[3]++;_offset=new((d[3]&1)!=0?2:-2,0);}if(--d[2]==0)Finished=true;}
            }
            else{_position=new(unchecked((short)(_position.X+d[6])),unchecked((short)(_position.Y+d[7])));if(++d[0]>40)Finished=true;}
        }
    }
}
