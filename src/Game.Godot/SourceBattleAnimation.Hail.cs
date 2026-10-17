namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string HailTemplate="sHailParticleSpriteTemplate",HailImpactTemplate="gIceCrystalHitLargeSpriteTemplate";
    private bool ValidateHailCommand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createvisualtask"||a.FirstOrDefault()!="AnimTask_Hail")return false;
        if(a.Length!=2||Number(a[1])!=5||!_script.SpriteRecipes.ContainsKey(HailTemplate)||!_script.SpriteRecipes.ContainsKey(HailImpactTemplate))Fail(command,"Hail task operands or child recipes differ.");
        var reachable=_script.Commands.Concat(_script.Subroutines.Values.SelectMany(x=>x)).ToArray();
        if(reachable.Count(c=>c.Op=="createvisualtask"&&c.Args.FirstOrDefault()=="AnimTask_Hail")!=1||reachable.Any(c=>c.Op is not ("loadspritegfx" or "createvisualtask" or "loopsewithpan" or "waitforvisualfinish" or "delay" or "end")||c.Op=="createvisualtask"&&c.Args.FirstOrDefault() is not ("AnimTask_Hail" or "AnimTask_BlendBattleAnimPal")))Fail(command,"Hail sprite/matrix capacity requires its reviewed task-only closure.");
        if(!_script.SpriteDependencies.Contains(HailTemplate)||!_script.SpriteDependencies.Contains(HailImpactTemplate))Fail(command,"Hail raw child dependency metadata is missing.");
        var hail=_script.SpriteRecipes[HailTemplate];var impact=_script.SpriteRecipes[HailImpactTemplate];
        if(hail.Callback!="AnimHailBegin"||hail.Asset!="hail"||hail.Width!=16||hail.Height!=16||hail.Blend||hail.AffineDoubleSize||hail.AffineAnimations.Length!=3||
            impact.Callback!="AnimIceEffectParticle"||impact.Asset!="ice-crystals"||impact.Width!=8||impact.Height!=16||!impact.Blend||impact.AffineDoubleSize||impact.AffineAnimations.Length!=1)
            Fail(command,"Hail source child shape differs.");
        foreach(var recipe in new[]{hail,impact})
            if(recipe.Animations.Length!=1||recipe.Animations[0].Length!=1||recipe.Animations[0][0]!=new SourceSpriteAnimFrame(recipe==hail?0:4,1,false,false)||!recipe.LoopStarts.SequenceEqual([recipe==hail?0:-1])||recipe.FrameLoops.Any(p=>p.Length!=0))Fail(command,"Hail child frame program differs.");
        for(int i=0;i<3;i++)
        {
            SourceSpriteAffineCommand[] expected=[new("frame",256-16*i,256-16*i,0,0,0),new("end",0,0,0,0,0)];
            if(!hail.AffineAnimations[i].SequenceEqual(expected))Fail(command,"Hail affine scale differs.");
        }
        SourceSpriteAffineCommand[] hit=[new("frame",206,206,0,0,0),new("frame",5,5,0,10,0),new("frame",0,0,0,6,0),new("end",0,0,0,0,0)];
        if(!impact.AffineAnimations[0].SequenceEqual(hit))Fail(command,"Hail impact affine program differs.");
        return true;
    }
    private bool TryCreateHailTask(SourceAnimationCommand command)
    {
        if(command.Args[0]!="AnimTask_Hail")return false;
        if(!_loaded.Contains("ANIM_TAG_HAIL")||!_loaded.Contains("ANIM_TAG_ICE_CRYSTALS"))Fail(command,"Hail graphics have not been loaded.");
        _tasks.Add(new HailTask());return true;
    }
    private sealed class HailTask:VisualTask
    {
        // Source sHailCoordData: X,Y,battler position, signed placement selector.
        private static readonly (int X,int Y,int Slot,int Placement)[] Coordinates=[(100,120,0,2),(85,120,0,0),(242,120,1,1),(66,120,2,1),(182,120,3,0),(60,120,0,2),(214,120,1,0),(113,120,0,1),(210,120,3,1),(38,120,2,0)];
        private int _state,_active,_variant,_row,_wait,_spacing;
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_state)
            {
                case 0:if(++_wait>2){_wait=0;_spacing=0;_variant=0;_state++;}break;
                case 1:
                    if(_spacing==0)
                    {
                        if(Generate(owner))_active++;
                        if(++_variant==3){if(++_row==10)_state++;else _state--;}
                        else _spacing=1;
                    }
                    else _spacing--;
                    break;
                case 2:if(_active==0)Finished=true;break;
            }
        }
        private bool Generate(SourceBattleAnimation owner)
        {
            var row=Coordinates[_row];var end=new PixelPoint(row.X,row.Y);bool visible=false;
            if(row.Placement!=2&&owner._battlers.TryGetValue(row.Slot,out var battler)&&battler.Visible)
            {
                visible=true;end=battler.Center(owner._doubles,true);int sign=row.Placement==0?-1:1;
                end=new(end.X+sign*(battler.SourceWidth/6),end.Y+sign*(battler.SourceHeight/6));
            }
            int slot=owner.FindFreeSpriteSlot();if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)return false;
            owner._sprites.Add(new HailParticle(slot,owner._script.SpriteRecipes[HailTemplate],this,end,_variant,visible));return true;
        }
        public void ReleaseChild()=>_active--;
    }
    private sealed class HailParticle:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly HailTask _task;private readonly PixelPoint _end;
        private readonly int _variant;private readonly bool _hit,_impact;
        private PixelPoint _position;private int _age;
        private SourceSpriteFrameProgram? _frames;private SourceSpriteAffineProgram? _affine;
        public override bool Counted=>false;
        public HailParticle(int slot,SourceSpriteRecipe recipe,HailTask task,PixelPoint end,int variant,bool hit,bool impact=false):base(slot,18)
        {_recipe=recipe;_task=task;_end=end;_variant=variant;_hit=hit;_impact=impact;_position=impact?end:new(unchecked((short)(end.X-(end.Y+8)/2)),-8);}
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=_frames?.Frame??_recipe.Animations[0][0];var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);
                return new(Slot,_recipe.Template,_position,default,f.Tile,true,f.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority)
                {Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {
            if(_frames is null){_frames=new(_recipe);_affine=new(_recipe.AffineAnimations[_variant]);}
            else{_frames.Step();_affine!.Step();}
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_impact){if(++_age==20){_task.ReleaseChild();Finished=true;}return;}
            _position=new(unchecked((short)(_position.X+4)),unchecked((short)(_position.Y+8)));
            if(_position.X<_end.X&&_position.Y<_end.Y)return;
            if(_hit&&_variant==0)
            {
                // Allocate before freeing this cell. Failure intentionally strands the
                // source task counter; authored-root capacity is independently proven.
                int slot=owner.FindFreeSpriteSlot();
                if(slot!=SourceSpriteRegistry<VisualSprite>.Capacity)
                    owner._sprites.Add(new HailParticle(slot,owner._script.SpriteRecipes[HailImpactTemplate],_task,_end,0,false,true));
            }
            else _task.ReleaseChild();
            Finished=true;
        }
    }
}
