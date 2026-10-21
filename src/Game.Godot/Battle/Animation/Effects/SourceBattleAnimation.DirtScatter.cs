namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateDirtScatterOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault() is not ("gSandAttackDirtSpriteTemplate" or "gMudSlapMudSpriteTemplate"))return false;
        if(a.Length!=8||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(command,"Missing dirt-scatter recipe or operands.");
        var recipe=_script.SpriteRecipes[a[0]];bool mud=a[0]=="gMudSlapMudSpriteTemplate";
        if(recipe.Callback!="AnimDirtScatter"||recipe.Asset!="mud-sand"||recipe.Width!=(mud?16:8)||recipe.Height!=recipe.Width||recipe.Blend||recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=0||
            recipe.Animations.Length!=1||recipe.Animations[0].Length!=1||recipe.Animations[0][0]!=new SourceSpriteAnimFrame(mud?1:0,1,false,false)||
            recipe.LoopStarts.Length!=1||recipe.LoopStarts[0]!=(mud?-1:0)||recipe.FrameLoops.Any(x=>x.Length!=0))Fail(command,"Dirt-scatter source template differs.");
        for(int i=1;i<a.Length;i++)
            if(!int.TryParse(a[i] switch{"ANIM_ATTACKER"=>"0","ANIM_TARGET"=>"1",_=>a[i]},out int value)||value is < -32768 or >32767)Fail(command,"Dirt scatter requires signed source operands.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[3]) is < -128 or >127||Number(a[4]) is < -128 or >127||Number(a[5]) is <1 or >255)Fail(command,"Dirt-scatter coordinates or duration differ.");
        RequireVisualSeed(command);return true;
    }
    private bool TryCreateDirtScatterSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not ("gSandAttackDirtSpriteTemplate" or "gMudSlapMudSpriteTemplate"))return false;
        if(!_loaded.Contains("ANIM_TAG_MUD_SAND"))Fail(command,"Dirt-scatter graphics have not been loaded.");
        _sprites.Add(new DirtScatter(slot,priority,_script.SpriteRecipes[command.Args[0]],this));return true;
    }
    private sealed class DirtScatter:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;
        private readonly PhysicalLinear _linear;private PixelPoint _offset;private bool _destroyNext;
        public DirtScatter(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var args=owner._animationArgs;
            var actor=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _position=new(unchecked((short)(actor.X+owner.InitialXDirection()*args[0])),unchecked((short)(actor.Y+args[1])));
            // Two source primary-stream draws, X then Y, happen at creation even
            // if the particle is clipped. Source args[3]/[4] are intentionally unused.
            int x=owner.NextVisualRandom()&31,y=owner.NextVisualRandom()&31;
            if(x>16)x=16-x;if(y>16)y=16-y;
            _linear=new(unchecked((byte)target.X)+x-_position.X,unchecked((byte)target.Y)+y-_position.Y,args[2]);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,false,false,256,_recipe.Asset,Subpriority)
            {Width=_recipe.Width,Height=_recipe.Height};
        public override void Step(SourceBattleAnimation owner)
        {
            if(_destroyNext){Finished=true;return;}
            // StartAnimLinearTranslation performs the first step on this callback.
            // Completion only installs DestroySpriteAndMatrix; destruction is next tick.
            if(_linear.Step(out var offset))_destroyNext=true;else _offset=offset;
            _frames.Step();
        }
    }
}
