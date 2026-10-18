namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string RolloutMud="gRolloutMudSpriteTemplate",RolloutRock="gRolloutRockSpriteTemplate";
    private bool ValidateRolloutCommand(SourceAnimationCommand c)
    {
        if(c.Op!="createvisualtask"||c.Args.FirstOrDefault()!="AnimTask_Rollout")return false;
        if(c.Args.Length!=2||Number(c.Args[1]) is <0 or >255)Fail(c,"Rollout task takes only source priority.");
        if(_validatingInvokedProgram&&_cue.RolloutCounter is not (>=1 and <=5))Fail(c,"Rollout requires the owned postdecrement turn counter.");
        var reachable=_script.Commands.Concat(_script.Subroutines.Values.SelectMany(s=>s)).ToArray();
        // The raw destructor locates the sole Rollout task by callback function.
        // Reject competing owners and counted use of these raw-only templates.
        if(reachable.Count(x=>x.Op=="createvisualtask"&&x.Args.FirstOrDefault()=="AnimTask_Rollout")!=1||
            reachable.Any(x=>x.Op=="createsprite"&&x.Args.FirstOrDefault() is RolloutMud or RolloutRock)||
            reachable.Any(x=>x.Op is "call" or "return" or "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"))
            Fail(c,"Rollout raw particles require one unambiguous task owner.");
        foreach(string name in new[]{RolloutMud,RolloutRock})
        {
            if(!_script.SpriteDependencies.Contains(name)||!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Rollout raw child recipe is missing.");
            r=_script.SpriteRecipes[name];int size=name==RolloutMud?8:32;
            if(r.Callback!="AnimRolloutParticle"||r.Asset!=(name==RolloutMud?"mud-sand":"rocks")||r.Width!=size||r.Height!=size||
                r.Blend||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.Animations.Length!=1||
                !r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||
                !r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Rollout raw source template differs.");
        }
        foreach(string sound in new[]{"SE_M_HEADBUTT","SE_M_DIG"})if(!_script.SoundEffects.ContainsKey(sound))Fail(c,"Rollout callback sound dependency is missing.");
        return true;
    }
    private bool TryCreateRolloutTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_Rollout")return false;
        if(!_loaded.Contains("ANIM_TAG_MUD_SAND")||!_loaded.Contains("ANIM_TAG_ROCKS"))Fail(c,"Rollout child graphics have not been loaded.");
        _tasks.Add(new RolloutTask(this));return true;
    }
    private sealed class RolloutTask:VisualTask
    {
        private readonly short[] _d=new short[16];
        public RolloutTask(SourceBattleAnimation o)
        {
            var a=o._battlers[o._cue.Actor.Value].Base(o._doubles);var t=o._battlers[o._cue.Target.Value].Base(o._doubles);
            int y=a.Y+24,targetY=o._cue.Actor.Partner==o._cue.Target?y:t.Y+24,counter=o._cue.RolloutCounter!.Value;
            _d[8]=(short)(counter==1?32:48-counter*8);_d[12]=1;_d[10]=(short)((_d[8]>>3)-1);
            _d[2]=(short)(a.X*8);_d[3]=(short)(y*8);_d[4]=(short)((t.X-a.X)*8/_d[8]);_d[5]=(short)((targetY-y)*8/_d[8]);
            _d[13]=(short)o.AdjustPanning(-64);_d[14]=(short)((o.AdjustPanning(63)-_d[13])/_d[8]);
            _d[1]=(short)counter;_d[15]=(short)o._cue.Actor.Value;
        }
        public override void Step(SourceBattleAnimation o)
        {
            switch(_d[0])
            {
                case 0:
                    _d[6]=unchecked((short)(_d[6]-_d[4]));_d[7]=unchecked((short)(_d[7]-_d[5]));Offset(o);
                    if(++_d[9]==10){_d[11]=20;_d[0]++;}
                    o._audio.Play(new("SE_M_HEADBUTT",unchecked((sbyte)_d[13]),o._frame));break;
                case 1:if(--_d[11]==0)_d[0]++;break;
                case 2:
                    if(--_d[9]!=0){_d[6]=unchecked((short)(_d[6]+_d[4]));_d[7]=unchecked((short)(_d[7]+_d[5]));}
                    else{_d[6]=0;_d[7]=0;_d[0]++;}Offset(o);break;
                case 3:
                    _d[2]=unchecked((short)(_d[2]+_d[4]));_d[3]=unchecked((short)(_d[3]+_d[5]));
                    if(++_d[9]>=_d[10]){_d[9]=0;CreateParticle(o);_d[13]=unchecked((short)(_d[13]+_d[14]));o._audio.Play(new("SE_M_DIG",unchecked((sbyte)_d[13]),o._frame));}
                    if(--_d[8]==0)_d[0]++;break;
                case 4:if(_d[11]==0)Finished=true;break;
            }
        }
        private void Offset(SourceBattleAnimation o)=>o._offsets[_d[15]]=new(_d[6]>>3,_d[7]>>3);
        private void CreateParticle(SourceBattleAnimation o)
        {
            int tile=_d[1] switch{1=>0,2 or 3=>80,4=>64,_=>48};string name=_d[1]==1?RolloutMud:RolloutRock;
            // Source intermediates are u16; the sprite coordinates narrow to s16.
            int x=unchecked((ushort)((_d[2]>>3)+_d[12]*4)),y=unchecked((ushort)(_d[3]>>3));
            int slot=o.FindFreeSpriteSlot();
            if(slot!=SourceSpriteRegistry<VisualSprite>.Capacity)
            {
                var start=new PixelPoint(unchecked((short)x),unchecked((short)y));
                int endX=unchecked((short)(_d[12]*20+x+_d[1]*3));
                o._sprites.Add(new RolloutParticle(slot,o._script.SpriteRecipes[name],this,start,endX-start.X,tile,-16-_d[1]*2));_d[11]++;
            }
            // Allocation failure still changes the next particle's side.
            _d[12]=unchecked((short)-_d[12]);
        }
        public void Release()=>_d[11]--;
    }
    private sealed class RolloutParticle(int slot,SourceSpriteRecipe recipe,RolloutTask task,PixelPoint position,int dx,int tile,int height):VisualSprite(slot,35)
    {
        private readonly HorizontalArc _arc=new(dx,0,18,height);private PixelPoint _offset;
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,recipe.Template,position,_offset,tile,true,false,false,256,recipe.Asset,Subpriority){Width=recipe.Width,Height=recipe.Height};
        public override void Step(SourceBattleAnimation o)
        {if(_arc.Step(o._script.SineTable,out var offset)){task.Release();Finished=true;}else _offset=offset;}
    }
}
