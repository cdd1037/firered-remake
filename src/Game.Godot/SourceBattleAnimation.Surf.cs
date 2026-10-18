namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateSurfCommand(SourceAnimationCommand command)
    {
        if(command.Op!="createvisualtask"||command.Args.FirstOrDefault()!="AnimTask_CreateSurfWave")return false;
        var a=command.Args;
        if(a.Length!=3||Number(a[1]) is <0 or >255||AnimationArgument(a[2]) is <short.MinValue or >short.MaxValue)Fail(command,"Surf task source operand widths differ.");
        string family=AnimationArgument(a[2])==0?"surf":"muddy-water";
        foreach(string side in new[]{"player","opponent"})if(!_script.OverlayDependencies.Contains(family+"-"+side)||!_script.OverlayPalettes.TryGetValue(family+"-"+side,out var colors)||colors.Length!=16)Fail(command,"Surf source indexed overlay is missing.");
        if(!_validatingInvokedProgram)return true;
        var closure=OverlayInvocationCommands();
        if(closure.Count(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_CreateSurfWave")!=1||closure.Any(c=>c.Op is not("createvisualtask" or "delay" or "panse" or "waitforvisualfinish" or "end")||c.Op=="createvisualtask"&&c.Args[0]!="AnimTask_CreateSurfWave"))Fail(command,"Surf requires one bounded scanline, palette and BG1 owner.");
        // Include the source script controller, raw child, visual parent, live
        // visual/audio tasks, and authored pan sound task in NUM_TASKS capacity.
        if(3+_tasks.Count(t=>!t.Finished)+_soundTasks.Count(t=>!t.Finished)+closure.Count(c=>c.Op=="panse")>16)Fail(command,"Surf raw and counted tasks exceed source capacity.");
        return true;
    }
    private bool TryCreateSurfTask(SourceAnimationCommand command)
    {
        if(command.Args[0]!="AnimTask_CreateSurfWave")return false;
        var child=new SurfScanlineTask((_cue.Actor.Value&1)!=0){Priority=unchecked((byte)(Number(command.Args[1])+1))};
        _tasks.Add(new SurfWaveTask(this,child,_animationArgs[0]!=0));_tasks.Add(child);return true;
    }
    // battle_anim_water.c:800–943. The parent owns the indexed BG1 material,
    // faded palette rotation and alpha envelope; its raw child owns HBlank DMA.
    private sealed class SurfWaveTask:VisualTask
    {
        private readonly SurfScanlineTask _child;private readonly string _material;private readonly int _dx,_dy;
        private int _phase,_paletteTick,_envelopeTick=1,_envelopeAge,_coefficient;
        public SurfWaveTask(SourceBattleAnimation owner,SurfScanlineTask child,bool muddy)
        {
            _child=child;bool opponent=(owner._cue.Actor.Value&1)!=0;_dx=opponent?2:-2;_dy=opponent?-1:1;
            _material=(muddy?"muddy-water":"surf")+(opponent?"-opponent":"-player");
            owner.SetAlphaBlend(2,63,0,16);owner.SetBackgroundPriority(1,1);owner.BeginOverlay(_material);
            owner.SetOverlayScroll(opponent?-224:0,opponent?256:-48);owner.QueueBackgroundRegisters(1);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_phase==0)
            {
                var p=owner.ScanlineBackgroundOffset(1);owner.SetOverlayScroll(p.X+_dx,p.Y+_dy);
                if(++_paletteTick==4)
                {
                    Span<ushort> colors=stackalloc ushort[7];var original=owner._script.OverlayPalettes[_material];
                    for(int i=0;i<7;i++){int source=i==0?7:i;colors[i]=owner._fadedBackgroundEntries.GetValueOrDefault(128+source,original[source]);}
                    owner.WriteBackgroundPaletteRange(8,1,colors);_paletteTick=0;
                }
                if(++_envelopeTick>1)
                {
                    _envelopeTick=0;++_envelopeAge;
                    if(_envelopeAge<14){_child.Coefficient=unchecked((ushort)(_envelopeAge|((16-_envelopeAge)<<8)));++_coefficient;}
                    if(_envelopeAge>54){--_coefficient;_child.Coefficient=unchecked((ushort)(_coefficient|((16-_coefficient)<<8)));}
                }
                if((_child.Coefficient&31)==0)_phase=1;
            }
            else if(_phase==1){owner.ClearBackgroundTilemaps();_phase=2;}
            else
            {
                owner.SetScanlineBackgroundOffset(1,default);owner.ClearAlphaBlend();owner._blendSecondTargets=0;_child.StopRequested=true;Finished=true;
            }
        }
    }
    // Source CreateTask is a real uncounted task and can run before the parent
    // when its u8 priority wraps 255→0. Keep it in the shared task scheduler.
    private sealed class SurfScanlineTask(bool opponent):VisualTask
    {
        private int _phase,_top=opponent?0:48,_bottom=opponent?0:112;
        public ushort Coefficient{get;set;}=0x1000;
        public bool StopRequested{get;set;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_phase==0)
            {
                Fill(owner.ScanlineBuffers[0]);Fill(owner.ScanlineBuffers[1]);
                owner.ScanlineBuffers[0][160]=owner.ScanlineBuffers[1][160]=_top==0?Coefficient:(ushort)0x1000;
                owner.StartAlphaScanline();_phase=1;return;
            }
            if(_phase==1)
            {
                if(!opponent){if(--_top<=0){_top=0;++_phase;}}
                else if(++_bottom>111)++_phase;
                Fill(owner.ScanlineBuffers[owner.ScanlineWriteBuffer]);return;
            }
            Fill(owner.ScanlineBuffers[owner.ScanlineWriteBuffer]);
            if(StopRequested){owner.ScanlineState.Stop();Finished=true;}
        }
        private void Fill(ushort[] bank)
        {for(int row=0;row<160;row++)bank[row]=row>=_top&&row<_bottom?Coefficient:(ushort)0x1000;}
    }
}
