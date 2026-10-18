namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private sealed class LeafBladeTask:VisualTask
    {
        private readonly short[] _data=new short[16];private readonly LeafBladeSprite _leader;
        public LeafBladeTask(SourceBattleAnimation o)
        {
            var target=o._battlers[o._cue.Target.Value];var p=target.Center(o._doubles,true);
            _data[4]=(short)(target.Subpriority-1);_data[6]=(short)p.X;_data[7]=(short)p.Y;_data[10]=(short)target.SourceWidth;_data[11]=(short)target.SourceHeight;
            _data[5]=(short)(target.Slot.IsPlayer?-1:1);_data[9]=(short)(56-_data[5]*64);_data[8]=(short)(_data[7]-_data[9]+_data[6]);
            int slot=o.FindFreeSpriteSlot();if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)throw new InvalidDataException("Leaf Blade leader allocation exceeded its reviewed source capacity.");
            _data[2]=(short)slot;_leader=new(slot,unchecked((byte)_data[4]),o._script.SpriteRecipes[LeafBladeTemplate],new(_data[8],_data[9]),null,0);
            _leader.BeginArc(new(_data[6]-(_data[10]/2+10)*_data[5],_data[7]+(_data[11]/2+10)*_data[5]));o._sprites.Add(_leader);o._oamY[slot]=0;
        }
        public override void Step(SourceBattleAnimation o)
        {
            int state=_data[0];
            switch(state)
            {
                case 0:case 2:case 4:case 6:case 8:case 10:
                    EmitTrail(o);if(_leader.Translate(o)){_data[15]=(short)(state+1);_data[0]=255;}break;
                case 1:case 3:case 5:case 7:case 9:case 11:
                    PixelPoint end;
                    switch(state)
                    {
                        case 1:end=new(_data[6],_data[7]);_data[4]+=2;_data[3]=1;break;
                        case 3:end=new(_data[6]-(_data[10]/2+10)*_data[5],_data[7]-(_data[11]/2+10)*_data[5]);_data[3]=2;break;
                        case 5:end=new(_data[6]+(_data[10]/2+10)*_data[5],_data[7]+(_data[11]/2+10)*_data[5]);_data[4]-=2;_data[3]=3;break;
                        case 7:end=new(_data[6],_data[7]);_data[4]+=2;_data[3]=4;break;
                        case 9:end=new(_data[6]-(_data[10]/2+10)*_data[5],_data[7]+(_data[11]/2+10)*_data[5]);_data[3]=5;break;
                        default:end=new(_data[8],_data[9]);_data[4]-=2;_data[3]=6;break;
                    }
                    _leader.CommitPosition();_leader.Priority=unchecked((byte)_data[4]);_leader.SelectAnimation(_data[3]);_leader.BeginArc(end);_data[0]++;break;
                case 12:
                    EmitTrail(o);if(_leader.Translate(o)){_leader.Retire();o._sprites.Remove(_leader);_data[0]++;}break;
                case 13:if(_data[12]==0)Finished=true;break;
                case 255:if(++_data[1]>5){_data[1]=0;_data[0]=_data[15];}break;
            }
        }
        private void EmitTrail(SourceBattleAnimation o)
        {
            if(++_data[14]<=0)return;_data[14]=0;int slot=o.FindFreeSpriteSlot();if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)return;
            var child=new LeafBladeSprite(slot,unchecked((byte)_data[4]),o._script.SpriteRecipes[LeafBladeTemplate],_leader.ActualPosition,this,(short)(_data[13]&1));
            child.SelectAnimation(_data[3]);o._sprites.Add(child);o._oamY[slot]=0;_data[12]++;_data[13]++;
        }
        public void ChildEnded()=>_data[12]--;
    }
    private sealed class LeafBladeSprite(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint position,LeafBladeTask? parent,short initialBlink):VisualSprite(slot,priority)
    {
        private PixelPoint _position=position,_offset;private HorizontalArc? _arc;private SourceSpriteFrameProgram? _frames;private short _blink=initialBlink,_toggles;private int _animation;private bool _begin=true,_invisible;
        public int Priority{get;set;}=priority;public override bool Counted=>false;
        public PixelPoint ActualPosition=>new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames?.Frame??recipe.Animations[0][0];return new(Slot,recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,false,256,recipe.Asset,Priority){Width=16,Height=16,VFlip=f.VFlip};}}
        public void SelectAnimation(int animation){_animation=animation;_begin=true;}
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_begin){_frames=new(recipe,_animation);_begin=false;}else _frames!.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(parent is null)return;
            if(++_blink>1){_blink=0;_invisible=!_invisible;if(++_toggles>8){parent.ChildEnded();Finished=true;}}
        }
        public void BeginArc(PixelPoint end)=>_arc=new(unchecked((short)(end.X-_position.X)),unchecked((short)(end.Y-_position.Y)),10,end.Y<_position.Y?-8:8);
        public bool Translate(SourceBattleAnimation o){if(_arc!.Step(o._script.SineTable,out var offset))return true;_offset=offset;return false;}
        public void CommitPosition(){_position=ActualPosition;_offset=default;}
        public void Retire()=>Finished=true;
    }
}
