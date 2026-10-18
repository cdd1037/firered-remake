using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public sealed record SourceTaskAffineCommand(string Op,int X=0,int Y=0,int Rotation=0,int Duration=0,int Target=0,int Count=0);

/// <summary>PrepareAffineAnimInTaskData / RunAffineAnimFromTaskData, independent of sprite.c's affine player.</summary>
public sealed class SourceTaskAffineProgram
{
    private readonly SourceTaskAffineCommand[] _commands;
    public int CommandIndex{get;private set;}
    public int FrameTicks{get;private set;}
    public int LoopRemaining{get;private set;}
    public int XScale{get;private set;}=256;
    public int YScale{get;private set;}=256;
    public int Rotation{get;private set;}
    public bool Ended{get;private set;}
    public bool AppliedFrame{get;private set;}
    // Task scales are already the hardware's inverse scale. Its rotation byte
    // is added directly to the u16 angle, NOT shifted eight places as in sprite.c.
    public SourceAffineMatrix Matrix=>Ended?new(256,0,0,256):SourceAffineMatrix.FromScaleRotation(XScale,YScale,Rotation);
    public SourceTaskAffineProgram(SourceTaskAffineCommand[] commands)
    {Validate(commands);_commands=commands;}
    public static void Validate(SourceTaskAffineCommand[] commands)
    {
        if(commands.Length is <1 or >256)throw new InvalidDataException("Unbounded task affine program.");
        for(int i=0;i<commands.Length;i++)
        {
            var c=commands[i];
            if(c.Op is not ("frame" or "jump" or "loop" or "end")||c.X is < -32768 or >32764||c.Y is < -32768 or >32767||c.Rotation is <0 or >255||c.Duration is <0 or >255||c.Count is <0 or >32767||c.Op=="jump"&&(c.Target<0||c.Target>=commands.Length))throw new InvalidDataException("Invalid task affine command.");
            // The source consumes the next union as a frame after an absolute
            // frame. A trailing zero-duration command would read beyond its data.
            if(c.Op=="frame"&&c.Duration==0&&i==commands.Length-1)throw new InvalidDataException("Trailing absolute task affine frame.");
        }
        if(commands[^1].Op is not ("end" or "jump"))throw new InvalidDataException("Unterminated task affine program.");
    }
    public static IReadOnlyDictionary<string,SourceTaskAffineCommand[]> Read(JsonElement programs)
    {
        var result=new Dictionary<string,SourceTaskAffineCommand[]>();
        foreach(var p in programs.EnumerateObject())
        {
            var commands=p.Value.EnumerateArray().Select(c=>new SourceTaskAffineCommand(c.GetProperty("op").GetString()!,c.GetProperty("x").GetInt32(),c.GetProperty("y").GetInt32(),c.GetProperty("rotation").GetInt32(),c.GetProperty("duration").GetInt32(),c.GetProperty("target").GetInt32(),c.GetProperty("count").GetInt32())).ToArray();
            Validate(commands);result.Add(p.Name,commands);
        }
        return result;
    }
    public void Prepare()
    {CommandIndex=FrameTicks=LoopRemaining=Rotation=0;XScale=YScale=256;Ended=AppliedFrame=false;}
    public bool Run()
    {
        AppliedFrame=false;if(Ended)return false;
        if(CommandIndex>=_commands.Length)throw new InvalidDataException("Task affine program fell through.");
        var c=_commands[CommandIndex];
        switch(c.Op)
        {
            case "jump":CommandIndex=c.Target;break;
            case "loop":
                if(c.Count!=0)
                {
                    if(LoopRemaining!=0){if(--LoopRemaining==0){CommandIndex++;break;}}
                    else LoopRemaining=c.Count;
                    while(CommandIndex>0)
                    {
                        CommandIndex--;
                        if(_commands[CommandIndex].Op=="loop"){CommandIndex++;break;}
                    }
                }
                else CommandIndex++;
                break;
            case "end":Ended=true;return false;
            default:
                if(c.Duration==0)
                {XScale=c.X;YScale=c.Y;Rotation=c.Rotation;c=_commands[++CommandIndex];}
                // This fallthrough intentionally handles the next union once,
                // including a second absolute frame, without re-dispatching it.
                int x=c.Op switch{"loop"=>0x7ffd,"jump"=>0x7ffe,"end"=>0x7fff,_=>c.X};
                int y=c.Op switch{"loop"=>c.Count,"jump"=>c.Target,"end"=>0,_=>c.Y};
                XScale=(short)(XScale+x);YScale=(short)(YScale+y);Rotation=(short)(Rotation+c.Rotation);AppliedFrame=true;
                if(++FrameTicks>=c.Duration){FrameTicks=0;CommandIndex++;}
                break;
        }
        return true;
    }
    public static int FootOffset(int matrixD,int battlerYOffset)
    {
        int height=64-battlerYOffset*2;
        int scaled=matrixD==0?0:(height<<8)/matrixD;
        return (height-Math.Min(128,scaled))/2;
    }
}

public sealed partial class SourceBattleAnimation
{
    private static string? TaskAffineProgramName(string callback)=>callback switch
    {
        "AnimTask_MeditateStretchAttacker"=>"sAffineAnim_MeditateStretchAttacker",
        "AnimTask_Splash"=>"sSplashEffectAffineAnimCmds",
        "AnimTask_StockpileDeformMon"=>"sStockpileDeformMonAffineAnimCmds",
        "AnimTask_SwallowDeformMon"=>"sSwallowDeformMonAffineAnimCmds",
        "AnimTask_GrowAndShrink"=>"sGrowAndShrinkAffineAnimCmds",
        "AnimTask_DefenseCurlDeformMon"=>"DefenseCurlDeformMonAffineAnimCmds",
        "AnimTask_SlackOffSquish"=>"sSlackOffSquishAffineAnimCmds",
        "AnimTask_StretchTargetUp"=>"sAffineAnims_StretchBattlerUp",
        _=>null
    };
    private bool ValidateTaskAffineOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="createvisualtask"&&TaskAffineProgramName(a.FirstOrDefault()??"") is {} programName)
        {
            int count=a[0]=="AnimTask_Splash"?4:a[0]=="AnimTask_SlackOffSquish"?3:2;
            if(a.Length!=count||Number(a[1]) is <0 or >255||(count>=3&&Number(a[2]) is <0 or >3)||(count==4&&Number(a[3]) is <0 or >32767))Fail(c,"Task affine operands differ.");
            if(!_script.TaskAffinePrograms.TryGetValue(programName,out var program))Fail(c,"Missing generated task affine program.");
            SourceTaskAffineProgram.Validate(program!);
            if(a[0]=="AnimTask_StockpileDeformMon"&&!program!.SequenceEqual([new SourceTaskAffineCommand("frame",8,-8,0,12),new("frame",-16,16,0,12),new("frame",8,-8,0,12),new("loop",Count:1),new("end")]))Fail(c,"Stockpile source affine program differs.");
            if(a[0]=="AnimTask_SwallowDeformMon"&&!program!.SequenceEqual([new SourceTaskAffineCommand("frame",0,6,0,20),new("frame",0,0,0,20),new("frame",7,-30,0,6),new("frame",0,0,0,20),new("frame",-2,3,0,20),new("end")]))Fail(c,"Swallow source affine program differs.");
            if(a[0]=="AnimTask_StretchTargetUp"&&!program!.SequenceEqual([new SourceTaskAffineCommand("frame",10,-13,0,10),new SourceTaskAffineCommand("frame",-10,13,0,10),new SourceTaskAffineCommand("end")]))Fail(c,"Target stretch source affine program differs.");
            return true;
        }
        if(c.Op=="createsprite"&&a.FirstOrDefault()=="gEclipsingOrbSpriteTemplate")
        {
            if(a.Length!=7||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[3]) is < -64 or >64||Number(a[4]) is < -64 or >64||Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1)Fail(c,"Eclipsing orb operands differ.");
            if(!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!="AnimSpriteOnMonPos"||recipe.Asset!="eclipsing-orb"||recipe.Width!=32||recipe.Height!=32||recipe.Blend||recipe.Animations.Length!=1||recipe.AffineAnimations.Length!=0)Fail(c,"Eclipsing orb recipe differs.");
            return true;
        }
        if(c.Op=="createsprite"&&a.FirstOrDefault()=="gBreathPuffSpriteTemplate")
        {
            if(a.Length!=3||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Breath puff operands differ.");
            if(!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!="AnimBreathPuff"||recipe.Asset!="breath"||recipe.Width!=16||recipe.Height!=16||recipe.Blend||recipe.Animations.Length!=2||recipe.AffineAnimations.Length!=0)Fail(c,"Breath puff recipe differs.");
            return true;
        }
        if(c.Op=="createsprite"&&a.FirstOrDefault()=="gMudsportMudSpriteTemplate")
        {
            if(a.Length!=6||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[3]) is <0 or >1||Number(a[4]) is < -32768 or >32767||Number(a[5]) is < -32768 or >32767)Fail(c,"Mud Sport particle operands differ.");
            if(!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!="AnimMudSportDirt"||recipe.Asset!="mud-sand"||recipe.Width!=16||recipe.Height!=16||recipe.Blend||recipe.AffineAnimations.Length!=0)Fail(c,"Mud Sport particle recipe differs.");
            return true;
        }
        if(c.Op=="loadspritegfx"&&a.Length==1&&a[0] is "ANIM_TAG_MUD_SAND" or "ANIM_TAG_BREATH" or "ANIM_TAG_ECLIPSING_ORB")return true;
        if(c.Op is "playsewithpan" or "waitplaysewithpan" or "loopsewithpan"&&a.FirstOrDefault() is "SE_M_DIG" or "SE_M_TAIL_WHIP" or "SE_M_BUBBLE2" or "SE_M_YAWN" or "SE_M_TRI_ATTACK")
        {
            if(a.Length!=(c.Op=="waitplaysewithpan"?3:c.Op=="loopsewithpan"?4:2)||SoundPan(a[1]) is < -64 or >63)Fail(c,"Task affine sound operands differ.");
            if(c.Op=="waitplaysewithpan"&&Number(a[2]) is <0 or >255)Fail(c,"Task affine sound delay differs.");
            if(c.Op=="loopsewithpan"&&(Number(a[2]) is <0 or >255||Number(a[3]) is <2 or >255))Fail(c,"Task affine sound loop differs.");
            return true;
        }
        return false;
    }
    private bool TryCreateTaskAffineTask(SourceAnimationCommand c)
    {
        if(TaskAffineProgramName(c.Args[0]) is not {} programName)return false;
        bool splash=c.Args[0]=="AnimTask_Splash";
        if(splash&&Number(c.Args[3])==0)return true;
        int participant=c.Args[0]=="AnimTask_StretchTargetUp"?1:c.Args[0] is "AnimTask_GrowAndShrink" or "AnimTask_DefenseCurlDeformMon" or "AnimTask_MeditateStretchAttacker" or "AnimTask_StockpileDeformMon" or "AnimTask_SwallowDeformMon"?0:Number(c.Args[2]);
        if(ResolveShakeParticipant(participant,false) is not {} slot)return true;
        var commands=_script.TaskAffinePrograms[programName];
        _tasks.Add(c.Args[0] switch
        {
            "AnimTask_StretchTargetUp"=>new StretchTargetTask(this,slot,commands),
            "AnimTask_Splash"=>new SplashTask(this,slot,Number(c.Args[3]),commands),
            "AnimTask_SlackOffSquish"=>new SlackOffTask(this,slot,commands),
            _=>new TaskAffinePlayback(this,slot,commands)
        });
        return true;
    }
    private bool TryCreateTaskAffineSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]=="gBreathPuffSpriteTemplate")
        {
            if(!_loaded.Contains("ANIM_TAG_BREATH"))Fail(c,"Breath graphics have not been loaded.");
            _sprites.Add(new AnimatedSprite(slot,priority,_script.SpriteRecipes[c.Args[0]],[],this));return true;
        }
        if(c.Args[0]!="gMudsportMudSpriteTemplate")return false;
        if(!_loaded.Contains("ANIM_TAG_MUD_SAND"))Fail(c,"Mud Sport graphics have not been loaded.");
        _sprites.Add(new MudSportDirt(slot,priority,c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private void PrepareTaskAffine(int slot,SourceTaskAffineProgram program)
    {program.Prepare();PrepareBattlerRotScale(slot,0);}
    private bool RunTaskAffine(int slot,SourceTaskAffineProgram program)
    {
        if(!program.Run())
        {_offsets[slot]=new(_offsets[slot].X,0);SetBattlerRotScale(slot,256,256,0);ResetBattlerRotScale(slot);return false;}
        if(program.AppliedFrame)
        {
            SetBattlerRotScale(slot,program.XScale,program.YScale,program.Rotation);
            _offsets[slot]=new(_offsets[slot].X,SourceTaskAffineProgram.FootOffset(_yScale[slot],_battlers[slot].YOffset));
        }
        return true;
    }
    private class TaskAffinePlayback:VisualTask
    {
        protected readonly int Battler;
        private readonly SourceTaskAffineProgram _program;
        public TaskAffinePlayback(SourceBattleAnimation owner,AnimationSlot slot,SourceTaskAffineCommand[] commands)
        {Battler=slot.Value;_program=new(commands);owner.PrepareTaskAffine(Battler,_program);}
        public override void Step(SourceBattleAnimation owner)
        {if(!owner.RunTaskAffine(Battler,_program))Finished=true;}
    }
    private sealed class StretchTargetTask:VisualTask
    {
        private readonly int _slot;private readonly SourceTaskAffineProgram _program;
        public StretchTargetTask(SourceBattleAnimation o,AnimationSlot slot,SourceTaskAffineCommand[] commands)
        {_slot=slot.Value;_program=new(commands);o.PrepareTaskAffine(_slot,_program);o._offsets[_slot]=new(4,o._offsets[_slot].Y);}
        public override void Step(SourceBattleAnimation o)
        {
            o._offsets[_slot]=new(-o._offsets[_slot].X,o._offsets[_slot].Y);
            if(!o.RunTaskAffine(_slot,_program)){o._offsets[_slot]=default;Finished=true;}
        }
    }
    private sealed class SlackOffTask(SourceBattleAnimation owner,AnimationSlot slot,SourceTaskAffineCommand[] commands):TaskAffinePlayback(owner,slot,commands)
    {
        private int _ticks,_shakeDelay,_shakes;
        public override void Step(SourceBattleAnimation owner)
        {
            _ticks++;
            if(_ticks>16&&_ticks<40)
            {
                if(++_shakeDelay>2){_shakeDelay=0;_shakes++;owner._offsets[Battler]=new((_shakes&1)==0?-1:1,owner._offsets[Battler].Y);}
            }
            else owner._offsets[Battler]=new(0,owner._offsets[Battler].Y);
            base.Step(owner);
        }
    }
    private sealed class SplashTask:VisualTask
    {
        private readonly int _slot;
        private readonly SourceTaskAffineProgram _affine;
        private int _hops,_state,_ticks,_height;
        public SplashTask(SourceBattleAnimation owner,AnimationSlot slot,int hops,SourceTaskAffineCommand[] commands)
        {_slot=slot.Value;_hops=hops;_affine=new(commands);owner.PrepareTaskAffine(_slot,_affine);}
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_state)
            {
                case 0:
                    owner.RunTaskAffine(_slot,_affine);_height+=3;
                    owner._offsets[_slot]=new(owner._offsets[_slot].X,owner._offsets[_slot].Y+_height);
                    if(++_ticks>7){_ticks=0;_state++;}break;
                case 1:
                    owner.RunTaskAffine(_slot,_affine);
                    owner._offsets[_slot]=new(owner._offsets[_slot].X,owner._offsets[_slot].Y+_height);
                    if(++_ticks>7){_ticks=0;_state++;}break;
                case 2:
                    if(_height!=0){owner._offsets[_slot]=new(owner._offsets[_slot].X,owner._offsets[_slot].Y-2);_height-=2;}
                    else _state++;
                    break;
                case 3:
                    if(!owner.RunTaskAffine(_slot,_affine))
                    {if(--_hops==0)Finished=true;else{owner.PrepareTaskAffine(_slot,_affine);_state=0;}}
                    break;
            }
        }
    }
    private sealed class MudSportDirt:VisualSprite
    {
        private PixelPoint _position,_offset;
        private readonly bool _rising;
        private readonly int _direction;
        private int _ticks,_state;
        private bool _hidden;
        public MudSportDirt(int slot,int priority,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {
            _rising=a[0]==0;
            if(_rising)
            {
                var center=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);
                _position=new((short)(center.X+a[1]),(short)(center.Y+a[2]));_direction=a[1]>0?1:-1;
            }
            else{_position=new(a[1],a[2]);_offset=new(0,(short)-a[2]);}
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gMudsportMudSpriteTemplate",_position,_offset,1,!_hidden,false,false,256,"mud-sand",Subpriority){Width=16,Height=16};
        public override void Step(SourceBattleAnimation owner)
        {
            if(_rising)
            {
                if(++_ticks>1){_ticks=0;_position=new((short)(_position.X+_direction),_position.Y);}
                _position=new(_position.X,(short)(_position.Y-4));if(_position.Y< -4)Finished=true;
            }
            else if(_state==0)
            {
                _offset=new(0,(short)(_offset.Y+4));
                if(_offset.Y>=0){_offset=default;_state=1;}
            }
            else{_hidden=!_hidden;if(++_ticks==10)Finished=true;}
        }
    }
}
