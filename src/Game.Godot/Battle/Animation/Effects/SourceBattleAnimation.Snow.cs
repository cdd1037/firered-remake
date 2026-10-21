namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private bool ValidateSnowCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var found)||found.Callback is not ("AnimSwirlingSnowball" or "AnimWaveFromCenterOfTarget"))return false;
        var recipe=_script.SpriteRecipes[a[0]];bool swirling=recipe.Callback=="AnimSwirlingSnowball";
        if(a.Length!=(swirling?9:6)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>AnimationArgument(v) is <short.MinValue or >short.MaxValue)||recipe.Width!=8||recipe.Height!=(swirling?8:16)||recipe.Blend==swirling||recipe.AffineAnimations.Length!=0)Fail(command,"Source snow recipe or operands differ.");
        if(swirling&&(Number(a[7]) is <1 or >32767||AnimationArgument(a[8]) is <0 or >1||_script.SineTable.Length!=320)||!swirling&&AnimationArgument(a[5]) is <0 or >1)Fail(command,"Source snow speed/grouping differs.");
        return true;
    }
    private bool TryCreateSnowSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(command.Args[0],out var recipe)||recipe.Callback is not ("AnimSwirlingSnowball" or "AnimWaveFromCenterOfTarget"))return false;
        if(!_loaded.Contains(recipe.Callback=="AnimSwirlingSnowball"?"ANIM_TAG_ICE_CRYSTALS":"ANIM_TAG_ICE_SPIKES"))Fail(command,"Source snow graphics are not loaded.");
        _sprites.Add(new SnowParticle(slot,priority,recipe,this));return true;
    }
    private sealed class SnowParticle:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _destination;private readonly int _speed;private readonly bool _player;
        private PixelPoint _position,_offset;private SourceFastLinear? _linear;private int _state,_angle,_orbit;
        public SnowParticle(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=owner._animationArgs;_player=owner._cue.Actor.IsPlayer;
            var actor=owner._battlers[owner._cue.Actor.Value];var target=owner._battlers[owner._cue.Target.Value];
            if(recipe.Callback=="AnimWaveFromCenterOfTarget")
            {
                var p=a[2]==0?target.Base(owner._doubles):owner.AverageElementalTarget(false);
                if(a[2]!=0&&!_player)a[0]=unchecked((short)-a[0]);
                _position=new(unchecked((short)(p.X+(a[2]==0?owner.InitialXDirection()*a[0]:a[0]))),unchecked((short)(p.Y+a[1])));return;
            }
            var start=actor.Center(owner._doubles,true);_position=new(unchecked((short)(start.X+owner.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
            var end=a[5]==0?target.Center(owner._doubles,true):owner.AverageElementalTarget(true);
            _destination=new(unchecked((short)(end.X+(_player?a[2]:-a[2]))),unchecked((short)(end.Y+(a[5]==0?a[3]:0))));_speed=a[4];
            var reverse=new SourceFastLinear(_destination.X-_position.X,_destination.Y-_position.Y,_speed).ReverseCopy();
            for(int step=0;;step++)
            {
                if(step==65536)throw new InvalidDataException("Source reverse snowball translation did not leave the screen.");
                reverse.StepContinuing(out var backward);
                if(_position.X+backward.X is < -16 or >256||_position.Y+backward.Y is < -16 or >160)
                {_position=new(unchecked((short)(_position.X+backward.X)),unchecked((short)(_position.Y+backward.Y)));break;}
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_recipe.Callback=="AnimWaveFromCenterOfTarget")
            {if(_frames.Ended)Finished=true;else _frames.Step();return;}
            switch(_state)
            {
                case 0:
                    _linear=new(_destination.X-_position.X,_destination.Y-_position.Y,_speed);_linear.Step(out _offset);_state=1;break;
                case 1:if(_linear!.Step(out _offset))_state=2;break;
                case 2:
                    _position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=default;_angle=128;_orbit=0;_state=3;Orbit(owner);break;
                case 3:
                    if(_orbit<=31)Orbit(owner);
                    else{_position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=default;_linear!.ResetAccumulators();_state=4;}break;
                case 4:
                    _linear!.StepContinuing(out _offset);
                    if((uint)(_position.X+_offset.X+16)>272||_position.Y+_offset.Y is < -16 or >256)Finished=true;break;
            }
            if(!Finished)_frames.Step();
        }
        private void Orbit(SourceBattleAnimation owner)
        {
            int amplitude=_player?-20:20;var sine=owner._script.SineTable;
            _offset=new((sine[_angle]*amplitude>>8)-(sine[128]*amplitude>>8),(sine[_angle+64]*15>>8)-(sine[192]*15>>8));_angle=(_angle+16)&255;_orbit++;
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!Finished,_frames.Frame.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_frames.Frame.VFlip};
    }
}
