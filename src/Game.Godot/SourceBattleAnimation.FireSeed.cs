namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateFireSeedOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault() is not ("gFirePlumeSpriteTemplate" or "gLargeFlameSpriteTemplate" or "gLargeFlameScatterSpriteTemplate" or "gLeechSeedSpriteTemplate"))return false;
        bool seed=a[0]=="gLeechSeedSpriteTemplate",plume=a[0]=="gFirePlumeSpriteTemplate",affine=a[0]=="gLargeFlameSpriteTemplate";
        if(a.Length!=9||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(command,"Fire/seed recipe or operand count differs.");
        var recipe=_script.SpriteRecipes[a[0]];
        if(recipe.Callback!=(seed?"AnimLeechSeed":plume?"AnimFirePlume":"AnimLargeFlame")||recipe.Asset!=(seed?"seed":plume?"fire-plume":"fire")||
            recipe.Width!=(seed?16:32)||recipe.Height!=(seed?16:32)||recipe.Blend||recipe.AffineDoubleSize||recipe.FrameLoops.Any(p=>p.Length!=0))Fail(command,"Fire/seed source template differs.");
        SourceSpriteAnimFrame F(int tile,int duration)=>new(tile,duration,false,false);
        SourceSpriteAnimFrame[][] frames=seed?[[F(0,1)],[F(4,7),F(8,7)]]:[Enumerable.Range(0,plume?5:8).Select(i=>F(i*16,plume?5:3)).ToArray()];
        int[] loops=seed?[-1,0]:[0];
        SourceSpriteAffineCommand[][] affines=affine?[[new("frame",50,256,0,0,0),new("frame",32,0,0,7,0),new("end",0,0,0,0,0)]]:[];
        if(recipe.Animations.Length!=frames.Length||!recipe.LoopStarts.SequenceEqual(loops)||recipe.AffineAnimations.Length!=affines.Length)Fail(command,"Fire/seed source animation tables differ.");
        for(int i=0;i<frames.Length;i++)if(!recipe.Animations[i].SequenceEqual(frames[i]))Fail(command,"Fire/seed source frame program differs.");
        for(int i=0;i<affines.Length;i++)if(!recipe.AffineAnimations[i].SequenceEqual(affines[i]))Fail(command,"Large flame source affine program differs.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(command,"Fire/seed source-width operands differ.");
        // Signed source durations must terminate before their counter wraps; arc
        // initialization additionally divides by this duration.
        if(Number(a[seed?7:5])<=0)Fail(command,"Fire/seed duration must be positive.");
        if(seed&&_script.SineTable.Length!=320)Fail(command,"Leech Seed requires the source sine table.");
        return true;
    }
    private bool TryCreateFireSeedSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not ("gFirePlumeSpriteTemplate" or "gLargeFlameSpriteTemplate" or "gLargeFlameScatterSpriteTemplate" or "gLeechSeedSpriteTemplate"))return false;
        var recipe=_script.SpriteRecipes[command.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Fire/seed graphics have not been loaded.");
        _sprites.Add(new FireSeedSprite(slot,priority,recipe,Resolve(command.Args[1]),this));return true;
    }
    private sealed class FireSeedSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private readonly HorizontalArc? _arc;private readonly PixelPoint _position;private PixelPoint _offset;
        private readonly short _lifetime,_moveUntil,_vx,_vy;private short _ticks,_remaining;private int _phase;
        private bool _invisible,_beginFrames;
        public FireSeedSprite(int slot,int priority,SourceSpriteRecipe recipe,AnimationSlot initialBattler,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=owner._animationArgs;
            bool seed=recipe.Callback=="AnimLeechSeed",plume=recipe.Callback=="AnimFirePlume";
            var center=owner._battlers[(seed||plume?owner._cue.Actor:initialBattler).Value].Center(owner._doubles,true);
            int direction=seed?owner.InitialXDirection():owner._cue.Actor.IsPlayer?1:-1;
            _position=new(unchecked((short)(center.X+direction*a[0])),unchecked((short)(center.Y+a[1])));
            if(seed)
            {
                if(!owner._cue.Actor.IsPlayer)a[2]=unchecked((short)-a[2]);
                // Source destinations use the unadjusted X/Y coordinates, while
                // InitSpritePosToAnimAttacker respects the actor's picture offset.
                var target=owner._battlers[owner._cue.Target.Value].Base(owner._doubles);
                int x=unchecked((short)(target.X+a[2])),y=unchecked((short)(target.Y+a[3]));
                _arc=new(x-_position.X,y-_position.Y,a[4],a[5]);
            }
            else
            {
                _lifetime=a[2];_moveUntil=a[3];_vx=unchecked((short)(direction*(plume?1:-1)*a[4]));_vy=a[5];
                if(recipe.AffineAnimations.Length!=0)_affine=new(recipe.AffineAnimations[0]);
            }
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=_frames.Frame;var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);
                return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,false,256,_recipe.Asset,Subpriority)
                {Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {
            // StartSpriteAnim performs BeginAnim this same frame. The shared
            // player's constructor already consumed that initial frame command.
            if(_beginFrames)_beginFrames=false;else _frames.Step();
            _affine?.Step();
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_arc is null)
            {
                _ticks=unchecked((short)(_ticks+1));
                if(_ticks<_moveUntil)_offset=new(unchecked((short)(_offset.X+_vx)),unchecked((short)(_offset.Y+_vy)));
                if(_ticks==_lifetime)Finished=true;
                return;
            }
            switch(_phase)
            {
                case 0:
                    if(_arc.Step(owner._script.SineTable,out var offset)){_invisible=true;_remaining=10;_phase=1;}else _offset=offset;
                    break;
                case 1:
                    if(_remaining>0)_remaining--;else _phase=2;
                    break;
                case 2:
                    _invisible=false;_frames=new(_recipe,1);_beginFrames=true;_remaining=60;_phase=3;
                    break;
                case 3:
                    if(_remaining>0)_remaining--;else _phase=4;
                    break;
                case 4:Finished=true;break;
            }
        }
    }
}
