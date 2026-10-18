namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string OverheatFlameTemplate="gOverheatFlameSpriteTemplate";
    private bool ValidateOverheatOperand(SourceAnimationCommand c)
    {
        var a=c.Args;if(c.Op!="createsprite"||a.FirstOrDefault()!=OverheatFlameTemplate)return false;
        if(a.Length!=8||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||Number(a[4]) is <0 or >255||Number(a[6]) is <0 or >32766)Fail(c,"Overheat flame operands exceed source widths or bounded lifetime.");
        if(!_script.SpriteRecipes.TryGetValue(OverheatFlameTemplate,out var r))Fail(c,"Overheat flame recipe is missing.");
        r=_script.SpriteRecipes[OverheatFlameTemplate];
        if(r.Callback!="AnimOverheatFlame"||r.Asset!="small-ember"||r.Width!=32||r.Height!=32||r.Blend||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.FrameLoops.Any(p=>p.Length!=0)||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(Enumerable.Range(0,5).Select(i=>new SourceSpriteAnimFrame(i*16,4,false,false)))||!r.LoopStarts.SequenceEqual([0])||_script.SineTable.Length!=320)Fail(c,"Overheat flame source frame or sine program differs.");
        RequireOverlaySpritePalette(c,"ANIM_TAG_SMALL_EMBER");return true;
    }
    private bool TryCreateOverheatSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=OverheatFlameTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_SMALL_EMBER"))Fail(c,"Overheat flame graphics have not been loaded.");
        _sprites.Add(new OverheatFlameSprite(slot,priority,_script.SpriteRecipes[OverheatFlameTemplate],_animationArgs.Take(5).Select(v=>(int)v).ToArray(),this));return true;
    }
    private sealed class OverheatFlameSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _position;private PixelPoint _offset;private readonly short _vx,_vy,_duration;private short _ticks,_x,_y;
        public OverheatFlameSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var p=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles);
            _vx=unchecked((short)((owner._script.SineTable[a[1]+64]*a[2])>>8));_vy=unchecked((short)((owner._script.SineTable[a[1]]*((a[2]*3)/5))>>8));_duration=(short)a[3];
            _position=new(unchecked((short)(p.X+_vx*a[0])),unchecked((short)(p.Y+a[4]+_vy*a[0])));
        }
        public override SourceVisualSpriteFrame Frame{get{var f=_frames.Frame;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=32,Height=32,VFlip=f.VFlip};}}
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override void Step(SourceBattleAnimation owner)
        {
            _x=unchecked((short)(_x+_vx));_y=unchecked((short)(_y+_vy));_offset=new(_x/10,_y/10);
            _ticks=unchecked((short)(_ticks+1));if(_ticks>_duration)Finished=true;
        }
    }
}
