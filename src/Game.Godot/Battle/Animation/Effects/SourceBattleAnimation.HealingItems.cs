namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private bool ValidateHealingItemCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var found)||found.Callback is not ("AnimSoftBoiledEgg" or "AnimMilkBottle"))return false;
        bool egg=found.Callback=="AnimSoftBoiledEgg";
        if(a.Length!=(egg?6:3)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Healing item source operands differ.");
        var recipe=_script.SpriteRecipes[a[0]];
        if(recipe.Width!=32||recipe.Height!=32||!recipe.Blend||recipe.AffineDoubleSize!=egg||recipe.AffineAnimations.Length!=(egg?3:2)||egg&&Number(a[5]) is <0 or >1)Fail(c,"Healing item source recipe differs.");
        return true;
    }
    private bool TryCreateHealingItemSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(c.Args[0],out var recipe)||recipe.Callback is not ("AnimSoftBoiledEgg" or "AnimMilkBottle"))return false;
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Healing item graphics are not loaded.");
        _sprites.Add(new HealingItemSprite(slot,priority,recipe,c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private sealed class HealingItemSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private SourceSpriteAffineProgram _affine;
        private PixelPoint _position,_offset;private readonly short[] _d=new short[8];private int _phase,_tile;private bool _hidden,_restarted;
        private readonly bool _egg,_opponent;
        public HealingItemSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] a,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);_egg=recipe.Callback=="AnimSoftBoiledEgg";_opponent=!o._cue.Actor.IsPlayer;
            if(_egg){var p=o._battlers[o._cue.Actor.Value].Base(o._doubles);_position=new((short)(p.X+o.InitialXDirection()*a[0]),(short)(p.Y+a[1]));_d[0]=0x380;_d[1]=(short)(_opponent?-160:160);_d[7]=(short)a[2];}
            else{var p=o._battlers[o._cue.Target.Value].Center(o._doubles,true);_position=new(p.X,(short)(p.Y-24));_d[7]=16;Alpha(o,0,16);}
        }
        private static void Alpha(SourceBattleAnimation o,int a,int b){o._blendEnabled=true;o._alphaA=a;o._alphaB=b;}
        private static void ClearAlpha(SourceBattleAnimation o){o._blendEnabled=false;o._alphaA=o._alphaB=0;}
        private void Restart(int index){_affine=new(_recipe.AffineAnimations[index]);_restarted=true;}
        public override SourceVisualSpriteFrame Frame{get{var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile+_tile,!_hidden,_frames.Frame.HFlip,true,256,_recipe.Asset,Subpriority){Width=32,Height=32,VFlip=_frames.Frame.VFlip,Affine=true,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void Step(SourceBattleAnimation o)
        {
            _restarted=false;if(_egg)Egg(o);else Milk(o);if(Finished)return;if(!_restarted)_affine.Step();_frames.Step();
        }
        private void Egg(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:
                    _offset=new(_d[1]>>8,(short)(_offset.Y-(_d[0]>>8)));_d[0]-=32;_d[1]+=(short)(_opponent?-160:160);
                    if(_offset.Y>0){_position=new((short)(_position.X+_offset.X),(short)(_position.Y+_offset.Y));_offset=default;_d[0]=0;Restart(1);_phase=1;}break;
                case 1:if(_d[0]++>19){Restart(2);_phase=2;}break;
                case 2:if(_affine.Ended){Restart(1);_d[0]=0;_tile+=_d[7]==0?16:32;_phase=_d[7]==0?3:5;}break;
                case 3:_offset=new(_offset.X,(short)(_offset.Y-2));if(++_d[0]==9){_d[0]=16;_d[1]=0;Alpha(o,16,0);_phase=4;}break;
                case 4:if(_d[1]++%3==0){_d[0]--;Alpha(o,_d[0],16-_d[0]);if(_d[0]==0)_phase=5;}break;
                case 5:if((ushort)o._animationArgs[7]==0xFFFF){_hidden=true;_phase=_d[7]==0?6:7;}break;
                case 6:ClearAlpha(o);Finished=true;break;
                default:Finished=true;break;
            }
        }
        private void Milk(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:
                    if(++_d[2]>0){_d[2]=0;if((++_d[1]&1)!=0){if(_d[6]<=15)_d[6]++;}else if(_d[7]>0)_d[7]--;Alpha(o,_d[6],_d[7]);if(_d[6]==16&&_d[7]==0){_d[1]=0;_phase++;}}break;
                case 1:if(++_d[1]>8){_d[1]=0;Restart(1);_phase++;}break;
                case 2:
                    if(_d[3]<=11)_d[4]+=2;if((ushort)(_d[3]-0x12)<=0x17)_d[4]-=2;if(_d[3]>0x2F)_d[4]+=2;
                    _offset=new(_d[4]/9,Math.Abs(_d[4]/14));if(++_d[3]>0x3B)_d[3]=0;
                    if(++_d[1]>2){_d[1]=0;_position=new(_position.X,(short)(_position.Y+1));}
                    if(++_d[2]<=29)break;
                    if((_d[2]&1)!=0){if(_d[6]>0)_d[6]--;}else if(_d[7]<=15)_d[7]++;
                    Alpha(o,_d[6],_d[7]);if(_d[6]==0&&_d[7]==16){_d[1]=_d[2]=0;_phase++;}break;
                case 3:_hidden=true;_phase++;break;
                default:ClearAlpha(o);Finished=true;break;
            }
        }
    }
}
