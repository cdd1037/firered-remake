namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateTrappingCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var found)||found.Callback is not ("AnimMeanLookEye" or "AnimBlockX"))return false;
        var recipe=_script.SpriteRecipes[a[0]];
        if(a.Length!=3||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||recipe.Width!=64||recipe.Height!=64||recipe.Animations.Length!=1)Fail(command,"Trapping sprite source recipe or operands differ.");
        if(recipe.Callback=="AnimMeanLookEye")
        {if(!recipe.Blend||!recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=2)Fail(command,"Mean Look source affine recipe differs.");}
        else if(recipe.Blend||recipe.AffineAnimations.Length!=0||_script.SineTable.Length!=320||!_script.SoundEffects.ContainsKey("SE_M_SKETCH")||!_script.SoundEffects.ContainsKey("SE_M_LEER"))Fail(command,"Block source recipe or callback sound dependency differs.");
        return true;
    }
    private bool TryCreateTrappingSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(command.Args[0],out var recipe)||recipe.Callback is not ("AnimMeanLookEye" or "AnimBlockX"))return false;
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Trapping sprite graphics are not loaded.");
        _sprites.Add(new TrappingSprite(slot,priority,recipe,this));return true;
    }
    private sealed class TrappingSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private SourceSpriteAffineProgram? _affine;
        private readonly PixelPoint _position;
        private readonly short[] _data=new short[8];
        private PixelPoint _offset;
        private int _phase;
        private bool _hidden,_paused,_restarted;
        public TrappingSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,recipe.Callback=="AnimBlockX"?unchecked((byte)(owner._battlers[owner._cue.Target.Value].Subpriority+(owner._cue.Target.IsPlayer?-2:2))):priority)
        {
            _recipe=recipe;_frames=new(recipe);_position=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            if(recipe.Callback=="AnimMeanLookEye")
            {_affine=new(recipe.AffineAnimations[0]);owner._blendEnabled=true;owner._alphaA=0;owner._alphaB=16;_data[0]=4;}
            else _offset=new(0,owner._cue.Target.IsPlayer?-144:-96);
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=new SourceVisualSpriteFrame(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!_hidden,_frames.Frame.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=64,Height=64,VFlip=_frames.Frame.VFlip};
                if(_affine is null)return f;var m=_affine.Matrix;return f with{Affine=true,DoubleSize=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {if(_recipe.Callback=="AnimMeanLookEye")Eye(owner);else Block(owner);}
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {
            _frames.Step();
            // BeginAffineAnim applies a restarted absolute frame even when paused.
            // The new shared player already applied that frame in Eye's callback.
            if(_restarted)_restarted=false;else if(!_paused)_affine?.Step();
        }
        private void Eye(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 0:
                    owner._alphaA=_data[0];owner._alphaB=16-_data[0];_data[0]+=(short)(_data[1]!=0?-1:1);
                    if(_data[0] is 15 or 4)_data[1]^=1;
                    if(_data[2]++>70){owner._blendEnabled=false;owner._alphaA=owner._alphaB=0;_affine=new(_recipe.AffineAnimations[1]);_restarted=true;_data[2]=0;_hidden=_paused=true;_phase=1;}break;
                case 1:
                    if(_data[2]++>9){_hidden=_paused=false;if(_affine!.Ended)_phase=2;}break;
                case 2:
                    _offset=_data[3] switch{0 or 1=>new(1,0),2 or 3=>new(-1,0),4 or 5=>new(0,1),_=>new(0,-1)};
                    if(++_data[3]>7)_data[3]=0;
                    if(_data[4]++>15){_data[0]=16;_data[1]=0;owner._blendEnabled=true;owner._alphaA=16;owner._alphaB=0;_phase=3;}break;
                default:
                    owner._alphaA=_data[0];owner._alphaB=16-_data[0];
                    if(_data[1]++>1){_data[0]--;_data[1]=0;}
                    if(_data[0]==0)_hidden=true;
                    if(_data[0]<0){owner._blendEnabled=false;owner._alphaA=owner._alphaB=0;Finished=true;}break;
            }
        }
        private void Sound(SourceBattleAnimation owner,string symbol)=>owner._audio.Play(new(symbol,owner.AdjustPanning(63),owner._frame));
        private void Block(SourceBattleAnimation owner)
        {
            switch(_data[0])
            {
                case 0:
                    _offset=new(0,unchecked((short)(_offset.Y+10)));
                    if(_offset.Y>=0){Sound(owner,"SE_M_SKETCH");_offset=default;_data[0]++;}break;
                case 1:
                    _data[1]+=4;_offset=new(0,-(owner._script.SineTable[_data[1]]>>3));
                    if(_data[1]>127){Sound(owner,"SE_M_SKETCH");_data[1]=0;_offset=default;_data[0]++;}break;
                case 2:
                    _data[1]+=6;_offset=new(0,-(owner._script.SineTable[_data[1]]>>4));
                    if(_data[1]>127){_data[1]=0;_offset=default;_data[0]++;}break;
                case 3:
                    if(++_data[1]>8){Sound(owner,"SE_M_LEER");_data[1]=0;_data[0]++;}break;
                case 4:
                    if(++_data[1]>8){_data[1]=0;_data[2]++;_hidden=(_data[2]&1)!=0;if(_data[2]==7)Finished=true;}break;
            }
        }
    }
}
