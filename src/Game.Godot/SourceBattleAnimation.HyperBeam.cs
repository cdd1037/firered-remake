namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateHyperBeamOperand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createsprite"||a.FirstOrDefault()!="gHyperBeamOrbSpriteTemplate")return false;
        if(a.Length!=3||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe))Fail(command,"Hyper Beam recipe or operands differ.");
        recipe=_script.SpriteRecipes[a[0]];
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||recipe.Callback!="AnimHyperBeamOrb"||recipe.Asset!="orbs"||recipe.Width!=8||recipe.Height!=8||recipe.Blend||recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=0||recipe.Animations.Length!=7||!recipe.LoopStarts.SequenceEqual(Enumerable.Repeat(-1,7))||recipe.FrameLoops.Any(p=>p.Length!=0)||_script.SineTable.Length!=320)Fail(command,"Hyper Beam source shape or seven-entry frame table differs.");
        for(int i=0;i<7;i++)if(recipe.Animations[i].Length!=1||recipe.Animations[i][0]!=new SourceSpriteAnimFrame(i,1,false,false))Fail(command,"Hyper Beam source frame entry differs.");
        RequireVisualSeed(command);return true;
    }
    private bool TryCreateHyperBeamSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0]!="gHyperBeamOrbSpriteTemplate")return false;
        if(!_loaded.Contains("ANIM_TAG_ORBS"))Fail(command,"Hyper Beam orb graphics have not been loaded.");
        _sprites.Add(new HyperBeamOrb(slot,priority,_script.SpriteRecipes[command.Args[0]],this));return true;
    }
    private sealed class HyperBeamOrb:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _position;private PixelPoint _offset;private readonly SourceFastLinear _linear;
        private int _angle,_renderPriority;
        public HyperBeamOrb(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;
            // Approved safety difference: source Random()%8 can address a missing
            // eighth entry. Draw uniformly from the seven actual frame programs.
            _frames=new(recipe,owner.NextVisualChoice(recipe.Animations.Length));
            var from=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);var to=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _position=new(unchecked((short)(from.X+(owner._cue.Actor.IsPlayer?20:-20))),from.Y);
            _linear=new(to.X-_position.X,to.Y-_position.Y,(owner.NextVisualRandom()&31)+64);
            _angle=owner.NextVisualRandom()&255;_renderPriority=priority;
            Step(owner); // Source initializer invokes its movement callback immediately.
        }
        public override SourceVisualSpriteFrame Frame
        {
            get{var f=_frames.Frame;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,_renderPriority){VFlip=f.VFlip,Width=8,Height=8};}
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override void Step(SourceBattleAnimation owner)
        {
            if(_linear.Step(out var p)){Finished=true;return;}
            _offset=new(p.X,unchecked((short)(p.Y+((owner._script.SineTable[_angle+64]*12)>>8))));
            _renderPriority=unchecked((byte)(Subpriority+(_angle<127?0:1)));_angle=(_angle+24)&255;
        }
    }
}
