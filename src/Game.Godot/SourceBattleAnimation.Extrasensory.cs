namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string ExtrasensoryDistortion="AnimTask_ExtrasensoryDistortion",TransparentClone="AnimTask_TransparentCloneGrowAndShrink";
    private bool ValidateExtrasensoryCommand(SourceAnimationCommand c)
    {
        var a=c.Args;if(c.Op!="createvisualtask"||a.FirstOrDefault() is not (ExtrasensoryDistortion or TransparentClone))return false;
        if(a.Length!=3||Number(a[1]) is <0 or >255||Number(a[2])<0||Number(a[2])>(a[0]==ExtrasensoryDistortion?2:3))Fail(c,"Extrasensory stage or clone selector differs from source.");
        if(_script.SineTable.Length!=320)Fail(c,"Extrasensory needs the source sine table.");
        if(!_validatingInvokedProgram)return true;
        foreach(var b in _battlers.Values)ValidateReactionGeometry(c,b);
        var closure=OverlayInvocationCommands();
        if(closure.Any(x=>x.Op is "createsprite" or "loadspritegfx" or "visible" or "invisible" or "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"||x.Op=="createvisualtask"&&x.Args[0] is not (ExtrasensoryDistortion or TransparentClone or "AnimTask_BlendMonInAndOut" or "AnimTask_SetPsychicBackground")))Fail(c,"Extrasensory requires its reviewed linear scanline, clone and palette companions.");
        // A source stop request is consumed at the following VBlank. An explicit
        // visual wait keeps each new scanline owner after its predecessor's exit.
        bool scanline=false;int clones=0;
        foreach(var command in closure)
        {
            if(command.Op=="waitforvisualfinish")scanline=false;
            if(command.Op!="createvisualtask")continue;
            if(command.Args[0]==ExtrasensoryDistortion){if(scanline)Fail(c,"Overlapping Extrasensory scanline owners.");scanline=true;}
            if(command.Args[0]==TransparentClone&&++clones>1)Fail(c,"Multiple raw affine clone owners exceed reviewed closure.");
        }
        int births=MaximumInvocationCost(x=>x.Op=="createvisualtask"&&x.Args[0]==TransparentClone?1:0);
        int tasks=1+MaximumInvocationCost(x=>x.Op is "createvisualtask" or "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "fadetobg" or "restorebg" or "clearmonbg"?1:x.Op=="monbg"?(_doubles?2:1):0);
        if(22+births+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64||4+births+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Extrasensory closure exceeds native object, matrix or task capacity.");
        return true;
    }
    private bool TryCreateExtrasensoryTask(SourceAnimationCommand c)
    {
        VisualTask task;if(c.Args[0]==ExtrasensoryDistortion)task=new ExtrasensoryDistortionTask(this);else if(c.Args[0]==TransparentClone)task=new TransparentCloneTask(this);else return false;
        if(!task.Finished)_tasks.Add(task);return true;
    }
    private sealed class ExtrasensoryDistortionTask:VisualTask
    {
        private readonly short[] _d=new short[16];
        public ExtrasensoryDistortionTask(SourceBattleAnimation o)
        {
            int slot=o._cue.Target.Value,y=o.ScanlineBattlerY(slot),mode=o._animationArgs[0],rank=o._battlers[slot].BackgroundRank;
            _d[14]=(short)Math.Max(0,y-32);_d[11]=(short)(mode==2?4:2);_d[12]=(short)(mode==2?4:5);_d[13]=(short)(mode==0?64:mode==1?192:0);_d[15]=(short)(y+32);_d[10]=unchecked((short)o.ScanlineBackgroundOffset(rank).X);
            for(int i=_d[14];i<=_d[14]+64;i++)Both(o,i,_d[10]);o.StartScanline(rank);
        }
        private static void Both(SourceBattleAnimation o,int row,int value)=>o.ScanlineBuffers[0][row]=o.ScanlineBuffers[1][row]=unchecked((ushort)value);
        public override void Step(SourceBattleAnimation o)
        {
            switch(_d[0])
            {
                case 0:
                    int angle=_d[13];for(int i=_d[14];i<=_d[15];i++){int displacement=o.SampleSourceSine(angle)>>_d[12];if(displacement>0)displacement+=_d[1]&3;else if(displacement<0)displacement-=_d[1]&3;Both(o,i,_d[10]+displacement);angle+=_d[11];}
                    if(++_d[1]>23)_d[0]++;break;
                case 1:o.StopScanlineDeferred();_d[0]++;break;
                case 2:Finished=true;break;
            }
        }
    }
    private sealed class TransparentCloneTask:VisualTask
    {
        private readonly TransparentBattlerClone? _copy;private int _phase,_angle,_scale;
        public TransparentCloneTask(SourceBattleAnimation o)
        {
            // The admitted complete path reserves a matrix. Keep the native
            // initializer's no-allocation exit for exhausted retained occupancy.
            if(4+o._spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>=32){Finished=true;return;}
            int? selected=o.SourceAnimBattlerSlot(o._animationArgs[0]);int slot=o.FindFreeSpriteSlot();
            if(selected is not int source||slot==64){Finished=true;return;}
            _copy=new TransparentBattlerClone(slot,o,source);o._sprites.Add(_copy);
        }
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:case 1:
                    _angle+=_phase==0?4:-4;_scale=256-(o.SampleSourceSine(_angle)>>1);_copy!.SetScale(_scale);if(_angle==(_phase==0?48:0))_phase++;break;
                case 2:_copy!.Destroy();o._sprites.Remove(_copy);_phase++;break;
                case 3:Finished=true;break;
            }
        }
    }
    private sealed class TransparentBattlerClone:VisualSprite
    {
        private readonly AnimationBattler _body;private readonly SourcePixelAsset? _material;private readonly PixelPoint _position;private readonly int _priority;private PixelPoint _offset;private SourceAffineMatrix _matrix=new(256,0,0,256);
        public TransparentBattlerClone(int slot,SourceBattleAnimation o,int source):base(slot,unchecked((byte)(o._battlers[source].Subpriority+1)))
        {_body=o._battlers[source];_position=o.BattlerPosition(source);_offset=o._offsets[source];_priority=o._battlerObjectPriorities[source];o._spriteMaterials?.TryGetValue(_body.Asset,out _material);o._oamY[slot]=o._oamY[source];}
        public override bool Counted=>false;
        public void SetScale(int scale){_matrix=SourceAffineMatrix.FromScaleRotation(scale,scale,0);int height=64-_body.YOffset*2,scaled=_matrix.D==0?0:(height<<8)/_matrix.D;_offset=new(_offset.X,(height-Math.Min(128,scaled))/2);}
        public void Destroy()=>Finished=true;
        public override void Step(SourceBattleAnimation o){}
        public override SourceVisualSpriteFrame Frame=>new(Slot,"TransparentCloneGrowAndShrink",_position,_offset,0,true,false,true,256,_body.Asset,Subpriority){Width=64,Height=64,BattlerCopySlot=_body.Slot.Value,Material=_material,OamPriority=_priority,Affine=true,DoubleSize=true,MatrixA=_matrix.A,MatrixB=_matrix.B,MatrixC=_matrix.C,MatrixD=_matrix.D};
    }
}
