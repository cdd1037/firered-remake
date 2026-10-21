namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SpiteShadowTaskName="AnimTask_SpiteTargetShadow";
    private bool ValidateSpiteCommand(SourceAnimationCommand c)
    {
        if(c.Op!="createvisualtask"||c.Args.FirstOrDefault()!=SpiteShadowTaskName)return false;
        if(c.Args.Length!=2||Number(c.Args[1]) is <0 or >255||_script.SineTable.Length!=320||!_script.SpriteTags.ContainsKey("ANIM_TAG_BENT_SPOON"))
            Fail(c,"Spite requires its source priority, sine table and owned palette tag.");
        if(!_validatingInvokedProgram)return true;
        int target=_cue.Target.Value;
        if(!IsSourceBattlerPresent(target)||_spriteMaterials is null||!_spriteMaterials.TryGetValue(_battlers[target].Asset,out var material)||material is not {Width:64,Height:64,Pixels.Length:4096})
            Fail(c,"Spite requires its present target and current 64x64 body material.");
        var commands=OverlayInvocationCommands();
        if(commands.Count(x=>x.Op=="createvisualtask"&&x.Args[0]==SpiteShadowTaskName)!=1||commands.Any(x=>
            x.Op is "createsprite" or "loadspritegfx" or "unloadspritegfx" or "visible" or "invisible" or "monbg_static" or "clearmonbg_static"||
            x.Op=="createvisualtask"&&x.Args[0] is not(SpiteShadowTaskName or "AnimTask_BlendColorCycle"))||
            commands.Any(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_BlendColorCycle"&&
                (x.Args.Length!=8||x.Args[2]!="F_PAL_ATTACKER"||x.Args[3]!="2"||x.Args[4]!="6"||x.Args[5]!="0"||x.Args[6]!="8"||x.Args[7]!="RGB_WHITE")))
            Fail(c,"Spite requires one raw clone/wave owner and its reviewed attacker palette companion.");
        int palette=FindSpritePalette(65535);
        if(palette is 15 or 255)Fail(c,"Spite requires a free palette bank4–14; the native slot15 leak boundary is not admitted.");
        int start=Math.Max(0,BattlerPosition(target).Y+_offsets[target].Y-32);
        if(start>191)Fail(c,"Spite's 64-line wave must fit its native byte end-line operand.");
        return true;
    }
    private bool TryCreateSpiteTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!=SpiteShadowTaskName)return false;
        var task=new SpiteShadowTask(this);if(!task.Finished)_tasks.Add(task);return true;
    }
    private sealed class SpiteShadowTask:VisualTask
    {
        private readonly int _target,_tag,_palette;
        private readonly SpiteShadowClone? _copy;
        private int _phase=1,_elapsed,_alphaA,_alphaB=16,_cleanup;
        public SpiteShadowTask(SourceBattleAnimation o)
        {
            _target=o._cue.Target.Value;_tag=o._script.SpriteTags["ANIM_TAG_BENT_SPOON"];
            _palette=o.AllocateSpritePalette(_tag);
            if(_palette is 15 or 255){Finished=true;return;}
            int slot=o.FindFreeSpriteSlot();
            if(!o.IsSourceBattlerPresent(_target)||slot==64){o.FreeSpritePalette(_tag);Finished=true;return;}
            _copy=new SpiteShadowClone(slot,o,_target,_palette);o._sprites.Add(_copy);
            o.SetBattlerBackgroundEnabled(_target,false);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase<5)
            {
                switch(_phase++)
                {
                    case 1:o.BlendPalettes(1u<<(16+_target),10,13|(15<<10));break;
                    case 2:
                        int start=Math.Max(0,o.BattlerPosition(_target).Y+o._offsets[_target].Y-32);
                        o.StartBackgroundScanlineWave(o._battlers[_target].BackgroundRank,start,start+64,2,6,0);break;
                    case 3:o.SetAlphaBlend(1<<o._battlers[_target].BackgroundRank,63,0,16);break;
                    case 4:o.SetBattlerBackgroundEnabled(_target,true);break;
                }
                return;
            }
            if(_phase==5)
            {
                ++_elapsed;
                if((_elapsed&1)==0)_alphaA=o._script.SineTable[_elapsed]/18;
                else _alphaB=16-o._script.SineTable[_elapsed]/18;
                o.SetAlphaBlend(1<<o._battlers[_target].BackgroundRank,63,_alphaA,_alphaB);
                if(_elapsed!=128)return;
                _phase=6;
            }
            switch(_cleanup++)
            {
                case 0:o.StopScanlineDeferred();o.SetBattlerBackgroundEnabled(_target,false);break;
                case 1:o.BlendPalettes(1u<<(16+_target),0,13|(15<<10));break;
                case 2:
                    o.SetSourceBattlerSpriteInvisible(_target,true);_copy!.Destroy();o._sprites.Remove(_copy);
                    o.FreeSpritePalette(_tag);o.ClearAlphaBlend();o.SetBattlerBackgroundEnabled(_target,true);Finished=true;break;
            }
        }
    }
    private sealed class SpiteShadowClone:VisualSprite
    {
        private readonly SourceVisualSpriteFrame _frame;
        public SpiteShadowClone(int slot,SourceBattleAnimation o,int target,int palette):base(slot,o._battlers[target].Subpriority)
        {
            // The first built OAM frame is followed by setup's normal→faded
            // palette copy before transfer. No admitted companion changes normal
            // target colors; an immutable prefix therefore represents all16
            // copied colors exactly without inventing source palette indices.
            var normal=new SourcePaletteBlend(16+palette,0,0){Unfaded=o._unfadedPaletteStates.GetValueOrDefault(16+target)};
            _frame=o.CaptureBattlerCopyFrame(slot,target,"SpiteTargetShadow") with
                {PaletteSlot=palette,OamPriority=3,BattlerCopyPalette=normal,Visible=!o.SourceBattlerLogicalInvisible(target),Blend=false};
        }
        public override bool Counted=>false;
        public void Destroy()=>Finished=true;
        public override void Step(SourceBattleAnimation o){}
        public override SourceVisualSpriteFrame Frame=>_frame;
    }
}
