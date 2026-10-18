namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateWallCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback is not ("AnimDefensiveWall" or "AnimWallSparkle"))return false;
        if(a.Length!=(recipe.Callback=="AnimDefensiveWall"?6:7)||recipe.Animations.Length!=1||recipe.AffineAnimations.Length!=0)Fail(command,"Wall callback recipe/operand shape differs.");
        if(recipe.Callback=="AnimDefensiveWall")
        {
            if(!recipe.Blend||recipe.Width!=64||recipe.Height!=64||a[5]!="ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')||!_script.SpriteTags.ContainsKey(a[5]))Fail(command,"Wall palette tag or geometry differs.");
        }
        else if(AnimationArgument(a[5]) is <0 or >1||AnimationArgument(a[6]) is <0 or >1)Fail(command,"Wall sparkle coordinate mode differs.");
        return true;
    }
    private bool TryCreateWallSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(command.Args[0],out var recipe)||recipe.Callback is not ("AnimDefensiveWall" or "AnimWallSparkle"))return false;
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Wall sprite graphics are not loaded.");
        var args=command.Args.Skip(3).Select(AnimationArgument).ToArray();
        _sprites.Add(new WallSprite(slot,priority,recipe,args,this));return true;
    }
    private sealed class WallSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;private readonly int _paletteTag;
        private int _state,_alpha,_delay,_rotations;private bool _hidden;
        public WallSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,recipe.Callback=="AnimDefensiveWall"&&owner._cue.Actor.IsPlayer?200:priority)
        {
            _recipe=recipe;_frames=new(recipe);var actor=owner._battlers[owner._cue.Actor.Value];bool wall=recipe.Callback=="AnimDefensiveWall";
            if(wall&&!owner._doubles&&!actor.Slot.IsPlayer)owner._animationArgs[0]=(short)-args[0];
            if(owner._doubles)_position=actor.Slot.IsPlayer?new(72+(wall?0:-args[0]),80+(wall?0:args[1])):new(176+(wall?0:args[0]),40+(wall?0:args[1]));
            else
            {
                var participant=wall||args[2]==0?actor:owner._battlers[owner._cue.Target.Value];var center=wall||args[3]!=0?participant.Base(false):participant.Center(false,true);
                int direction=wall?(actor.Slot.IsPlayer?1:-1):owner.InitialXDirection();_position=new(center.X+direction*args[0],center.Y+args[1]);
            }
            if(wall)
            {
                _paletteTag=args[2];
                for(int battler=1;battler<owner._battlerCount;battler+=2)if(owner.IsSourceBattlerVisible(battler))owner.MoveBattlerToBackground(battler,false);
                WriteAlpha(owner); // Initial callback invokes Step2 immediately.
            }
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,default,_frames.Frame.Tile,!_hidden,_frames.Frame.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_frames.Frame.VFlip,OamPriority=2};
        private void WriteAlpha(SourceBattleAnimation owner)
        {owner._alphaA=_alpha;owner._alphaB=16-_alpha;if(_alpha==13)_state=1;else _alpha++;}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_recipe.Callback=="AnimWallSparkle")
            {if(_frames.Ended){Finished=true;return;}_frames.Step();return;}
            switch(_state)
            {
                case 0:WriteAlpha(owner);break;
                case 1:
                    if(++_delay==2){_delay=0;owner.RotateSpritePalette(_paletteTag,1,8);if(++_rotations==16)_state=2;}break;
                case 2:
                    owner._alphaA=_alpha;owner._alphaB=16-_alpha;
                    if(--_alpha==-1)
                    {
                        for(int battler=1;battler<owner._battlerCount;battler+=2)if(owner.IsSourceBattlerVisible(battler))owner.RestoreBattlerObject(battler);
                        _hidden=true;_state=3;
                    }
                    break;
                case 3:owner.ClearBattlerBackgrounds();_state=4;break;
                default:Finished=true;return;
            }
            _frames.Step();
        }
    }
}
