using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public static class SourcePhysicalAnimationCatalog
{
    public static IReadOnlyDictionary<string,SourcePixelAsset> Assets(string path)
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path));
        return doc.RootElement.GetProperty("assets").EnumerateArray().Select(r=>r.GetProperty("id").GetString()!).ToDictionary(id=>id,id=>SourceHitAnimationCatalog.Asset(doc,Path.GetDirectoryName(path)!,id));
    }
    public static bool Supports(int move) => move is 3 or 4 or 167 or 207 or 321 or 111 or 303 or 339 or 150 or 300 or 20 or 35 or 2 or 66 or 77 or 78 or 79 or 29 or 64 or 21 or 22 or 11 or 23 or 26 or 27 or 34 or 36 or 38 or 67 or 70 or 136 or 289;
    public static IReadOnlyDictionary<int,SourceAnimationScript> Read(string path,JsonDocument hitManifest)
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path));
        var shared=SourceHitAnimationCatalog.Script(hitManifest,1);
        var taskAffinePrograms=doc.RootElement.TryGetProperty("taskAffinePrograms",out var affinePrograms)?SourceTaskAffineProgram.Read(affinePrograms):new Dictionary<string,SourceTaskAffineCommand[]>();
        var recipes=shared.SpriteRecipes.Concat(SourceHitAnimationCatalog.Recipes(doc.RootElement.GetProperty("spriteRecipes"))).ToDictionary(p=>p.Key,p=>p.Value);
        var subroutines=doc.RootElement.GetProperty("subroutines").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray());
        var scripts=doc.RootElement.GetProperty("scripts").EnumerateArray().Select(r=>new SourceAnimationScript(r.GetProperty("moveId").GetInt32(),r.GetProperty("label").GetString()!,r.GetProperty("commands").EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray()){TaskAffinePrograms=taskAffinePrograms,SineTable=shared.SineTable,SpriteRecipes=recipes,Subroutines=subroutines,Labels=r.TryGetProperty("labels",out var labels)?labels.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetInt32()):new Dictionary<string,int>()}).ToDictionary(s=>s.MoveId);
        if(scripts.Count==0||scripts.Keys.Any(id=>!Supports(id)))throw new InvalidDataException("Physical script catalog differs.");
        return scripts;
    }
}
public sealed partial class SourceBattleAnimation
{
    private bool ValidatePhysicalOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="loadspritegfx"&&a.Length==1&&a[0] is "ANIM_TAG_SLAM_HIT" or "ANIM_TAG_WHIP_HIT" or "ANIM_TAG_POISON_POWDER" or "ANIM_TAG_SLEEP_POWDER" or "ANIM_TAG_STUN_SPORE" or "ANIM_TAG_POISON_BUBBLE")return true;
        if(c.Op=="createsprite"&&a.FirstOrDefault() is "gPoisonPowderParticleSpriteTemplate" or "gSleepPowderParticleSpriteTemplate" or "gStunSporeParticleSpriteTemplate")
        {
            if(a.Length!=9||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!="AnimMovePowderParticle"||recipe.Width!=8||recipe.Height!=16||_script.SineTable.Length!=320)Fail(c,"Powder recipe differs.");
            if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[3]) is < -127 or >127||Number(a[4]) is < -127 or >127||Number(a[5]) is <1 or >255||Number(a[6]) is < -32768 or >32767||Number(a[7]) is < -127 or >127||Number(a[8]) is <0 or >255)Fail(c,"Powder operands differ.");return true;
        }
        if(c.Op=="createsprite"&&a.FirstOrDefault() is "gSlamHitSpriteTemplate" or "gVineWhipSpriteTemplate")
        {
            if(a.Length!=5||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!="AnimWhipHit"||recipe.Asset is not ("slam-hit" or "whip-hit"))Fail(c,"Whip source recipe differs.");
            if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[3]) is < -127 or >127||Number(a[4]) is < -127 or >127)Fail(c,"Whip coordinates differ.");return true;
        }
        if(c.Op is "playsewithpan" or "loopsewithpan" or "waitplaysewithpan"&&a.FirstOrDefault() is "SE_M_MEGA_KICK2" or "SE_M_JUMP_KICK" or "SE_M_VICEGRIP" or "SE_M_SWIFT" or "SE_M_SWAGGER" or "SE_M_HORN_ATTACK" or "SE_M_POISON_POWDER")
        {if(a.Length!=(c.Op=="loopsewithpan"?4:c.Op=="waitplaysewithpan"?3:2)||a[1] is not ("SOUND_PAN_TARGET" or "SOUND_PAN_ATTACKER"))Fail(c,"Physical sound operands differ.");if(c.Op=="loopsewithpan"&&(Number(a[2]) is <0 or >255||Number(a[3]) is <2 or >255))Fail(c,"Physical sound loop differs.");if(c.Op=="waitplaysewithpan"&&Number(a[2]) is <0 or >255)Fail(c,"Delayed sound duration differs.");return true;}
        if(c.Op=="createsprite"&&a.FirstOrDefault()=="gSimplePaletteBlendSpriteTemplate")
        {
            if(a.Length!=8)Fail(c,"Simple palette argument count differs.");var p=a.Skip(3).Select(PaletteNumber).ToArray();
            if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||p[0] is <0 or >31||p[1] is < -128 or >127||p[2] is <0 or >16||p[3] is <0 or >16)Fail(c,"Simple palette operands differ.");return true;
        }
        if(c.Op=="createsprite"&&a.FirstOrDefault()=="gComplexPaletteBlendSpriteTemplate")
        {
            if(a.Length!=10||a[1] is not ("ANIM_ATTACKER" or "ANIM_TARGET"))Fail(c,"Palette callback shape differs.");
            var p=a.Skip(3).Select(PaletteNumber).ToArray();
            if(p[0] is <0 or >31||p[1] is <0 or >255||p[2] is <0 or >255||p[4] is <0 or >16||p[6] is <0 or >16)Fail(c,"Palette callback range differs.");return true;
        }
        int count=c.Op=="createsprite"?a.FirstOrDefault() switch{"gBowMonSpriteTemplate"=>4,"gFlashingHitSplatSpriteTemplate"=>7,"gViceGripSpriteTemplate"=>4,"gVerticalDipSpriteTemplate"=>6,"gStompFootSpriteTemplate"=>6,"gJumpKickSpriteTemplate" or "gKarateChopSpriteTemplate"=>11,"gSlidingKickSpriteTemplate"=>9,_=>0}:c.Op=="createvisualtask"?a.FirstOrDefault() switch{"AnimTask_ShakeAndSinkMon" or "AnimTask_ShakeMonInPlace"=>7,"AnimTask_WindUpLunge"=>9,"AnimTask_RotateMonToSideAndRestore" or "AnimTask_RotateMonSpriteToSide"=>6,_=>0}:0;
        if(count==0)return false;
        if(a.Length!=count)Fail(c,"Physical callback operand count differs.");
        for(int i=1;i<a.Length;i++)
            if(!int.TryParse(a[i] switch{"ANIM_ATTACKER"=>"0","ANIM_TARGET"=>"1",_=>a[i]},out int value)||value is < -32768 or >32767)Fail(c,"Physical callback requires signed source operands.");
        if(c.Op=="createsprite")
        {
            if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Physical sprite participant/priority differs.");
            if(a[0]=="gBowMonSpriteTemplate")
            {if(Number(a[3]) is <0 or >2)Fail(c,"Bow stage differs.");return true;}
            if(a[0]=="gFlashingHitSplatSpriteTemplate")
            {if(Number(a[3]) is < -127 or >127||Number(a[4]) is < -127 or >127||Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >3)Fail(c,"Flashing hit operands differ.");return true;}
            if(a[0]=="gViceGripSpriteTemplate")
            {if(Number(a[3]) is <0 or >1||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!="AnimViceGripPincer"||recipe.Asset!="cut")Fail(c,"Vice Grip recipe differs.");return true;}
            if(a[0]!="gVerticalDipSpriteTemplate"&&(Number(a[3]) is < -64 or >64||Number(a[4]) is < -64 or >64))Fail(c,"Physical sprite coordinate range differs.");
            if(a[0]=="gVerticalDipSpriteTemplate"&&(Number(a[3]) is <1 or >255||Number(a[5]) is <0 or >1))Fail(c,"Vertical dip operands differ.");
            if(a[0]=="gStompFootSpriteTemplate"&&Number(a[5]) is <0 or >255)Fail(c,"Stomp delay differs.");
            if(a[0] is "gJumpKickSpriteTemplate" or "gKarateChopSpriteTemplate"&&(Number(a[7]) is <1 or >255||Number(a[8]) is <0 or >1||Number(a[9]) is <0 or >4||Number(a[10]) is <0 or >1))Fail(c,"Jump kick translation differs.");
            if(a[0]=="gSlidingKickSpriteTemplate"&&(Number(a[6]) is <1 or >255||_script.SineTable.Length!=320))Fail(c,"Sliding kick translation/sine table differs.");
        }
        else if(a[0]=="AnimTask_WindUpLunge")
        {if(Number(a[2]) is <0 or >1||Number(a[5]) is <1 or >255||Number(a[6]) is <0 or >255||Number(a[8]) is <1 or >255||_script.SineTable.Length!=320)Fail(c,"Wind-up operands differ.");}
        else if(a[0] is "AnimTask_RotateMonSpriteToSide" or "AnimTask_RotateMonToSideAndRestore")
        {if(Number(a[2]) is <1 or >255||Number(a[4]) is <0 or >1||Number(a[5]) is <0 or >2)Fail(c,"Rotation task operands differ.");}
        else if(Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >1||Number(a[4]) is <0 or >255||Number(a[6]) is <1 or >255)Fail(c,"Sink task operands differ.");
        return true;
    }
    // InitAnimLinearTranslation/AnimTranslateLinear store direction in bit zero,
    // unsigned 16-bit accumulators, and report completion on the next callback.
    private sealed class PhysicalLinear(int dx,int dy,int duration)
    {
        private readonly int _vx=((Math.Abs(dx)<<8)/duration&~1)|(dx<0?1:0),_vy=((Math.Abs(dy)<<8)/duration&~1)|(dy<0?1:0);
        private int _x,_y,_remaining=duration;
        // Some source callbacks overwrite data[0]=1 before each translation step.
        public void StepContinuing(out PixelPoint offset){_remaining=1;Step(out offset);}
        public bool Step(out PixelPoint offset)
        {
            if(_remaining==0){offset=default;return true;}
            _x=(_x+_vx)&65535;_y=(_y+_vy)&65535;_remaining--;
            offset=new((_x>>8)*((_vx&1)==0?1:-1),(_y>>8)*((_vy&1)==0?1:-1));return false;
        }
    }
    private sealed class PhysicalFoot:VisualSprite
    {
        private readonly string _template;private readonly PixelPoint _position,_destination;
        private readonly SourceSpriteAnimFrame _image;private readonly int _amplitude,_angleStep;
        private PhysicalLinear? _linear;private PixelPoint _offset;private int _state,_wait,_angle;
        public PhysicalFoot(int slot,int priority,string template,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {
            _template=template;if(!owner._loaded.Contains("ANIM_TAG_HANDS_AND_FEET"))throw new InvalidDataException("Physical foot graphics have not been loaded.");var target=owner._battlers[owner._cue.Target.Value];var center=target.Center(owner._doubles,true);
            int x=a[0],y=a[1],anim=1;
            if(template is "gJumpKickSpriteTemplate" or "gKarateChopSpriteTemplate")
            {
                if(a[7]==1&&!owner._cue.Actor.IsPlayer)y=-y;
                // AnimTravelDiagonally calls target initialization twice for arg5=1.
                var start=a[5]==0?owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true):center;
                _position=new(start.X+2*owner.InitialXDirection()*x,start.Y+2*y);
                var dest=owner._battlers[(a[5]==0?owner._cue.Actor:owner._cue.Target).Value].Center(owner._doubles,true);
                _destination=new(dest.X+(owner._cue.Actor.IsPlayer?a[2]:-a[2]),dest.Y+(a[7]==1&&!owner._cue.Actor.IsPlayer?-a[3]:a[3]));
                _wait=a[4];anim=a[6];
            }
            else
            {
                if(template=="gSlidingKickSpriteTemplate"&&owner._cue.Actor.Partner==owner._cue.Target&&owner._cue.Target.Value<2)x=-x;
                _position=new(center.X+owner.InitialXDirection()*x,center.Y+y);
                if(template=="gStompFootSpriteTemplate"){_destination=target.Center(owner._doubles,true);_wait=a[2];_state=0;}
                else{_destination=new(_position.X+(owner._cue.Actor.IsPlayer?a[2]:-a[2]),_position.Y);_linear=new(_destination.X-_position.X,0,a[3]);_amplitude=a[5];_angleStep=a[4];_state=2;}
            }
            _image=owner._script.SpriteRecipes["gFistFootSpriteTemplate"].Animations[anim][0];
            if(template is "gJumpKickSpriteTemplate" or "gKarateChopSpriteTemplate")_state=1;
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_template,_position,_offset,_image.Tile,true,_image.HFlip,false,256,"hands-and-feet",Subpriority);
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_state)
            {
                case 0:if(--_wait==-1){_wait=6;_state=1;}break;
                case 1:_linear=new(_destination.X-_position.X,_destination.Y-_position.Y,_wait);_state=2;goto case 2;
                case 2:
                    if(_linear!.Step(out var offset)){_state=_template=="gStompFootSpriteTemplate"?3:5;if(_template=="gSlidingKickSpriteTemplate")Finished=true;}
                    else{_offset=offset;if(_template=="gSlidingKickSpriteTemplate"){_offset=new(offset.X,offset.Y+((_amplitude*owner._script.SineTable[(_angle>>8)&255])>>8));_angle=(short)(_angle+_angleStep);}}
                    break;
                case 3:_wait=15;_state=4;break;
                case 4:if(_wait>0)_wait--;else _state=5;break;
                case 5:Finished=true;break;
            }
        }
    }
    private sealed class ShakeAndSinkTask(AnimationSlot slot,int x,int delay,int speed,int duration):VisualTask
    {
        private int _x=x,_timer,_fraction,_remaining=duration;
        public override void Step(SourceBattleAnimation owner)
        {
            var offset=owner._offsets[slot.Value];
            if(delay==_timer++){_timer=0;if(offset.X==_x)_x=-_x;offset=new(offset.X+_x,offset.Y);}
            _fraction=(short)(_fraction+speed);owner._offsets[slot.Value]=new(offset.X,_fraction>>8);
            if(--_remaining==0)Finished=true;
        }
    }
    private sealed class VerticalDip(int slot,int priority,AnimationSlot battler,int duration,int delta):VisualSprite(slot,priority)
    {
        private readonly int _duration=duration;
        private int _remaining=duration,_delta=delta,_state;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gVerticalDipSpriteTemplate",default,default,0,false,false,false,256,null,Subpriority);
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state is 0 or 2)
            {if(_remaining>0){_remaining--;var p=owner._offsets[battler.Value];owner._offsets[battler.Value]=new(p.X,p.Y+_delta);}else _state++;}
            else if(_state==1){_remaining=_duration;_delta=-_delta;_state=2;}
            else Finished=true;
        }
    }
    private sealed class InPlaceShake(AnimationSlot slot,int x,int y,int shakes,int delay):VisualTask
    {
        private int _count,_timer;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_timer!=0){_timer--;return;}
            var p=owner._offsets[slot.Value];int sign=(_count&1)==0?-1:1;
            p=new(p.X+sign*x*2,p.Y+sign*y*2);_timer=delay;
            if(++_count>=shakes){int restore=(_count&1)==0?-1:1;p=new(p.X+restore*x,p.Y+restore*y);Finished=true;}
            owner._offsets[slot.Value]=p;
        }
    }
    private sealed class RotateToSide:VisualTask
    {
        private readonly int _slot,_duration;private readonly bool _offsetBoth;private int _rotation,_speed,_mode,_count;
        public RotateToSide(SourceBattleAnimation owner,int duration,int speed,AnimationSlot slot,int mode,bool offsetBoth=false)
        {
            _offsetBoth=offsetBoth;_slot=slot.Value;_duration=duration;
            if(offsetBoth&&!slot.IsPlayer)owner._animationArgs[1]=unchecked((short)-owner._animationArgs[1]);
            _speed=unchecked((short)(slot.IsPlayer?-speed:speed));_mode=mode;_rotation=unchecked((short)(mode==1?duration*_speed:0));owner.PrepareBattlerRotScale(_slot,0);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            _rotation=(short)(_rotation+_speed);owner.SetBattlerRotScale(_slot,256,256,_rotation);
            if(_offsetBoth||(_slot&1)==0){var p=owner._offsets[_slot];owner._offsets[_slot]=new(p.X,Math.Abs(owner._matrixC[_slot])>>3);}
            if(++_count<_duration)return;
            if(_mode==2){_count=0;_speed=unchecked((short)-_speed);_mode=1;return;}
            if(_mode==1)owner.ResetBattlerRotScale(_slot);
            Finished=true;
        }
    }

    private sealed class WindUpLunge:VisualTask
    {
        private readonly AnimationSlot _slot;private readonly int _vx,_vy,_amplitude,_wave;
        private int _first,_delay,_second,_x,_angle,_lunge;
        public WindUpLunge(SourceBattleAnimation owner,int[] a)
        {
            _slot=a[0]==0?owner._cue.Actor:owner._cue.Target;int direction=owner._cue.Actor.IsPlayer?1:-1;
            _first=a[3];_delay=a[4];_second=a[6];_vx=direction*a[1]*256/_first;_vy=direction*a[5]*256/_second;_amplitude=a[2];_wave=32768/_first;
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_first>0){_x=(short)(_x+_vx);owner._offsets[_slot.Value]=new(_x>>8,(_amplitude*owner._script.SineTable[(_angle>>8)&255])>>8);_angle=(short)(_angle+_wave);_first--;}
            else if(_delay>0)_delay--;
            else{_lunge=(short)(_lunge+_vy);var p=owner._offsets[_slot.Value];owner._offsets[_slot.Value]=new((_lunge>>8)+(_x>>8),p.Y);if(--_second==0)Finished=true;}
        }
    }

    private sealed class SimplePaletteSprite:VisualSprite
    {
        public SimplePaletteSprite(int slot,int priority,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {if(!owner.PaletteFadeActive){owner._fade=new(owner.SelectPalettes(a[0]),a[2],a[3],(ushort)a[4],a[1]);owner._fade.Update(owner);}}
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gSimplePaletteBlendSpriteTemplate",default,default,0,false,false,false,256,null,Subpriority);
        public override void Step(SourceBattleAnimation owner){if(!owner.PaletteFadeActive)Finished=true;}
    }

    private sealed class FlashingHit:VisualSprite
    {
        private readonly PixelPoint _position;private readonly int _scale;private int _count;private bool _invisible;
        public FlashingHit(int slot,int priority,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {
            if(!owner._loaded.Contains("ANIM_TAG_IMPACT"))throw new InvalidDataException("Impact graphics have not been loaded.");
            var center=owner._battlers[(a[2]==0?owner._cue.Actor:owner._cue.Target).Value].Center(owner._doubles,true);
            _position=new(center.X+owner.InitialXDirection()*a[0],center.Y+a[1]);_scale=a[3] switch{0=>256,1=>216,2=>176,_=>128};
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gFlashingHitSplatSpriteTemplate",_position,default,0,!_invisible,false,false,_scale,"impact",Subpriority);
        public override void Step(SourceBattleAnimation owner){_invisible=!_invisible;if(_count++>12)Finished=true;}
    }
    private sealed class BowMon(int slot,int priority,int stage):VisualSprite(slot,priority)
    {
        private int _state,_count,_delta,_rotation;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gBowMonSpriteTemplate",default,default,0,false,false,false,256,null,Subpriority);
        public override void Step(SourceBattleAnimation owner)
        {
            int actor=owner._cue.Actor.Value,direction=owner._cue.Actor.IsPlayer?1:-1;
            if(stage is 0 or 1)
            {
                if(_state==0){_count=stage==0?6:4;_delta=direction*(stage==0?-2:3);_state=1;return;}
                if(_state==1)
                {if(_count>0){_count--;var p=owner._offsets[actor];owner._offsets[actor]=new(p.X+_delta,p.Y);}else _state=stage==0?2:4;return;}
                if(_state==2)
                {
                    owner.PrepareBattlerRotScale(actor,0);_rotation-=direction*768;owner.SetBattlerRotScale(actor,256,256,_rotation);
                    var p=owner._offsets[actor];owner._offsets[actor]=new(p.X,Math.Abs(owner._matrixC[actor])>>3);
                    if(++_count>3)_state=4;return;
                }
            }
            else
            {
                if(_state==0){if(++_count>8){_count=0;_rotation=-direction*3072;_state=3;}return;}
                if(_state==3)
                {
                    _rotation+=direction*1024;owner.SetBattlerRotScale(actor,256,256,_rotation);var p=owner._offsets[actor];owner._offsets[actor]=new(p.X,Math.Abs(owner._matrixC[actor])>>3);
                    if(++_count>2){owner.SetBattlerRotScale(actor,256,256,0);owner.ResetBattlerRotScale(actor);_state=4;}return;
                }
            }
            Finished=true;
        }
    }

}
