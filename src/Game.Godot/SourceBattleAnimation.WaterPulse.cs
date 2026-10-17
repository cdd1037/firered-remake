namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string PulseBubbleTemplate="gWaterPulseBubbleSpriteTemplate",PulseRingTemplate="gWaterPulseRingSpriteTemplate",PulseChildTemplate="gWaterPulseRingBubbleSpriteTemplate";
    private bool ValidateWaterPulseOperand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createsprite"||a.FirstOrDefault() is not (PulseBubbleTemplate or PulseRingTemplate))return false;
        bool ring=a[0]==PulseRingTemplate;
        if(a.Length!=(ring?7:10)||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(command,"Water Pulse recipe or operand count differs.");
        var recipe=_script.SpriteRecipes[a[0]];
        if(recipe.Callback!=(ring?"AnimWaterPulseRing":"AnimWaterPulseBubble")||recipe.Asset!=(ring?"blue-ring-2":"small-bubbles")||recipe.Width!=(ring?16:8)||recipe.Height!=(ring?32:8)||recipe.Blend||recipe.AffineDoubleSize!=ring||_script.SineTable.Length!=320)Fail(command,"Water Pulse source shape/callback differs.");
        if(a.Skip(3).Any(v=>AnimationArgument(v) is <short.MinValue or >short.MaxValue))Fail(command,"Water Pulse operands must be source signed words.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(command,"Water Pulse battler or priority differs.");
        if(ring)
        {
            // Bounded positive duration/interval prevents source division by zero
            // and preserves the reviewed authored allocation/lifetime contract.
            if(AnimationArgument(a[3]) is < -128 or >127||AnimationArgument(a[4]) is < -128 or >127||AnimationArgument(a[5])!=40||AnimationArgument(a[6])!=15)Fail(command,"Water Pulse ring operands exceed the reviewed source closure.");
            SourceSpriteAffineCommand[] program=[new("frame",5,5,0,10,0),new("frame",-10,-10,0,10,0),new("frame",10,10,0,10,0),new("frame",-10,-10,0,10,0),new("frame",10,10,0,10,0),new("frame",-10,-10,0,10,0),new("frame",10,10,0,10,0),new("end",0,0,0,0,0)];
            if(recipe.AffineAnimations.Length!=1||!recipe.AffineAnimations[0].SequenceEqual(program)||recipe.FrameLoops.Any(p=>p.Length!=0)||recipe.Animations.Length!=1||recipe.Animations[0].Length!=1||recipe.Animations[0][0]!=new SourceSpriteAnimFrame(0,1,false,false)||!recipe.LoopStarts.SequenceEqual([0]))Fail(command,"Water Pulse ring animation differs.");
            if(!_script.SpriteRecipes.ContainsKey(PulseChildTemplate))Fail(command,"Missing raw Water Pulse bubble recipe.");
            var child=_script.SpriteRecipes[PulseChildTemplate];
            if(child.Callback!="AnimWaterPulseRingBubble"||child.Asset!="small-bubbles"||child.Width!=8||child.Height!=8||child.Blend||child.AffineDoubleSize||!PulseBubbleFrames(child)||child.AffineAnimations.Length!=2)Fail(command,"Raw Water Pulse bubble shape differs.");
            for(int i=0;i<2;i++)
            {
                int scale=i==0?256:224,delta=i==0?-10:-8;
                SourceSpriteAffineCommand[] p=[new("frame",scale,scale,0,0,0),new("frame",delta,delta,0,15,0),new("end",0,0,0,0,0)];
                if(!child.AffineAnimations[i].SequenceEqual(p))Fail(command,"Raw Water Pulse affine program differs.");
            }
            RequireVisualSeed(command);
        }
        else if(!PulseBubbleFrames(recipe)||recipe.AffineAnimations.Length!=0||AnimationArgument(a[3]) is < -256 or >256||AnimationArgument(a[4]) is < -256 or >256||AnimationArgument(a[5]) is < -256 or >256||AnimationArgument(a[6]) is < -255 or >255||AnimationArgument(a[7]) is < -128 or >127||AnimationArgument(a[8]) is <1 or >255)Fail(command,"Floating Water Pulse source operands/frames differ.");
        return true;
    }
    private static bool PulseBubbleFrames(SourceSpriteRecipe recipe)=>recipe.Animations.Length==2&&recipe.LoopStarts.SequenceEqual([-1,-1])&&recipe.FrameLoops.All(p=>p.Length==0)&&Enumerable.Range(0,2).All(i=>recipe.Animations[i].Length==1&&recipe.Animations[i][0]==new SourceSpriteAnimFrame(8+i,1,false,false));
    private bool TryCreateWaterPulseSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not (PulseBubbleTemplate or PulseRingTemplate))return false;
        if(!_loaded.Contains("ANIM_TAG_SMALL_BUBBLES")||command.Args[0]==PulseRingTemplate&&!_loaded.Contains("ANIM_TAG_BLUE_RING_2"))Fail(command,"Water Pulse graphics have not been loaded.");
        _sprites.Add(new WaterPulseSprite(slot,priority,_script.SpriteRecipes[command.Args[0]],this));return true;
    }
    private sealed class WaterPulseSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly bool _ring,_raw;
        private SourceSpriteFrameProgram? _frames;private SourceSpriteAffineProgram? _affine;
        private PixelPoint _position,_offset;private readonly short[] _data=new short[8];
        public override bool Counted=>!_raw;
        // Script CreateSpriteAndAnimate initializer and Begin run immediately.
        public WaterPulseSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_ring=recipe.Template==PulseRingTemplate;var a=owner._animationArgs;
            if(_ring)
            {
                var start=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
                _position=new(unchecked((short)(start.X+owner.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
                _data[1]=(short)target.X;_data[2]=(short)target.Y;_data[3]=a[2];_data[4]=a[3];
            }
            else{_position=new(a[0],a[1]);Array.Copy(a,2,_data,0,4);}
            BeginAnimation();
        }
        // Raw CreateSprite inserts data only. Its first visited callback precedes
        // BeginAnim/BeginAffine, even when visited later in the creating pass.
        public WaterPulseSprite(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint position,int xSpeed,int ySpeed):base(slot,priority)
        {_recipe=recipe;_raw=true;_position=position;_data[0]=20;_data[1]=unchecked((short)xSpeed);_data[2]=unchecked((short)ySpeed);}
        private void BeginAnimation(){_frames=new(_recipe);if(_recipe.AffineAnimations.Length!=0)_affine=new(_recipe.AffineAnimations[0]);}
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=_frames?.Frame??_recipe.Animations[0][0];var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);
                return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority)
                {VFlip=f.VFlip,Width=_recipe.Width,Height=_recipe.Height,Affine=_ring||_raw,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {if(_frames is null)BeginAnimation();else{_frames.Step();_affine?.Step();}}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_raw)
            {
                _data[3]=unchecked((short)(_data[3]+_data[1]));_data[4]=unchecked((short)(_data[4]+_data[2]));_offset=new(_data[3]>>7,_data[4]>>7);
                if(--_data[0]==0)Finished=true;return;
            }
            if(!_ring)
            {
                _data[4]=unchecked((short)(_data[4]-_data[0]));_data[5]=(short)((_data[5]+_data[1])&255);
                _offset=new(unchecked((short)((owner._script.SineTable[_data[5]]*_data[2])>>8)),_data[4]/10);
                if(--_data[3]==0)Finished=true;return;
            }
            int xDiff=_data[1]-_position.X,yDiff=_data[2]-_position.Y;
            _offset=new(unchecked((short)(_data[0]*xDiff/_data[3])),unchecked((short)(_data[0]*yDiff/_data[3])));
            if(++_data[5]==_data[4]){_data[5]=0;EmitBubbles(owner,xDiff,yDiff);}
            if(_data[3]==_data[0])Finished=true;_data[0]++;
        }
        private void EmitBubbles(SourceBattleAnimation owner,int xDiff,int yDiff)
        {
            int spread=_data[0]/2;var center=new PixelPoint(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));
            int xSpeed=unchecked((short)(yDiff+owner.NextVisualRandom()%10-5));int perpendicular=unchecked((short)(-xDiff+owner.NextVisualRandom()%10-5));
            var recipe=owner._script.SpriteRecipes[PulseChildTemplate];int priority=owner._battlers[owner._cue.Actor.Value].Subpriority-1;
            for(int i=0;i<2;i++)
            {
                int slot=owner.FindFreeSpriteSlot();if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)continue; // Source writes only its inert sentinel on failure.
                int ySpeed=i==0?Math.Abs(perpendicular):-Math.Abs(perpendicular);
                owner._sprites.Add(new WaterPulseSprite(slot,priority,recipe,new(center.X,unchecked((short)(center.Y+(i==0?spread:-spread)))),xSpeed,ySpeed));
            }
        }
    }
}
