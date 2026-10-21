namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string MeteorStarTemplate="gMeteorMashStarSpriteTemplate",MiniStarTemplate="gMiniTwinklingStarSpriteTemplate";
    private bool ValidateMeteorStarOperand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createsprite"||a.FirstOrDefault()!=MeteorStarTemplate)return false;
        if(a.Length!=8||!_script.SpriteRecipes.ContainsKey(MeteorStarTemplate)||!_script.SpriteRecipes.ContainsKey(MiniStarTemplate))Fail(command,"Missing shooting/twinkling star recipe or operands.");
        ValidateGoldStarRecipe(command,MeteorStarTemplate,16,"AnimMeteorMashStar");
        ValidateGoldStarRecipe(command,MiniStarTemplate,8,"AnimMiniTwinklingStar");
        if(Number(a[1])!=1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>AnimationArgument(v) is <short.MinValue or >short.MaxValue)||AnimationArgument(a[7])!=30)Fail(command,"Shooting star operands exceed the reviewed source closure.");
        RequireVisualSeed(command);return true;
    }
    private void ValidateGoldStarRecipe(SourceAnimationCommand command,string template,int size,string callback)
    {
        if(!_script.SpriteRecipes.ContainsKey(template))Fail(command,"Missing gold star recipe.");
        var recipe=_script.SpriteRecipes[template];
        if(recipe.Callback!=callback||recipe.Asset!="gold-stars"||recipe.Width!=size||recipe.Height!=size||recipe.Blend||recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=0||
            recipe.Animations.Length!=1||recipe.Animations[0].Length!=1||recipe.Animations[0][0]!=new SourceSpriteAnimFrame(0,1,false,false)||!recipe.LoopStarts.SequenceEqual([0])||recipe.FrameLoops.Any(p=>p.Length!=0))Fail(command,"Shooting/twinkling source template differs.");
    }
    private bool TryCreateMeteorStarSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0]!=MeteorStarTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_GOLD_STARS"))Fail(command,"Gold star graphics have not been loaded.");
        var target=_battlers[_cue.Target.Value];
        _sprites.Add(new MeteorStarSprite(slot,priority,_script.SpriteRecipes[MeteorStarTemplate],target.Center(_doubles,true),target.Slot.IsPlayer,_animationArgs));return true;
    }
    private sealed class MeteorStarSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _position;private PixelPoint _offset;private readonly short[] _data=new short[8];
        public MeteorStarSprite(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint center,bool targetIsPlayer,short[] a):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);int sign=targetIsPlayer?-1:1;
            _data[0]=unchecked((short)(center.X+sign*a[0]));_data[1]=unchecked((short)(center.Y+a[1]));
            _data[2]=unchecked((short)(center.X+sign*a[2]));_data[3]=unchecked((short)(center.Y+a[3]));_data[4]=a[4];_position=new(_data[0],_data[1]);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,false,false,256,_recipe.Asset,Subpriority){Width=16,Height=16};
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override void Step(SourceBattleAnimation owner)
        {
            _offset=new(unchecked((short)((_data[2]-_data[0])*_data[5]/_data[4])),unchecked((short)((_data[3]-_data[1])*_data[5]/_data[4])));
            if((_data[5]&1)==0)
            {
                int slot=owner.FindFreeSpriteSlot();
                // Source CreateSprite returns its inert sentinel on exhaustion;
                // no child initializer runs, so no random value is consumed.
                if(slot!=SourceSpriteRegistry<VisualSprite>.Capacity)
                    owner._sprites.Add(new MiniTwinklingStar(slot,owner._script.SpriteRecipes[MiniStarTemplate],new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)))));
            }
            if(_data[5]==_data[4])Finished=true;_data[5]++;
        }
    }
    private sealed class MiniTwinklingStar(int slot,SourceSpriteRecipe recipe,PixelPoint position,int priority=5):VisualSprite(slot,priority)
    {
        private SourceSpriteFrameProgram? _frames;private short _age,_blink;private int _tile,_yOffset;private bool _initialized,_invisible;
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,recipe.Template,position,new(0,_yOffset),_tile,!_invisible,false,false,256,recipe.Asset,Subpriority){Width=8,Height=8};
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {
            // Source dummy-END BeginAnim does not write OAM.tileNum. Preserve
            // the callback's manual tile4/5 while reusing the inert frame player.
            if(_frames is null)_frames=new(recipe);else _frames.Step();
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(!_initialized)
            {
                _tile=(owner.NextVisualRandom()&3)==0?4:5;int y=owner.NextVisualRandom()&7;_yOffset=y>3?-y:y;_initialized=true;return;
            }
            if(++_age<30)
            {
                if(++_blink==2){_invisible=!_invisible;_blink=0;}
            }
            else
            {
                if(_blink==2)_invisible=false;
                if(_blink==3){_invisible=true;_blink=-1;}
                _blink++;
            }
            if(_age>60)Finished=true;
        }
    }
}
