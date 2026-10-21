namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SmokescreenImpactTask="AnimTask_SmokescreenImpact",SmokescreenImpactTemplate="sSmokescreenImpactSpriteTemplate",BlackSmokeTemplate="gBlackSmokeSpriteTemplate";
    private const int SmokescreenGraphicsTag=55019;
    private bool ValidateSmokescreenCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="loadspritegfx"&&name=="ANIM_TAG_BLACK_SMOKE")
        {if(a.Length!=1||!_script.SpriteTags.ContainsKey(name))Fail(c,"Black smoke source graphics tag is missing.");return true;}
        bool sprite=c.Op=="createsprite"&&name==BlackSmokeTemplate,task=c.Op=="createvisualtask"&&name==SmokescreenImpactTask;
        if(!sprite&&!task)return false;
        if(a.Length!=(sprite?8:2)||Number(a[sprite?2:1]) is <0 or >255||sprite&&(Number(a[1]) is <0 or >1||Number(a[2])>127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue)))Fail(c,"Smokescreen source operand shape differs.");
        if(sprite)
        {
            if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Black smoke recipe is missing.");return false;}
            if(r.Asset!="black-smoke"||r.Callback!="AnimBlackSmoke"||r.Width!=32||r.Height!=16||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.AffineAnimations.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!r.LoopStarts.SequenceEqual([0])||r.FrameLoops.Length>1||r.FrameLoops.Any(x=>x.Length!=0))Fail(c,"Black smoke dummy source frame program differs.");
            return true;
        }
        if(!_script.SpriteDependencies.Contains(SmokescreenImpactTemplate)||!_script.SpriteRecipes.TryGetValue(SmokescreenImpactTemplate,out var impact)){Fail(c,"Smokescreen raw quadrant dependency is missing.");return false;}
        if(impact.Asset!="smokescreen-impact"||impact.Callback!="SpriteCB_SmokescreenImpact"||impact.Width!=16||impact.Height!=16||impact.Blend||impact.AffineDoubleSize||impact.Subsprites.Length!=0||impact.AffineAnimations.Length!=0||impact.Animations.Length!=4||!impact.LoopStarts.SequenceEqual([-1,-1,-1,-1])||impact.FrameLoops.Length>4||impact.FrameLoops.Any(x=>x.Length!=0))Fail(c,"Smokescreen raw quadrant shape differs.");
        for(int i=0;i<4;i++)if(!impact.Animations[i].SequenceEqual(new[]{0,4,8}.Select(tile=>new SourceSpriteAnimFrame(tile,4,(i&1)!=0,(i&2)!=0))))Fail(c,"Smokescreen quadrant source frame/flip table differs.");
        if(_script.SpriteTags.GetValueOrDefault("TAG_SMOKESCREEN",-1)!=SmokescreenGraphicsTag||!_script.SpritePalettes.TryGetValue(SmokescreenGraphicsTag,out var palette)||palette.Length!=16||palette.Any(v=>v>32767))Fail(c,"Smokescreen dynamic indexed palette is missing.");
        if(_spriteMaterials is not null&&(!_spriteMaterials.TryGetValue("smokescreen-impact",out var material)||material.Width!=16||material.Height!=48||material.PaletteTag!=SmokescreenGraphicsTag||material.PaletteIndices.Length!=768||!material.Palette.SequenceEqual(_script.SpritePalettes[SmokescreenGraphicsTag])))Fail(c,"Smokescreen dynamic indexed material differs.");
        if(!_validatingInvokedProgram)return true;
        if(!IsSourceBattlerPresent(_cue.Target.Value))Fail(c,"Smokescreen requires its source target sprite.");
        ValidateReactionGeometry(c,_battlers[_cue.Target.Value]);
        var commands=OverlayInvocationCommands();
        if(commands.Any(x=>x.Op=="createsprite"&&x.Args[0] is not (BlackSmokeTemplate or "gBlackBallSpriteTemplate")||x.Op=="createvisualtask"&&x.Args[0]!=SmokescreenImpactTask||x.Op is not ("loadspritegfx" or "createsprite" or "createvisualtask" or "delay" or "playsewithpan" or "waitforvisualfinish" or "end" or "call" or "return")))Fail(c,"Smokescreen requires its bounded raw-parent and non-emitting smoke closure.");
        if(MaximumInvocationCost(x=>x.Op=="createvisualtask"&&x.Args[0]==SmokescreenImpactTask?1:0)!=1||commands.Any(x=>x.Op=="loadspritegfx"&&x.Args[0] is not ("ANIM_TAG_BLACK_BALL" or "ANIM_TAG_BLACK_SMOKE")))Fail(c,"Smokescreen requires one dynamic graphics owner.");
        if(_spriteScene.HasDynamicSpriteGraphics(SmokescreenGraphicsTag))Fail(c,"Smokescreen dynamic tag is still owned by a previous raw parent.");
        int births=MaximumInvocationCost(x=>x.Op=="createsprite"?1:0)+5;
        int free=Enumerable.Range(0,64).Count(slot=>!IsSourceBattlerPresent(slot)&&_spriteScene.Sprites[slot] is null);
        if(births>free||22+births+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64)Fail(c,"Smokescreen complete path exceeds source sprite or OAM capacity.");
        int paletteCount=commands.Where(x=>x.Op=="loadspritegfx").Select(x=>_script.SpriteTags.GetValueOrDefault(x.Args[0],-1)).Append(SmokescreenGraphicsTag).Distinct().Count();
        if(paletteCount>Enumerable.Range(4,12).Count(slot=>_spritePaletteTags[slot]==65535))Fail(c,"Smokescreen complete path exceeds source palette capacity.");
        return true;
    }
    private bool TryCreateSmokescreenSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=BlackSmokeTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_BLACK_SMOKE"))Fail(c,"Black smoke graphics are not loaded.");
        _sprites.Add(new BlackSmokeSprite(slot,priority,_script.SpriteRecipes[BlackSmokeTemplate],this));return true;
    }
    private bool TryCreateSmokescreenTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!=SmokescreenImpactTask)return false;
        // The native helper has no allocation-failure guards. Validation proves
        // the full invoked closure's capacity before any script or sound effects.
        AcquireDynamicSpriteGraphics(SmokescreenGraphicsTag);
        int slot=RequireFreeSpriteSlot();var parent=new SmokescreenImpactParent(slot);_sprites.Add(parent);_oamY[slot]=160;
        var target=_battlers[_cue.Target.Value].Center(_doubles,true);int x=unchecked((short)(target.X+8)),y=unchecked((short)(target.Y+8));
        for(int quadrant=0;quadrant<4;quadrant++)
        {
            slot=RequireFreeSpriteSlot();var position=new PixelPoint(unchecked((short)(x+((quadrant&1)==0?-16:0))),unchecked((short)(y+((quadrant&2)==0?-16:0))));
            _sprites.Add(new SmokescreenImpactQuadrant(slot,parent,_script.SpriteRecipes[SmokescreenImpactTemplate],position,quadrant));_oamY[slot]=0;parent.LiveChildren++;
        }
        // AnimTask_SmokescreenImpact destroys its counted task in its initializer.
        return true;
    }
    private sealed class BlackSmokeSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;private readonly short _speed;
        private short _remaining,_sum;private PixelPoint _offset;private bool _invisible;
        public BlackSmokeSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=owner._animationArgs;var p=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _position=new(unchecked((short)(p.X+a[0])),unchecked((short)(p.Y+a[1])));_speed=unchecked((short)(a[3]==0?a[2]:-a[2]));_remaining=a[4];
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_remaining<=0){Finished=true;return;}
            _offset=new(_sum>>8,0);_sum=unchecked((short)(_sum+_speed));_invisible=!_invisible;_remaining--;
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame=>new(Slot,BlackSmokeTemplate,_position,_offset,_frames.Frame.Tile,!_invisible,false,false,256,_recipe.Asset,Subpriority){Width=32,Height=16};
    }
    private sealed class SmokescreenImpactParent(int slot):VisualSprite(slot,14)
    {
        public int LiveChildren;public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"SpriteCB_SmokescreenImpactMain",new(248,168),default,0,false,false,false,256,null,Subpriority){Width=8,Height=8,OamPriority=3};
        public override void Step(SourceBattleAnimation owner)
        {if(LiveChildren!=0)return;owner.ReleaseDynamicSpriteGraphics(SmokescreenGraphicsTag);Finished=true;}
    }
    private sealed class SmokescreenImpactQuadrant(int slot,SmokescreenImpactParent parent,SourceSpriteRecipe recipe,PixelPoint position,int quadrant):VisualSprite(slot,2)
    {
        // Constructor is the helper's explicit AnimateSprite/BeginAnim call.
        private readonly SourceSpriteFrameProgram _frames=new(recipe,quadrant);
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner)
        {if(!_frames.Ended)return;parent.LiveChildren--;Finished=true;}
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame{get{var f=_frames.Frame;return new(Slot,SmokescreenImpactTemplate,position,default,f.Tile,true,f.HFlip,false,256,recipe.Asset,Subpriority){Width=16,Height=16,VFlip=f.VFlip,OamPriority=1};}}
    }
}
