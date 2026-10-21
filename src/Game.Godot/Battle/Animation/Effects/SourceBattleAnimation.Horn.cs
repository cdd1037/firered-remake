namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private bool ValidateHornCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!="AnimHornHit")return false;
        if(a.Length!=6||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||recipe.Width!=32||recipe.Height!=32||recipe.Blend||recipe.AffineAnimations.Length!=0)Fail(command,"Horn hit source recipe or operands differ.");
        return true;
    }
    private bool TryCreateHornSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(command.Args[0],out var recipe)||recipe.Callback!="AnimHornHit")return false;
        if(!_loaded.Contains("ANIM_TAG_HORN_HIT"))Fail(command,"Horn graphics are not loaded.");
        _sprites.Add(new HornHit(slot,priority,recipe,this));return true;
    }
    private sealed class HornHit:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _destination;private readonly bool _flipped;
        private short _x,_y;private readonly int _vx,_vy;private int _remaining;private PixelPoint _position;
        public HornHit(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_remaining=Math.Clamp((int)owner._animationArgs[2],2,127);owner._animationArgs[2]=(short)_remaining;
            var center=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _destination=new(unchecked((short)(center.X+owner._animationArgs[0])),unchecked((short)(center.Y+owner._animationArgs[1])));
            _flipped=!owner._cue.Actor.IsPlayer;int direction=_flipped?-1:1;
            _position=new(unchecked((short)(_destination.X-40*direction)),unchecked((short)(_destination.Y+20*direction)));
            _x=unchecked((short)(_position.X<<7));_y=unchecked((short)(_position.Y<<7));_vx=direction*0x1400/_remaining;_vy=-direction*0xA00/_remaining;
        }
        public override void Step(SourceBattleAnimation owner)
        {
            _x=unchecked((short)(_x+_vx));_y=unchecked((short)(_y+_vy));_position=new(_x>>7,_y>>7);
            if(--_remaining==1)_position=_destination;
            if(_remaining==0){Finished=true;return;}_frames.Step();
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,default,_frames.Frame.Tile,!Finished,_flipped,false,256,_recipe.Asset,Subpriority){Width=32,Height=32,VFlip=_flipped};
    }
}
