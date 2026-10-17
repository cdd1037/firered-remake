namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string WishStarTemplate="gWishStarSpriteTemplate";
    private bool ValidateWishStarOperand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createsprite"||a.FirstOrDefault()!=WishStarTemplate)return false;
        if(a.Length!=3||Number(a[1])!=0||Number(a[2])!=40)Fail(command,"Wish star source selector/priority differs.");
        ValidateGoldStarRecipe(command,WishStarTemplate,16,"AnimWishStar");
        ValidateGoldStarRecipe(command,MiniStarTemplate,8,"AnimMiniTwinklingStar");
        RequireVisualSeed(command);return true;
    }
    private bool TryCreateWishStarSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0]!=WishStarTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_GOLD_STARS"))Fail(command,"Gold star graphics have not been loaded.");
        _sprites.Add(new WishStarSprite(slot,priority,_script.SpriteRecipes[WishStarTemplate],_cue.Actor.IsPlayer));return true;
    }
    private sealed class WishStarSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly bool _player;private readonly PixelPoint _position;private PixelPoint _offset;private short _dx,_dy,_age;
        public WishStarSprite(int slot,int priority,SourceSpriteRecipe recipe,bool player):base(slot,priority)
        {_recipe=recipe;_frames=new(recipe);_player=player;_position=new(player?256:-16,0);}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,false,false,256,_recipe.Asset,Subpriority){Width=16,Height=16};
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
        public override void Step(SourceBattleAnimation owner)
        {
            _dx=unchecked((short)(_dx+72));_dy=unchecked((short)(_dy+16));
            _offset=new(_player?-(_dx>>4):_dx>>4,unchecked((short)(_offset.Y+(_dy>>8))));
            _age=unchecked((short)(_age+1));
            if(_age%3==0)
            {
                var position=new PixelPoint(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));
                owner._sprites.CreateAndAnimate(slot=>new MiniTwinklingStar(slot,owner._script.SpriteRecipes[MiniStarTemplate],position,unchecked((byte)(Subpriority+1))),
                    owner.StepSourceSprite,sprite=>sprite.AdvanceAnimation(owner),owner.IsReservedSpriteSlot);
            }
            if(unchecked((uint)(_position.X+_offset.X+32))>304)Finished=true;
        }
    }
}
