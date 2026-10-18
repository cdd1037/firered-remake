namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private static bool MementoTaskName(string name)=>name is "AnimTask_InitMementoShadow" or "AnimTask_MoveAttackerMementoShadow" or "AnimTask_MementoHandleBg" or "AnimTask_MoveTargetMementoShadow";
    private bool ValidateMementoCommand(SourceAnimationCommand c)
    {
        if(c.Op!="createvisualtask"||!MementoTaskName(c.Args.FirstOrDefault()??""))return false;
        string name=c.Args[0];
        if(c.Args.Length!=2||Number(c.Args[1])!=(name is "AnimTask_InitMementoShadow" or "AnimTask_MementoHandleBg"?2:5))
            Fail(c,"Memento task arguments differ from the source callbacks.");
        if(!_validatingInvokedProgram)return true;
        var commands=OverlayInvocationCommands();
        string[] order=["AnimTask_InitMementoShadow","AnimTask_MoveAttackerMementoShadow","AnimTask_MementoHandleBg","AnimTask_MoveTargetMementoShadow"];
        if(!commands.Where(x=>x.Op=="createvisualtask").Select(x=>x.Args[0]).SequenceEqual(order)||
            commands.Any(x=>x.Op is not ("createvisualtask" or "delay" or "setalpha" or "playsewithpan" or "waitforvisualfinish" or "monbg_static" or "clearmonbg_static" or "blendoff" or "end"))||
            commands.Count(x=>x.Op=="monbg_static")!=1||commands.Count(x=>x.Op=="clearmonbg_static")!=1||
            commands.Any(x=>x.Op is "monbg_static" or "clearmonbg_static"&&x.Args is not ["ANIM_TARGET"]))
            Fail(c,"Memento requires its ordered four callbacks and exclusive static-BG/window/scanline ownership.");
        foreach(int slot in new[]{_cue.Actor.Value,_cue.Target.Value})
        {
            var b=_battlers[slot];int y=ScanlineBattlerY(slot),top=b.Center(_doubles,true).Y-b.SourceHeight/2-7;
            if(!(slot==_cue.Actor.Value?IsSourceBattlerVisible(slot):IsSourceBattlerPresent(slot))||b.SourceHeight is <8 or >64||(b.SourceHeight&7)!=0||b.Base(_doubles).X is <32 or >208||y+31>=160||top>=y+31||y+31-top>=128)
                Fail(c,"Memento requires a visible actor and present target with bounded exact picture/top-edge geometry.");
        }
        return true;
    }
    private bool TryCreateMementoTask(SourceAnimationCommand c)
    {
        switch(c.Args[0])
        {
            case "AnimTask_InitMementoShadow":
                MoveBattlerToStaticBackground(_cue.Actor.Value);
                if(IsSourceBattlerVisible(_cue.Actor.Partner.Value))MoveBattlerToStaticBackground(_cue.Actor.Partner.Value);
                return true;
            case "AnimTask_MementoHandleBg":
                ClearStaticBattlerBackgroundRank(_battlers[_cue.Actor.Value].BackgroundRank);
                if(IsSourceBattlerVisible(_cue.Actor.Partner.Value))ClearStaticBattlerBackgroundRank(_battlers[_cue.Actor.Partner.Value].BackgroundRank);
                return true;
            case "AnimTask_MoveAttackerMementoShadow":_tasks.Add(new MementoShadowTask(this,false));return true;
            case "AnimTask_MoveTargetMementoShadow":var task=new MementoShadowTask(this,true);task.Step(this);if(!task.Finished)_tasks.Add(task);return true;
            default:return false;
        }
    }
    private sealed class MementoShadowTask:VisualTask
    {
        private readonly bool _target;
        private bool _setup;
        private readonly short[] _d=new short[16];
        private static short S(int value)=>unchecked((short)value);
        public MementoShadowTask(SourceBattleAnimation o,bool target)
        {
            _target=target;_setup=target;if(target)return;
            Geometry(o,o._cue.Actor.Value);_d[4]=_d[6];_d[5]=_d[7];_d[14]-=32;_d[15]+=32;
            _d[3]=S(o._battlers[o._cue.Actor.Value].BackgroundRank);_d[10]=S(o.ScanlineBackgroundOffset(_d[3]).Y);
            o.SetAlphaBlend(1<<_d[3],63,o._alphaA,o._alphaB);o.FillStaticBattlerBackgroundPalette(_d[3],0);HideOtherPlane(o);
            _d[12]=16;SetPriorities(o);
            for(int i=0;i<112;i++)Both(o,i,_d[10]);
            o.StartVerticalScanline(_d[3]);Window(o);
        }
        private void Geometry(SourceBattleAnimation o,int slot)
        {
            var b=o._battlers[slot];_d[7]=S(o.ScanlineBattlerY(slot)+31);_d[6]=S(b.Center(o._doubles,true).Y-b.SourceHeight/2-7);
            _d[13]=S((_d[7]-_d[6])<<8);_d[14]=_d[15]=S(b.Base(o._doubles).X);_d[8]=S(b.Slot.IsPlayer?-12:-64);
        }
        private static void Both(SourceBattleAnimation o,int i,int value)=>o.ScanlineBuffers[0][i]=o.ScanlineBuffers[1][i]=unchecked((ushort)value);
        private void HideOtherPlane(SourceBattleAnimation o)
        {int rank=_d[3]^3;var p=o.ScanlineBackgroundOffset(rank);o.SetScanlineBackgroundOffset(rank,new(p.X+240,p.Y));}
        private static void SetPriorities(SourceBattleAnimation o)
        {for(int participant=0;participant<4;participant++)if(o.SourceAnimBattlerSlot(participant) is {} slot)o.SetBattlerObjectPriority(slot,3);}
        private void Window(SourceBattleAnimation o)=>o._window0=new(true,unchecked((ushort)(((ushort)_d[14]<<8)|(ushort)_d[15])),160,63,63^(1<<_d[3]));
        public override void Step(SourceBattleAnimation o)
        {
            if(_setup){SetupTarget(o);return;}
            if(_target)Target(o);else Attacker(o);
        }
        private void SetupTarget(SourceBattleAnimation o)
        {
            switch(_d[0]++)
            {
                case 0:_d[3]=S(o._battlers[o._cue.Target.Value].BackgroundRank);o.SetAlphaBlend(1<<_d[3],63,o._alphaA,o._alphaB);HideOtherPlane(o);break;
                case 1:_d[10]=S(o.ScanlineBackgroundOffset(_d[3]).Y);o.FillStaticBattlerBackgroundPalette(_d[3],0);SetPriorities(o);break;
                case 2:Geometry(o,o._cue.Target.Value);_d[14]-=4;_d[15]+=4;_d[4]=_d[5]=_d[8];_d[11]=12;_d[12]=8;break;
                case 3:for(int i=0;i<112;i++)Both(o,i,_d[10]+159-i);o.StartVerticalScanline(_d[3]);break;
                case 4:Window(o);_d[0]=_d[1]=_d[2]=0;o.SetAlphaBlend(1<<_d[3],63,12,8);_setup=false;break;
            }
        }
        private void Attacker(SourceBattleAnimation o)
        {
            switch(_d[0])
            {
                case 0:
                    if(++_d[1]>1){_d[1]=0;if((++_d[2]&1)!=0){if(_d[11]!=12)_d[11]++;}else if(_d[12]!=8)_d[12]--;
                        o.SetAlphaBlend(1<<_d[3],63,_d[11],_d[12]);if(_d[11]==12&&_d[12]==8)_d[0]++;}break;
                case 1:_d[4]-=8;Distort(o);if(_d[4]<_d[8])_d[0]++;break;
                case 2:_d[4]-=8;Distort(o);_d[14]+=4;_d[15]-=4;if(_d[14]>=_d[15])_d[14]=_d[15];Window(o);if(_d[14]==_d[15])_d[0]++;break;
                case 3:o.StopScanlineDeferred();_d[0]++;break;
                case 4:Finished=true;break;
            }
        }
        private void Target(SourceBattleAnimation o)
        {
            switch(_d[0])
            {
                case 0:_d[5]+=8;if(_d[5]>=_d[7])_d[5]=_d[7];Distort(o);if(_d[5]==_d[7])_d[0]++;break;
                case 1:
                    if(_d[15]-_d[14]<64){_d[14]-=4;_d[15]+=4;}else _d[1]=1;Window(o);
                    _d[4]+=8;if(_d[4]>=_d[6])_d[4]=_d[6];Distort(o);if(_d[4]==_d[6]&&_d[1]!=0){_d[1]=0;_d[0]++;}break;
                case 2:
                    if(++_d[1]>1){_d[1]=0;if((++_d[2]&1)!=0){if(_d[11]!=0)_d[11]--;}else if(_d[12]<16)_d[12]++;
                        o.SetAlphaBlend(1<<_d[3],63,_d[11],_d[12]);if(_d[11]==0&&_d[12]==16)_d[0]++;}break;
                case 3:o.StopScanlineDeferred();_d[0]++;break;
                case 4:o._window0=new(true,0,0,63,63);Finished=true;break;
            }
        }
        private void Distort(SourceBattleAnimation o)
        {
            // Source signed division truncates toward zero. Negative rows still
            // advance the fixed-point numerator, but do not address either bank.
            int height=S(_d[5]-_d[4]);
            if(height==0){for(int i=0;i<112;i++)Both(o,i,_d[10]+159-i);return;}
            int step=_d[13]/height,value=_d[6]<<8,row=0;var bank=o.ScanlineBuffers[o.ScanlineWriteBuffer];
            for(;row<_d[4];row++)bank[row]=unchecked((ushort)(_d[10]-(row-159)));
            for(row=_d[4];row<=_d[5];row++)
            {if(row>=0)bank[row]=unchecked((ushort)(S((value>>8)-row)+_d[10]));value+=step;}
            int tail=_d[10]-(row-159);for(;row<_d[7];row++)if(row>=0)bank[row]=unchecked((ushort)tail--);
        }
    }
}
