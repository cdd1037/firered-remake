namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const int AdditionalMonPaletteTag=55125;
    private bool ValidateRolePlayCommand(SourceAnimationCommand c)
    {
        if(c.Op!="createvisualtask"||c.Args.FirstOrDefault()!="AnimTask_RolePlaySilhouette")return false;
        if(c.Args.Length!=2||Number(c.Args[1]) is <0 or >255)Fail(c,"Role Play silhouette takes only its source task priority.");
        if(_validatingInvokedProgram)
        {
            if(_additionalMonMaterial is not {} material||material.Geometry.Slot!=_cue.Actor||material.Geometry.YOffset is <0 or >64)
                Fail(c,"Additional-mon material requires its authenticated source face and base-species Y offset.");
            var commands=OverlayInvocationCommands();
            if(commands.Count(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_RolePlaySilhouette")!=1||
                commands.Any(x=>x.Op is "loadspritegfx" or "visible" or "invisible" or "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"||
                    x.Op=="createvisualtask"&&x.Args[0] is not("AnimTask_RolePlaySilhouette" or "AnimTask_BlendBattleAnimPal")||
                    x.Op=="createsprite"&&x.Args[0]!="gSimplePaletteBlendSpriteTemplate"))
                Fail(c,"Additional-mon silhouette requires its reviewed palette-only companion closure.");
            if(22+commands.Count(x=>x.Op=="createsprite")+1+_spriteScene.Sprites.Sum(x=>Math.Max(1,x.Frame.Subsprites.Length))>64||
                4+1+_spriteScene.Sprites.Count(x=>x.Frame.Affine||x.Frame.Scale!=256)>32)
                Fail(c,"Additional-mon silhouette exceeds source OAM or affine capacity.");
        }
        return true;
    }
    private bool TryCreateRolePlayTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_RolePlaySilhouette")return false;
        var material=_additionalMonMaterial??throw new InvalidOperationException("Authenticated additional-mon material is missing.");
        int slot=RequireFreeSpriteSlot(),palette=AllocateSpritePalette(AdditionalMonPaletteTag);
        if(palette==255)throw new InvalidOperationException("Additional-mon palette capacity exhausted.");
        ResetPaletteTransform(16+palette);
        ushort[] white=Enumerable.Repeat((ushort)0x7fff,16).ToArray();
        LoadSpritePaletteRange(palette,0,white);
        var task=new RolePlaySilhouetteTask(this,slot,palette,material);_tasks.Add(task);return true;
    }
    private sealed class RolePlaySilhouetteTask:VisualTask
    {
        private readonly AdditionalMonSilhouetteSprite _sprite;
        private short _phase,_delay,_alpha,_x=256,_y=256,_steps;
        public RolePlaySilhouetteTask(SourceBattleAnimation o,int slot,int palette,SourceBattlerFormMaterial material)
        {
            int actor=o._cue.Actor.Value;var origin=o._battlers[actor].Base(o._doubles);
            _sprite=new(slot,palette,material,new(origin.X+(o._cue.Actor.IsPlayer?-20:20),origin.Y+material.Geometry.YOffset),o.BattlerBackgroundPriority(actor));
            o._sprites.Add(_sprite);o.SetAlphaBlend(0,63,0,16);
        }
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:
                    if(_delay++>1)
                    {
                        _delay=0;_alpha++;o._alphaA=_alpha;o._alphaB=16-_alpha;
                        if(_alpha==10)_phase=1;
                    }
                    return;
                case 1:
                    _x-=16;_y+=128;_sprite.SetMatrix(_x,_y);
                    if(++_steps!=9)return;
                    _sprite.ResetAndDestroy();o._sprites.Remove(_sprite);o.FreeSpritePalette(AdditionalMonPaletteTag);_phase=2;return;
                case 2:o.ClearAlphaBlend();Finished=true;return;
            }
        }
    }
    private sealed class AdditionalMonSilhouetteSprite:VisualSprite
    {
        private readonly string _asset;private readonly SourcePixelAsset _material;private readonly int _palette,_priority;
        private readonly PixelPoint _position;private SourceAffineMatrix _matrix=new(256,0,0,256);private bool _double;
        public AdditionalMonSilhouetteSprite(int slot,int palette,SourceBattlerFormMaterial input,PixelPoint position,int priority):base(slot,5)
        {
            _asset=input.Geometry.Asset;_position=position;_palette=palette;_priority=priority;
            // FillPalette(WHITE) makes OT/shiny colors and Spinda's nonzero-index
            // recoloring irrelevant. Retain exactly the authenticated shape.
            byte[] indices=input.Pixels.Pixels.Select(p=>(byte)((p&0x8000)==0?0:1)).ToArray();
            _material=new(64,64,indices.Select(i=>i==0?(ushort)0:(ushort)0xffff).ToArray())
                {PaletteIndices=indices,Palette=Enumerable.Repeat((ushort)0x7fff,16).ToArray(),PaletteTag=AdditionalMonPaletteTag};
        }
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o){} // Both source animation programs are END-only.
        public void SetMatrix(int x,int y){_double=true;_matrix=SourceAffineMatrix.FromScaleRotation(x,y,0);}
        public void ResetAndDestroy(){_matrix=SourceAffineMatrix.FromScaleRotation(256,256,0);Finished=true;}
        public override SourceVisualSpriteFrame Frame=>new(Slot,"AnimRolePlaySilhouette",_position,default,0,true,false,true,256,_asset,Subpriority)
            {Width=64,Height=64,Material=_material,PaletteSlot=_palette,OamPriority=_priority,Affine=true,DoubleSize=_double,
                MatrixA=_matrix.A,MatrixB=_matrix.B,MatrixC=_matrix.C,MatrixD=_matrix.D};
    }
}
