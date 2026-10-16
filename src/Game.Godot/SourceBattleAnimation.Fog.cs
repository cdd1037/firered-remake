namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private bool ValidateFogCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!="InitSwirlingFogAnim")return false;
        if(a.Length!=9||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||Number(a[6]) is <1 or >32767||Number(a[7]) is <0 or >1||Number(a[8]) is <0 or >1||recipe.Width!=32||recipe.Height!=16||!recipe.Blend||recipe.AffineAnimations.Length!=0||_script.SineTable.Length!=320)Fail(command,"Swirling fog source recipe or operands differ.");
        return true;
    }
    private bool TryCreateFogSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(command.Args[0],out var recipe)||recipe.Callback!="InitSwirlingFogAnim")return false;
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Fog graphics are not loaded.");
        _sprites.Add(new SwirlingFog(slot,priority,recipe,command.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private sealed class SwirlingFog:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PhysicalLinear _linear;private readonly PixelPoint _position;private readonly int _participant,_radius;
        private PixelPoint _offset;private int _angle=64,_priority=2;
        public SwirlingFog(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_participant=(args[4]==0?owner._cue.Actor:owner._cue.Target).Value;
            var selected=owner._battlers[_participant];var p=selected.Base(owner._doubles);
            if(args[5]!=0&&owner._doubles){var partner=owner._battlers[_participant^2].Base(true);p=new((p.X+partner.X)/2,(p.Y+partner.Y)/2);}
            int direction=args[5]==0?owner.InitialXDirection():selected.Slot.IsPlayer?1:-1;
            _position=new(unchecked((short)(p.X+direction*args[0])),unchecked((short)(p.Y+args[1]+(owner._cue.Target.IsPlayer?8:0))));
            _radius=args[5]==0||!owner._doubles?32:64;_linear=new(0,args[2],args[3]);Translate(owner);
        }
        private void Translate(SourceBattleAnimation owner)
        {
            if(_linear.Step(out var linear)){Finished=true;return;}
            _offset=new(unchecked((short)(linear.X+(owner._script.SineTable[_angle]*_radius>>8))),unchecked((short)(linear.Y+(owner._script.SineTable[_angle+64]*-6>>8))));
            int priority=owner.BattlerBackgroundPriority(_participant);_priority=(priority+((ushort)(_angle-64)<=127?0:1))&3;_angle=(_angle+3)&255;
        }
        public override void Step(SourceBattleAnimation owner){Translate(owner);if(!Finished)_frames.Step();}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!Finished,_frames.Frame.HFlip,true,256,_recipe.Asset,Subpriority){Width=32,Height=16,VFlip=_frames.Frame.VFlip,OamPriority=_priority};
    }
}
