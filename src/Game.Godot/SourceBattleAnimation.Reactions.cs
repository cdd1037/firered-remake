namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string TriangleTemplate="gTriAttackTriangleSpriteTemplate",SweatTemplate="gFacadeSweatDropSpriteTemplate",SaltsHandTemplate="gSmellingSaltsHandSpriteTemplate",SaltsMarkTemplate="gSmellingSaltExclamationSpriteTemplate",LeafBladeTemplate="gLeafBladeSpriteTemplate";
    private static readonly ushort[] FacadeColors=[1852,5820,8795,11739,15706,18682,21625,25625,23577,20505,16409,13337,10266,6170,3098,27,59,187,315,411,540,636,764,893];
    private bool ValidateReactionCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op is "playsewithpan" or "loopsewithpan"&&name is "SE_M_SWAGGER" or "SE_M_SWAGGER2" or "SE_M_TRI_ATTACK2")
        {
            if(a.Length!=(c.Op=="loopsewithpan"?4:2)||SoundPan(a[1]) is < -64 or >63||!_script.SoundEffects.ContainsKey(name))Fail(c,"Reaction sound operands or dependency differ.");
            if(c.Op=="loopsewithpan"&&(Number(a[2]) is <0 or >255||Number(a[3]) is <2 or >255))Fail(c,"Reaction sound loop differs.");return true;
        }
        if(c.Op=="createsprite"&&name is TriangleTemplate or SaltsHandTemplate or SaltsMarkTemplate)
        {
            if(a.Length!=(name==TriangleTemplate?5:6)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Reaction sprite source operands differ.");
            if(name!=TriangleTemplate&&(Number(a[3]) is <0 or >1||Number(a[5]) is <1 or >32767||Number(a[4])<(name==SaltsHandTemplate?0:1)||Number(a[4])>(name==SaltsHandTemplate?1:32767)))Fail(c,"Smelling Salt selector or bounded repeat differs.");
            ValidateReactionRecipe(c,name);
            if(_validatingInvokedProgram&&name!=TriangleTemplate)ValidateReactionGeometry(c,_battlers[(Number(a[3])==0?_cue.Actor:_cue.Target).Value]);
            return true;
        }
        if(c.Op!="createvisualtask"||name is not ("AnimTask_SquishAndSweatDroplets" or "AnimTask_FacadeColorBlend" or "AnimTask_SmellingSaltsSquish" or "AnimTask_LeafBlade"))return false;
        if(a.Length!=(name=="AnimTask_LeafBlade"?2:4)||Number(a[1]) is <0 or >255)Fail(c,"Reaction task operands differ.");
        if(name!="AnimTask_LeafBlade"&&(Number(a[2]) is <0 or >1||Number(a[3])<(name=="AnimTask_FacadeColorBlend"?0:1)||Number(a[3])>32767))Fail(c,"Reaction participant or bounded duration differs.");
        if(name=="AnimTask_FacadeColorBlend")return true;
        if(name=="AnimTask_LeafBlade")
        {
            if(!_script.SpriteDependencies.Contains(LeafBladeTemplate)||_script.SineTable.Length!=320)Fail(c,"Leaf Blade raw child or sine dependency missing.");
            ValidateReactionRecipe(c,LeafBladeTemplate);
        }
        else
        {
            string program=name=="AnimTask_SquishAndSweatDroplets"?"sFacadeSquishAffineAnimCmds":"sSmellingSaltsSquishAffineAnimCmds";
            SourceTaskAffineCommand[] expected=name=="AnimTask_SquishAndSweatDroplets"?[new("frame",-16,16,0,6),new("frame",16,-16,0,12),new("frame",-16,16,0,6),new("end")]:[new("frame",0,-16,0,6),new("frame",0,16,0,6),new("end")];
            if(!_script.TaskAffinePrograms.TryGetValue(program,out var commands)||!commands.SequenceEqual(expected))Fail(c,"Reaction source task affine program differs.");
            if(name=="AnimTask_SquishAndSweatDroplets")
            {if(!_script.SpriteDependencies.Contains(SweatTemplate))Fail(c,"Squish raw sweat dependency missing.");ValidateReactionRecipe(c,SweatTemplate);}
        }
        if(_validatingInvokedProgram)
        {
            if(name=="AnimTask_LeafBlade")ValidateReactionGeometry(c,_battlers[_cue.Target.Value]);
            if(name is "AnimTask_LeafBlade" or "AnimTask_SquishAndSweatDroplets")ValidateReactionEmitterClosure(c,name);
        }
        return true;
    }
    private void ValidateReactionGeometry(SourceAnimationCommand c,AnimationBattler battler)
    {if(battler.SourceWidth is <8 or >64||battler.SourceHeight is <8 or >64||(battler.SourceWidth&7)!=0||(battler.SourceHeight&7)!=0)Fail(c,"Reaction requires exact source picture dimensions.");}
    private void ValidateReactionEmitterClosure(SourceAnimationCommand c,string name)
    {
        // Only invoked programs need a capacity proof; unused helpers keep ABI validation.
        var root=_script.Commands;
        if(!root.Contains(c)||root.Any(x=>x.Op is "call" or "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim")||root.Count(x=>x.Op=="createvisualtask"&&x.Args[0]==name)!=1)Fail(c,"Reaction emitter requires one bounded linear invocation.");
        bool leaf=name=="AnimTask_LeafBlade";
        var companions=root.Where(x=>x.Op=="createvisualtask"&&x.Args[0]!=name).ToArray();
        var sprites=root.Where(x=>x.Op=="createsprite").ToArray();
        bool damageShake=!leaf&&companions.Length==3&&companions.Count(x=>x.Args[0]=="AnimTask_BlendMonInAndOut")==1&&
            companions.Count(x=>x.Args[0]=="AnimTask_ShakeTargetBasedOnMovePowerOrDmg"&&x.Args.Length==7&&AnimationArgument(x.Args[2])==1)==2;
        if(damageShake)
        {
            if(sprites.Length!=2||sprites.Any(x=>x.Args[0]!="gBasicHitSplatSpriteTemplate"))Fail(c,"Squish damage-shake companion bound differs.");
        }
        else if(companions.Length>(leaf?4:1)||sprites.Length>(leaf?1:0)||
            sprites.Any(x=>x.Args[0]!="gCrossImpactSpriteTemplate")||
            companions.Any(x=>x.Args[0]!=(leaf?"AnimTask_ShakeMon2":"AnimTask_FacadeColorBlend")))
            Fail(c,"Reaction emitter companions exceed reviewed source capacity.");
        // Trails and sweat are affine-off. Facade needs one retained battler matrix.
        // The ordinary-entry proof allows existing affine-off tails in free cells.
        if(!leaf&&_spriteScene.Sprites.Any(s=>s.Frame.Affine||s.Frame.Scale!=256))Fail(c,"Squish cannot prove retained affine capacity.");
        // Seven eleven-tick arc passes with six-tick pauses peak at eleven trails.
        // Reserve the leader and the single allowed cross-impact companion as well.
        int needed=leaf?13:damageShake?6:4;
        if(Enumerable.Range(0,SourceSpriteRegistry<VisualSprite>.Capacity).Count(i=>!_battlers.Values.Any(b=>IsSourceBattlerPresent(b.Slot.Value)&&b.Slot.Value==i)&&!_spriteScene.Sprites.Any(s=>s.Slot==i))<needed)Fail(c,"Reaction emitter source sprite capacity proof failed.");
    }
    private void ValidateReactionRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Reaction source sprite recipe missing.");r=_script.SpriteRecipes[name];
        var expected=name switch{TriangleTemplate=>("tri-attack-triangle","AnimTriAttackTriangle",64),SweatTemplate=>("sweat-drop","AnimFacadeSweatDrop",8),SaltsHandTemplate=>("tag-hand","AnimSmellingSaltsHand",32),SaltsMarkTemplate=>("smellingsalt-effect","AnimSmellingSaltExclamation",32),_=>("leaf","SpriteCallbackDummy",16)};
        bool triangle=name==TriangleTemplate,leaf=name==LeafBladeTemplate;
        string tag="ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_');
        if(!_script.SpriteTags.TryGetValue(tag,out int paletteTag)||!_script.SpritePalettes.TryGetValue(paletteTag,out var palette)||palette.Length!=16)Fail(c,"Reaction indexed source palette dependency missing.");
        if(r.Asset!=expected.Item1||r.Callback!=expected.Item2||r.Width!=expected.Item3||r.Height!=r.Width||r.Blend||r.AffineDoubleSize!=triangle||r.FrameLoops.Any(x=>x.Length!=0))Fail(c,"Reaction source shape differs.");
        int[] tiles=leaf?[28,32,20,28,16,16,28]:[0];
        if(r.Animations.Length!=tiles.Length||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(leaf||triangle?-1:0,tiles.Length)))Fail(c,"Reaction frame table differs.");
        for(int i=0;i<tiles.Length;i++)if(!r.Animations[i].SequenceEqual([new SourceSpriteAnimFrame(tiles[i],triangle?8:1,leaf&&(i==3||i==5),false)]))Fail(c,"Reaction source frame differs.");
        SourceSpriteAffineCommand[] affine=[new("frame",0,0,5,40,0),new("frame",0,0,10,10,0),new("frame",0,0,15,10,0),new("frame",0,0,20,40,0),new("jump",0,0,0,0,0)];
        if(r.AffineAnimations.Length!=(triangle?1:0)||triangle&&!r.AffineAnimations[0].SequenceEqual(affine))Fail(c,"Reaction source affine differs.");
    }
    private bool TryCreateReactionSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (TriangleTemplate or SaltsHandTemplate or SaltsMarkTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Reaction graphics have not been loaded.");
        _sprites.Add(new ReactionSprite(slot,priority,r,this));return true;
    }
    private bool TryCreateReactionTask(SourceAnimationCommand c)
    {
        switch(c.Args[0])
        {
            case "AnimTask_FacadeColorBlend":_tasks.Add(new FacadeColorTask((_animationArgs[0]==0?_cue.Actor:_cue.Target).Value,_animationArgs[1]));return true;
            case "AnimTask_SquishAndSweatDroplets":
                if(!_loaded.Contains("ANIM_TAG_SWEAT_DROP"))Fail(c,"Squish sweat graphics have not been loaded.");
                _tasks.Add(new SquishSweatTask(this,(_animationArgs[0]==0?_cue.Actor:_cue.Target).Value,_animationArgs[1]));return true;
            case "AnimTask_SmellingSaltsSquish":
                if(_animationArgs[0]!=0)_tasks.Add(new SmellingSquishTask(this,_cue.Target.Value,_animationArgs[1]));return true;
            case "AnimTask_LeafBlade":
                if(!_loaded.Contains("ANIM_TAG_LEAF"))Fail(c,"Leaf Blade graphics have not been loaded.");
                _tasks.Add(new LeafBladeTask(this));return true;
            default:return false;
        }
    }
    private sealed class ReactionSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;private PhysicalLinear? _linear;
        private PixelPoint _position,_offset;private short _state,_timer,_steps,_repeat,_direction,_delay;private bool _invisible,_flip;
        public ReactionSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);var a=o._animationArgs;
            if(r.Template==TriangleTemplate)
            {
                var p=o._battlers[o._cue.Actor.Value].Base(o._doubles);_position=new(unchecked((short)(p.X+o.InitialXDirection()*a[0])),unchecked((short)(p.Y+a[1])));_affine=new(r.AffineAnimations[0]);Step(o);
            }
            else
            {
                var battler=o._battlers[(a[0]==0?o._cue.Actor:o._cue.Target).Value];var center=battler.Center(o._doubles,true);_repeat=a[2];
                if(r.Template==SaltsHandTemplate){_flip=a[1]==0;_direction=(short)(_flip?-1:1);_position=new(center.X+_direction*(battler.SourceWidth/2+8),center.Y);}
                else{_position=new(center.X,Math.Max(8,center.Y-battler.SourceHeight/2));_delay=a[1];}
            }
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,f.Tile+(_recipe.Template==SaltsHandTemplate?16:0),!_invisible,_flip||f.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==SaltsMarkTemplate)
            {if(++_timer>=_delay){_timer=0;_invisible=!_invisible;if(_invisible&&--_repeat==0)Finished=true;}return;}
            if(_recipe.Template==SaltsHandTemplate)
            {
                switch(_state)
                {
                    case 0:if(++_timer>1){_timer=0;_offset=new(_offset.X+_direction,0);if(++_steps==12)_state++;}break;
                    case 1:if(++_timer==8){_timer=0;_state++;}break;
                    case 2:_offset=new(unchecked((short)(_offset.X-_direction*4)),0);if(++_timer==6){_timer=0;_state++;}break;
                    case 3:_offset=new(unchecked((short)(_offset.X+_direction*3)),0);if(++_timer==8){if(--_repeat!=0){_timer=0;_state--;}else Finished=true;}break;
                }
                return;
            }
            switch(_state)
            {
                case 0:
                    if(++_timer<40)_invisible=(_timer&1)==0;if(_timer>30)_invisible=false;
                    if(_timer==61){_position=new(_position.X+_offset.X,_position.Y+_offset.Y);_offset=default;_state=1;}break;
                case 1:
                    var target=o._battlers[o._cue.Target.Value].Center(o._doubles,true);_linear=new(target.X-_position.X,target.Y-_position.Y,20);_state=2;goto case 2;
                case 2:if(_linear!.Step(out var p))_state=3;else _offset=p;break;
                case 3:Finished=true;break;
            }
        }
    }
    private sealed class FacadeColorTask(int slot,short duration):VisualTask
    {
        private short _remaining=duration,_color;
        public override void Step(SourceBattleAnimation o)
        {if(_remaining!=0){o.BlendPalettes(1u<<(16+slot),8,FacadeColors[_color]);if(++_color>23)_color=0;_remaining--;}else{o.BlendPalettes(1u<<(16+slot),0,0);Finished=true;}}
    }
    private sealed class SquishSweatTask:VisualTask
    {
        private readonly int _slot,_priority;private readonly PixelPoint _base;private readonly SourceTaskAffineProgram _program;private short _remaining,_state,_timer,_active;
        public SquishSweatTask(SourceBattleAnimation o,int slot,short count)
        {_slot=slot;_remaining=count;var battler=o._battlers[slot];_base=battler.Base(o._doubles);_priority=battler.Subpriority;_program=new(o._script.TaskAffinePrograms["sFacadeSquishAffineAnimCmds"]);o.PrepareTaskAffine(slot,_program);}
        public override void Step(SourceBattleAnimation o)
        {
            if(_state==1){if(_active==0)Finished=true;return;}
            ++_timer;if(_timer==6||_timer==18)Emit(o,_timer==6);
            if(!o.RunTaskAffine(_slot,_program)){if(--_remaining==0)_state++;else{_timer=0;o.PrepareTaskAffine(_slot,_program);}}
        }
        private void Emit(SourceBattleAnimation o,bool lower)
        {
            int x=lower?30:18,y=lower?20:-20;
            for(int i=0;i<4;i++)
            {
                int slot=o.FindFreeSpriteSlot();if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)continue;
                var p=new PixelPoint(_base.X+(i<2?-1:1)*(x+((i&1)*4)),_base.Y+y+(i&1)*6);
                o._sprites.Add(new SweatDroplet(slot,unchecked((byte)(_priority-5)),o._script.SpriteRecipes[SweatTemplate],p,i<2?-2:2,this));o._oamY[slot]=0;_active++;
            }
        }
        public void ChildEnded()=>_active--;
    }
    private sealed class SweatDroplet(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint position,int velocity,SquishSweatTask parent):VisualSprite(slot,priority)
    {
        private PixelPoint _position=position;private short _timer;private SourceSpriteFrameProgram? _frames;public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,recipe.Template,_position,default,_frames?.Frame.Tile??0,true,false,false,256,recipe.Asset,Subpriority){Width=8,Height=8};
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_frames is null)_frames=new(recipe);else _frames.Step();}
        public override void Step(SourceBattleAnimation o){_position=new(unchecked((short)(_position.X+velocity)),unchecked((short)(_position.Y-1)));if(++_timer>6){parent.ChildEnded();Finished=true;}}
    }
    private sealed class SmellingSquishTask:VisualTask
    {
        private readonly int _slot;private readonly SourceTaskAffineProgram _program;private short _remaining,_timer;
        public SmellingSquishTask(SourceBattleAnimation o,int slot,short count){_slot=slot;_remaining=count;_program=new(o._script.TaskAffinePrograms["sSmellingSaltsSquishAffineAnimCmds"]);o.PrepareTaskAffine(slot,_program);}
        public override void Step(SourceBattleAnimation o)
        {
            // Original data[2] is never incremented: every shake write is +2.
            if(++_timer>1){_timer=0;o._offsets[_slot]=new(2,o._offsets[_slot].Y);}
            if(!o.RunTaskAffine(_slot,_program)){o._offsets[_slot]=new(0,o._offsets[_slot].Y);if(--_remaining!=0){o.PrepareTaskAffine(_slot,_program);_timer=0;}else Finished=true;}
        }
    }
}
