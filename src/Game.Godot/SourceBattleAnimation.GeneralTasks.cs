namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateGeneralTaskCommand(SourceAnimationCommand c)
    {
        if(c.Op=="createvisualtask"&&c.Args.FirstOrDefault()=="AnimTask_DoomDesireLightBeam")
        {
            if(c.Args.Length!=2||Number(c.Args[1]) is <0 or >255)Fail(c,"Light-beam task takes only source priority.");
            if(!_validatingInvokedProgram)return true;
            if(!_script.OverlayDependencies.Contains("morning-sun")||!_script.OverlayPalettes.TryGetValue("morning-sun",out var palette)||palette.Length!=16)
                Fail(c,"Light-beam task requires the shared indexed morning-sun material.");
            var commands=OverlayInvocationCommands();
            if(commands.Count(IsOverlayCommand)!=1||commands.Any(x=>x.Op is "monbg" or "monbg_static" or "setalpha" or "blendoff" or "fadetobg" or "changebg"||
                x.Op=="createsprite"&&x.Args[0] is not ("gExplosionSpriteTemplate" or "gSimplePaletteBlendSpriteTemplate")||
                x.Op=="createvisualtask"&&x.Args[0] is not ("AnimTask_DoomDesireLightBeam" or "AnimTask_SetAnimTargetToBattlerTarget" or "AnimTask_ShakeMonInPlace")))
                Fail(c,"Light-beam requires one BG1 owner and its bounded non-emitting companions.");
            if(22+MaximumInvocationCost(x=>x.Op=="createsprite"?1:0)+_spriteScene.Sprites.Sum(x=>Math.Max(1,x.Frame.Subsprites.Length))>64)
                Fail(c,"Light-beam companions exceed the shared source OAM capacity.");
            return true;
        }
        if(c.Op!="createvisualtask"||c.Args.FirstOrDefault()!="AnimTask_SetAnimTargetToBattlerTarget")return false;
        if(c.Args.Length!=2||Number(c.Args[1]) is <0 or >255)Fail(c,"Source target assignment takes only task priority.");
        // The invocation already carries the command-owned gBattlerTarget. Both
        // delayed source programs put this synchronous setter before any visual
        // instruction, so there is no earlier target state to reconstruct.
        if(_validatingInvokedProgram&&(_cue.GeneralAnimationLabel is null||!ReferenceEquals(c,_invocationProgram.Commands[0])))
            Fail(c,"Committed target assignment must be the named controller's first instruction.");
        return true;
    }
    private bool TryCreateGeneralTask(SourceAnimationCommand c)
    {
        if(c.Args[0]=="AnimTask_DoomDesireLightBeam"){_tasks.Add(new DoomDesireBeamTask(this));return true;}
        if(c.Args[0]!="AnimTask_SetAnimTargetToBattlerTarget")return false;
        return true;
    }
    private sealed class DoomDesireBeamTask:VisualTask
    {
        private static readonly int[] Coordinates=[120,80,40,0],Delays=[0,0,0,0,50];
        private readonly int _baseX,_side;private int _phase=1,_alpha,_index,_timer;
        public DoomDesireBeamTask(SourceBattleAnimation owner)
        {
            int target=owner._cue.Target.Value;_side=owner._cue.Target.IsPlayer?-1:1;
            _baseX=owner._doubles?target switch{0=>14,1=>-155,2=>-20,3=>-115,_=>throw new InvalidDataException("Unknown native target position.")}:owner._cue.Target.IsPlayer?-10:-135;
            owner.BeginOverlay("morning-sun");owner.SetBackgroundPriority(1,1);owner.SetOverlayScroll(_baseX,0);owner.SetAlphaBlend(2,63,3,13);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 1:
                    _timer=0;
                    // Approved safe policy: the source's fifth coordinate read is
                    // outside its four-entry table. Keep the last legal position
                    // for that final visible callback; cleanup still waits a tick.
                    if(_index<Coordinates.Length)owner.SetOverlayScroll(_baseX+_side*Coordinates[_index],0);
                    _phase=++_index==5?5:2;break;
                case 2:_alpha=Math.Max(5,_alpha-1);owner.SetAlphaBlend(2,63,3,_alpha);if(_alpha==5)_phase=3;break;
                case 3:if(++_timer>Delays[_index])_phase=4;break;
                case 4:_alpha=Math.Min(13,_alpha+1);owner.SetAlphaBlend(2,63,3,_alpha);if(_alpha==13)_phase=1;break;
                case 5:
                    owner.SetOverlayScroll(0,0);owner.EndOverlay(false);owner.SetBackgroundPriority(1,1);owner.ClearAlphaBlend();Finished=true;break;
            }
        }
    }
}
