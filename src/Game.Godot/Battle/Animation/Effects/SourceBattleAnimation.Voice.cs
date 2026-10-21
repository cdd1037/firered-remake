namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateVoiceCommand(SourceAnimationCommand c)
    {
        var a=c.Args;if(a.Length==0)return false;
        if(c.Op=="createsprite"&&_script.SpriteRecipes.TryGetValue(a[0],out var found)&&found.Callback is "AnimRoarNoiseLine" or "AnimHyperVoiceRing")
        {
            var r=_script.SpriteRecipes[a[0]];bool noise=r.Callback=="AnimRoarNoiseLine";
            if(a.Length!=(noise?6:10)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>AnimationArgument(x) is <short.MinValue or >short.MaxValue))Fail(c,"Voice sprite operands differ.");
            if(noise){if(r.Width!=32||r.Height!=32||r.Blend||r.AffineAnimations.Length!=0||r.Animations.Length!=2||Number(a[5]) is <0 or >2)Fail(c,"Noise line recipe differs.");}
            else if(r.Width!=64||r.Height!=64||!r.Blend||!r.AffineDoubleSize||r.AffineAnimations.Length!=1||Number(a[3]) is <1 or >short.MaxValue||Number(a[8]) is <0 or >1||Number(a[9]) is <0 or >1)Fail(c,"Hyper Voice ring recipe differs.");
            return true;
        }
        if(c.Op!="createvisualtask")return false;
        if(a[0]=="AnimTask_DeepInhale")
        {
            if(a.Length!=3||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >3||!_script.TaskAffinePrograms.ContainsKey("sDeepInhaleAffineAnimCmds"))Fail(c,"Deep inhale task operands/program differ.");
            SourceTaskAffineProgram.Validate(_script.TaskAffinePrograms["sDeepInhaleAffineAnimCmds"]);return true;
        }
        if(a[0]=="AnimTask_ShakeBattleTerrain")
        {if(a.Length!=6||Number(a[1]) is <0 or >255||a.Skip(2).Take(2).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||Number(a[4]) is <1 or >short.MaxValue||Number(a[5]) is <0 or >short.MaxValue)Fail(c,"Terrain shake operands differ.");return true;}
        if(a[0] is not ("SoundTask_PlayCryHighPitch" or "SoundTask_PlayDoubleCry" or "SoundTask_PlayCryWithEcho" or "SoundTask_WaitForCry"))return false;
        bool selected=a[0] is "SoundTask_PlayCryHighPitch" or "SoundTask_PlayDoubleCry";
        if(a.Length!=(selected?4:2)||Number(a[1]) is <0 or >255||selected&&(Number(a[2]) is <0 or >3||AnimationArgument(a[3]) is <short.MinValue or >short.MaxValue))Fail(c,"Cry task source operands differ.");
        if(a[0]=="SoundTask_PlayDoubleCry"&&AnimationArgument(a[3]) is not (2 or 255))Fail(c,"Double-cry selector differs from reviewed Roar/Growl modes.");
        if(_validatingInvokedProgram)
        {
            if(!_audio.SupportsCries)Fail(c,"source-cry-audio-unavailable");
            if(a[0]!="SoundTask_WaitForCry")
            {
                int participant=selected?Number(a[2]):0,slot=VoiceParticipant(participant);
                if((participant is not (1 or 3)||IsSourceBattlerVisible(slot))&&(!_originalPartySpecies.TryGetValue(slot,out int species)||species is <0 or >ushort.MaxValue))Fail(c,"Original-party cry species is missing or outside source width.");
            }
        }
        return true;
    }
    private int VoiceParticipant(int participant)=>participant switch{0=>_cue.Actor.Value,1=>_cue.Target.Value,2=>_cue.Actor.Value^2,_=>_cue.Target.Value^2};
    private bool TryCreateVoiceSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(c.Args[0],out var r)||r.Callback is not ("AnimRoarNoiseLine" or "AnimHyperVoiceRing"))return false;
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Voice graphics not loaded.");
        _sprites.Add(new VoiceSprite(slot,priority,r,c.Args.Skip(3).Select(AnimationArgument).ToArray(),this));return true;
    }
    private bool TryCreateVoiceTask(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(a[0]=="AnimTask_DeepInhale")
        {if(ResolveShakeParticipant(Number(a[2]),false) is {} slot)_tasks.Add(new DeepInhaleTask(this,slot,_script.TaskAffinePrograms["sDeepInhaleAffineAnimCmds"]));return true;}
        if(a[0]=="AnimTask_ShakeBattleTerrain")
        {var t=new VoiceTerrainShake(this,a.Skip(2).Select(Number).ToArray());if(!t.Finished)_tasks.Add(t);return true;}
        if(a[0]=="SoundTask_WaitForCry"){_tasks.Add(new VoiceCryTask(0,0,0,true));return true;}
        if(a[0] is not ("SoundTask_PlayCryHighPitch" or "SoundTask_PlayDoubleCry" or "SoundTask_PlayCryWithEcho"))return false;
        int participant=a[0]=="SoundTask_PlayCryWithEcho"?0:Number(a[2]),b=VoiceParticipant(participant);
        if(participant is 1 or 3&&!IsSourceBattlerVisible(b))return true;
        int species=_originalPartySpecies[b],pan=AdjustPanning(-64);if(species==0)return true;
        int mode=a[0] switch{"SoundTask_PlayCryHighPitch"=>3,"SoundTask_PlayDoubleCry"=>AnimationArgument(a[3])==255?9:7,_=>4};
        _audio.PlayCry(new(species,mode,pan,_frame));
        if(mode!=3)_tasks.Add(new VoiceCryTask(species,pan,mode==9?10:mode==7?8:6,false));return true;
    }
    private sealed class VoiceCryTask(int species,int pan,int secondMode,bool waitOnly):VisualTask
    {
        private short _state;
        public override void Step(SourceBattleAnimation owner)
        {if(_state<2){_state++;return;}if(owner._audio.IsCryPlaying)return;if(!waitOnly)owner._audio.PlayCry(new(species,secondMode,pan,owner._frame));Finished=true;}
    }
    private sealed class DeepInhaleTask(SourceBattleAnimation owner,AnimationSlot slot,SourceTaskAffineCommand[] commands):TaskAffinePlayback(owner,slot,commands)
    {
        private short _counter,_delay,_shakes;
        public override void Step(SourceBattleAnimation owner)
        {
            ushort window=unchecked((ushort)(_counter++-20));
            if(window<23){if(++_delay>1){_delay=0;_shakes++;owner._offsets[Battler]=new((_shakes&1)!=0?1:-1,owner._offsets[Battler].Y);}}
            else owner._offsets[Battler]=new(0,owner._offsets[Battler].Y);
            base.Step(owner);
        }
    }
    private sealed class VoiceTerrainShake:VisualTask
    {
        private readonly short _x,_y,_delay;private short _remaining,_timer;
        public VoiceTerrainShake(SourceBattleAnimation owner,int[] a)
        {_x=(short)a[0];_y=(short)a[1];_remaining=(short)a[2];_timer=_delay=(short)a[3];owner.BackgroundScrollX=unchecked((ushort)_x);owner.BackgroundScrollY=unchecked((ushort)_y);Step(owner);}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_timer!=0){_timer--;return;}
            owner.BackgroundScrollX=unchecked((ushort)(owner.BackgroundScrollX==_x?-_x:_x));
            owner.BackgroundScrollY=owner.BackgroundScrollY==-_y?(ushort)0:unchecked((ushort)-_y);
            _timer=_delay;if(--_remaining==0){owner.BackgroundScrollX=owner.BackgroundScrollY=0;Finished=true;}
        }
    }
    private sealed class VoiceSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private readonly PixelPoint _position;private PixelPoint _offset;private readonly PhysicalLinear? _linear;
        private readonly bool _hflip,_vflip;private readonly int _priority;private short _x,_y,_vx,_vy,_ticks;
        public VoiceSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_priority=priority;bool noise=recipe.Callback=="AnimRoarNoiseLine";
            _frames=new(recipe,noise&&a[2]==2?1:0);
            if(noise)
            {
                var actor=owner._battlers[owner._cue.Actor.Value];if(!actor.Slot.IsPlayer){a[0]=unchecked((short)-a[0]);owner._animationArgs[0]=(short)a[0];_hflip=true;}
                var p=actor.Base(owner._doubles);_position=new(unchecked((short)(p.X+a[0])),unchecked((short)(p.Y+a[1])));_vx=(short)(_hflip?-640:640);_vy=(short)(a[2]==0?-640:a[2]==1?640:0);_vflip=a[2]==1;
            }
            else
            {
                _affine=new(recipe.AffineAnimations[0]);var first=owner._battlers[a[5]==0?owner._cue.Actor.Value:owner._cue.Target.Value];var second=owner._battlers[a[5]==0?owner._cue.Target.Value:owner._cue.Actor.Value];
                PixelPoint Coord(AnimationBattler b)=>a[6]!=0?b.Center(owner._doubles,true):b.Base(owner._doubles);
                bool Partner(AnimationBattler b,out AnimationBattler p){if(owner._doubles&&owner._battlers.TryGetValue(b.Slot.Value^2,out var q)&&owner.IsSourceBattlerVisible(q.Slot.Value)){p=q;return true;}p=b;return false;}
                var start=Coord(first);_position=new(unchecked((short)(start.X+(first.Slot.IsPlayer?-a[0]:a[0]))),unchecked((short)(start.Y+a[1])));
                if(!first.Slot.IsPlayer)_priority=unchecked((byte)((Partner(second,out var secondPartner)?secondPartner:second).Subpriority-1));
                else if(Partner(first,out var p)&&first.Base(true).X<p.Base(true).X)_priority=unchecked((byte)(p.Subpriority+1));
                else _priority=unchecked((byte)(first.Subpriority-1));
                var end=Coord(second);if(Partner(second,out var other)){var q=Coord(other);end=new((end.X+q.X)/2,(end.Y+q.Y)/2);}
                end=new(unchecked((short)(end.X+(second.Slot.IsPlayer?-a[3]:a[3]))),unchecked((short)(end.Y+a[4])));
                _linear=new(end.X-_position.X,end.Y-_position.Y,a[0]);Step(owner);
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_linear is not null){if(_linear.Step(out var p))Finished=true;else _offset=p;return;}
            _x=unchecked((short)(_x+_vx));_y=unchecked((short)(_y+_vy));_offset=new(_x>>8,_y>>8);if(++_ticks==14)Finished=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner){_frames.Step();_affine?.Step();}
        public override SourceVisualSpriteFrame Frame
        {
            get {var f=new SourceVisualSpriteFrame(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,_hflip^_frames.Frame.HFlip,_recipe.Blend,256,_recipe.Asset,_priority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_vflip^_frames.Frame.VFlip};if(_affine is null)return f;var m=_affine.Matrix;return f with{Affine=true,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}
        }
    }
}
