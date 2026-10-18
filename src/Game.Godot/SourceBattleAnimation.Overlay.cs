namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string GlareDotTemplate="gGlareEyeDotSpriteTemplate",MistBallTemplate="gMistBallSpriteTemplate";
    private bool ValidateOverlayOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        bool glare=command.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_GlareEyeDots";
        bool overlay=command.Op=="createvisualtask"&&a.FirstOrDefault() is "AnimTask_ScaryFace" or "AnimTask_HazeScrollingFog" or "AnimTask_MistBallFog";
        bool mist=command.Op=="createsprite"&&a.FirstOrDefault()==MistBallTemplate;
        if(!glare&&!overlay&&!mist)return false;
        if(overlay)
        {
            if(a.Length!=2||Number(a[1])!=5)Fail(command,"BG1 overlay task operands differ.");
            string[] materials=a[0]=="AnimTask_ScaryFace"?["scary-face-player","scary-face-opponent"]:["fog"];
            if(_validatingInvokedProgram)
            {
                foreach(string material in materials)
                    if(!_script.OverlayDependencies.Contains(material)||!_script.OverlayPalettes.TryGetValue(material,out var palette)||palette.Length!=16)
                        Fail(command,"BG1 overlay material dependency or palette is missing.");
                var closure=OverlayInvocationCommands();
                if(closure.Any(c=>c.Op=="monbg"))Fail(command,"BG1 overlay plus monbg ownership is outside the bounded source closure.");
                if(closure.Count(IsOverlayCommand)!=1)Fail(command,"Multiple BG1 overlay owners are outside the bounded source closure.");
            }
            return true;
        }
        if(glare)
        {
            if(a.Length!=3||Number(a[1])!=5||Number(a[2]) is <short.MinValue or >short.MaxValue||!_script.SpriteDependencies.Contains(GlareDotTemplate))Fail(command,"Glare task operands or raw child dependency differ.");
        }
        else if(a.Length!=9||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue)||Number(a[7])<1||Number(a[8]) is not (0 or 1 or 256 or 257))
            Fail(command,"Mist Ball source-width operands differ.");
        if(_validatingInvokedProgram)
        {
            RequireOverlaySpritePalette(command,glare?"ANIM_TAG_SMALL_RED_EYE":"ANIM_TAG_SMALL_BUBBLES");
            if(mist&&OverlayInvocationCommands().Any(c=>c.Op=="loadspritegfx"&&c.Args.FirstOrDefault()=="ANIM_TAG_WHITE_FEATHER"))
            {
                if(!_script.MaterialDependencies.Contains("white-feather"))Fail(command,"Mist Ball load-only material dependency is missing.");
                RequireOverlaySpritePalette(command,"ANIM_TAG_WHITE_FEATHER");
            }
        }
        string template=glare?GlareDotTemplate:MistBallTemplate;
        if(!_script.SpriteRecipes.TryGetValue(template,out var recipe)){Fail(command,"Overlay sprite recipe is missing.");return false;}
        if(recipe.Callback!=(glare?"AnimGlareEyeDot":"AnimThrowMistBall")||recipe.Asset!=(glare?"small-red-eye":"small-bubbles")||recipe.Width!=(glare?8:16)||recipe.Height!=recipe.Width||recipe.Blend||recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=0||recipe.FrameLoops.Any(p=>p.Length!=0)||
            recipe.Animations.Length!=1||!recipe.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!recipe.LoopStarts.SequenceEqual([0]))
            Fail(command,"Overlay sprite source shape or dummy program differs.");
        return true;
    }
    private static bool IsOverlayCommand(SourceAnimationCommand c)=>c.Op=="createvisualtask"&&c.Args.FirstOrDefault() is "AnimTask_ScaryFace" or "AnimTask_HazeScrollingFog" or "AnimTask_MistBallFog" or "AnimTask_LoadSandstormBackground" or "AnimTask_MorningSunLightBeam";
    private SourceAnimationCommand[] OverlayInvocationCommands()
    {
        // Catalogs share uninvoked helpers. Only calls in this root contribute
        // ownership; repeated calls are counted separately. Nested calls already
        // fail the common bounded-program validator.
        return _script.Commands.SelectMany(c=>c.Op=="call"&&c.Args.Length==1&&_script.Subroutines.TryGetValue(c.Args[0],out var body)?new[]{c}.Concat(body):new[]{c}).ToArray();
    }
    private void RequireOverlaySpritePalette(SourceAnimationCommand command,string tag)
    {
        if(!_script.SpriteTags.TryGetValue(tag,out int value)||!_script.SpritePalettes.TryGetValue(value,out var palette)||palette.Length!=16)
            Fail(command,"Overlay indexed sprite palette dependency is missing.");
    }
    private bool TryCreateOverlayTask(SourceAnimationCommand command)
    {
        switch(command.Args[0])
        {
            case "AnimTask_GlareEyeDots":
                if(!_loaded.Contains("ANIM_TAG_SMALL_RED_EYE"))Fail(command,"Glare child graphics have not been loaded.");
                _tasks.Add(new GlareDotsTask(this));return true;
            case "AnimTask_ScaryFace":
                _tasks.Add(new OverlayTask(0,this));return true;
            case "AnimTask_HazeScrollingFog":
                _tasks.Add(new OverlayTask(1,this));return true;
            case "AnimTask_MistBallFog":
                _tasks.Add(new OverlayTask(2,this));return true;
            default:return false;
        }
    }
    private bool TryCreateOverlaySprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0]!=MistBallTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_SMALL_BUBBLES"))Fail(command,"Mist Ball graphics have not been loaded.");
        _sprites.Add(new MistBallSprite(slot,priority,_script.SpriteRecipes[MistBallTemplate],this));return true;
    }
    private sealed class OverlayTask:VisualTask
    {
        private static readonly byte[] HazeAmounts=[0,1,2,2,2,2,3,4,4,4,5,6,6,6,6,7,8,8,8,9];
        private static readonly byte[] MistAmounts=[0,1,1,1,1,2,2,2,2,3,3,3,3,3,4,4,4,4,4,5];
        private readonly int _kind;private int _state,_timer,_index,_alpha;private ushort _scroll;
        public OverlayTask(int kind,SourceBattleAnimation owner)
        {
            _kind=kind;
            owner.BeginOverlay(kind==0?(owner._cue.Target.IsPlayer?"scary-face-opponent":"scary-face-player"):"fog");
            owner.SetBackgroundPriority(1,1);owner.SetAlphaBlend(2,63,0,16);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_kind!=0){_scroll=unchecked((ushort)(_scroll-1));owner.SetOverlayScroll(_scroll,0);}
            int peak=_kind==0?14:_kind==1?9:5;
            switch(_state)
            {
                case 0:
                    if(_kind==2||++_timer==(_kind==0?2:4))
                    {
                        _timer=0;_alpha=_kind==0?_alpha+1:(_kind==1?HazeAmounts:MistAmounts)[++_index];
                        owner._alphaA=_alpha;owner._alphaB=(_kind==2?17:16)-_alpha;
                        if(_alpha==peak){_state=1;_alpha=0;}
                    }
                    break;
                case 1:
                    if(++_alpha==(_kind==0?21:81)){_alpha=peak;_state=2;}
                    break;
                case 2:
                    if(++_timer==(_kind==0?2:4))
                    {
                        _timer=0;--_alpha;owner._alphaA=_alpha;owner._alphaB=16-_alpha;
                        if(_alpha==0)_state=3;
                    }
                    break;
                case 3:
                    // Source case 3 clears both animation BGs and falls through
                    // to case 4 in this same RunTasks invocation.
                    if(_kind!=0)owner.SetOverlayScroll(0,0);
                    owner.EndOverlay(true);owner.SetBackgroundPriority(1,1);
                    owner.ClearAlphaBlend();Finished=true;
                    break;
            }
        }
    }
    private sealed class GlareDotsTask:VisualTask
    {
        private readonly PixelPoint _start,_end;
        private int _timer,_pair,_active;private bool _waiting;
        public GlareDotsTask(SourceBattleAnimation owner)
        {
            var actor=owner._battlers[owner._cue.Actor.Value];var center=actor.Center(owner._doubles,true);int quarter=actor.SourceHeight/4;
            _start=new(unchecked((short)(center.X+(actor.Slot.IsPlayer?quarter:-quarter))),unchecked((short)(center.Y-quarter)));
            _end=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
        }
        private PixelPoint Position()
        {
            if(_pair==0)return _start;if(_pair>=12)return _end;
            int x=(_start.X<<8)+_pair*(((_end.X-_start.X)<<8)/11),y=(_start.Y<<8)+_pair*(((_end.Y-_start.Y)<<8)/11);
            return new(unchecked((short)(x>>8)),unchecked((short)(y>>8)));
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_waiting){if(_active==0)Finished=true;return;}
            if(++_timer<=3)return;_timer=0;var position=Position();
            for(int i=0;i<2;i++)
            {
                int slot=owner.FindFreeSpriteSlot();if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)continue;
                owner._sprites.Add(new GlareDotSprite(slot,owner._script.SpriteRecipes[GlareDotTemplate],position,i==0?-3:3,this));_active++;
            }
            if(_pair==12)_waiting=true;_pair++;
        }
        public void Release()=>_active--;
    }
    private sealed class GlareDotSprite(int slot,SourceSpriteRecipe recipe,PixelPoint position,int diagonal,GlareDotsTask task):VisualSprite(slot,35)
    {
        private int _timer;
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,recipe.Template,position,new(diagonal,diagonal),0,true,false,false,256,recipe.Asset,Subpriority){Width=8,Height=8};
        public override void Step(SourceBattleAnimation owner){if(++_timer>36){task.Release();Finished=true;}}
    }
    private sealed class MistBallSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private PixelPoint _position,_offset;private PhysicalLinear? _linear;private bool _destroyNext;
        public MistBallSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {_recipe=recipe;_position=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,0,true,false,false,256,_recipe.Asset,Subpriority){Width=16,Height=16};
        public override void Step(SourceBattleAnimation owner)
        {
            if(_destroyNext){Finished=true;return;}
            if(_linear is null)
            {
                // AnimThrowMistBall merely installed this initializer. Read live
                // shared arguments in the following sprite callback, then install
                // StartAnimLinearTranslation without moving on this invocation.
                var a=owner._animationArgs;var actor=owner._battlers[owner._cue.Actor.Value];var target=owner._battlers[owner._cue.Target.Value];
                var start=(a[5]&0xff00)==0?actor.Center(owner._doubles,true):actor.Base(owner._doubles);
                _position=new(unchecked((short)(start.X+owner.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
                var end=(a[5]&0xff)==0?target.Center(owner._doubles,true):target.Base(owner._doubles);
                if(!actor.Slot.IsPlayer)a[2]=unchecked((short)-a[2]);
                _linear=new(unchecked((short)(end.X+a[2]))-_position.X,unchecked((short)(end.Y+a[3]))-_position.Y,a[4]);return;
            }
            if(_linear.Step(out var offset))_destroyNext=true;else _offset=offset;
        }
    }
}
