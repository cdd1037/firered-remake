using System.Buffers.Binary;
using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public sealed record SourceBattlerAffine(bool DoubleSize,int XScale,int YScale)
{ public int MatrixB {get;init;} public int MatrixC {get;init;} }
public sealed record SourceMetallicFrame(bool WindowEnabled,bool BackgroundLoaded,int ScrollX,int ScrollY,int MaskSlot,bool MaskVisible,int PriorityPartner);
public sealed class SourceMetallicMaterial
{
    private readonly byte[] _data;
    internal SourceMetallicMaterial(byte[] data){if(data.Length!=6176)throw new InvalidDataException("Metallic material shape differs.");_data=(byte[])data.Clone();}
    public ushort Pixel(int x,int y)
    {
        x&=255;y&=255;int entry=BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(4096+((y>>3)*32+(x>>3))*2));
        int tx=x&7,ty=y&7;if((entry&0x400)!=0)tx=7-tx;if((entry&0x800)!=0)ty=7-ty;
        int tile=entry&1023,index=_data[(tile/8*8+ty)*64+(tile%8*8+tx)];
        return index==0?(ushort)0:(ushort)(0x8000|BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(6144+index*2)));
    }
}
public static class SourceStatMoveCatalog
{

    public static bool Supports(int move)=>move is 74 or 106 or 334 or 110 or 107;
    public static (IReadOnlyDictionary<int,SourceAnimationScript> Scripts,SourceMetallicMaterial Material) Read(string folder)
    {
        var bytes=System.IO.File.ReadAllBytes(Path.Combine(folder,"manifest.json"));
        using var doc=JsonDocument.Parse(bytes);
        SourceAnimationCommand[] Commands(JsonElement row)=>row.EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray();
        var subs=doc.RootElement.GetProperty("subroutines").EnumerateObject().ToDictionary(p=>p.Name,p=>Commands(p.Value));
        var scripts=doc.RootElement.GetProperty("scripts").EnumerateArray().Select(r=>new SourceAnimationScript(r.GetProperty("moveId").GetInt32(),r.GetProperty("label").GetString()!,Commands(r.GetProperty("commands"))){Subroutines=subs}).ToDictionary(s=>s.MoveId);
        var data=System.IO.File.ReadAllBytes(Path.Combine(folder,"metal-shine.bin"));
        return (scripts,new(data));
    }
}
public sealed partial class SourceBattleAnimation
{
    private SourceMetallicFrame? _metallic;
    private readonly HashSet<int> _greyscale=[];
    private readonly bool[] _affineMode=new bool[4],_affineOamMode=new bool[4];
    private readonly int[] _xScale=Enumerable.Repeat(256,4).ToArray(),_yScale=Enumerable.Repeat(256,4).ToArray();
    private readonly int[] _matrixB=new int[4],_matrixC=new int[4];
    private PaletteFade? _fade;
    private SourceAnimationCommand[]? _subroutine;
    private int _returnPc;
    private SourceAnimationCommand[] Program=>_subroutine??_script.Commands;
    private bool ValidateStatMoveOperand(SourceAnimationCommand c)
    {
        string[]? expected=c.Op switch
        {
            "call"=>["GrowthEffect"],"return"=>[],
            "createvisualtask"=>c.Args.FirstOrDefault() switch
            {
                "AnimTask_Minimize"=>["AnimTask_Minimize","2"],
                "AnimTask_Withdraw"=>["AnimTask_Withdraw","5"],
                "AnimTask_MetallicShine"=>["AnimTask_MetallicShine","5","0","0","0"],
                "AnimTask_BlendColorCycle"=>["AnimTask_BlendColorCycle","2","F_PAL_ATTACKER","0","2","0","8","RGB_WHITE"],
                "AnimTask_ScaleMonAndRestore"=>["AnimTask_ScaleMonAndRestore","5","-3","-3","16","ANIM_ATTACKER","0"],_=>null
            },
            "createsprite" when c.Args.FirstOrDefault()=="gComplexPaletteBlendSpriteTemplate"=>["gComplexPaletteBlendSpriteTemplate","ANIM_ATTACKER","2","F_PAL_BG","8","2","RGB_WHITEALPHA","14","RGB_WHITEALPHA","0"],_=>null
        };
        if(expected is null)return false;
        if(!c.Args.SequenceEqual(expected)||!SourceStatMoveCatalog.Supports(_script.MoveId))Fail(c,"Unadmitted stat move callback arguments/root.");
        return true;
    }
    private sealed class MetallicTask:VisualTask
    {
        private int _scroll,_passes;
        private readonly int _slot;private readonly bool _restore;
        public MetallicTask(SourceBattleAnimation owner,bool restore,bool tint,ushort color)
        {
            _restore=restore;_slot=owner._cue.Actor.Value;var b=owner._battlers[_slot];var center=b.DisplayCenter??b.Center(owner._doubles);
            int partner=owner._doubles&&_slot is 0 or 3&&owner._battlers[_slot^2].Visible?_slot^2:-1;
            owner._metallic=new(true,true,96-center.X,32-center.Y,_slot,b.Visible,partner);
            if(tint)owner.BlendPalettes(1u<<(16+_slot),11,color);else owner.SetBattlerGreyscaleOrOriginal(_slot,false);owner._blendEnabled=true;owner._alphaA=8;owner._alphaB=12;
        }
        public override void Step(SourceBattleAnimation owner)
        {
            var mask=owner._metallic!;owner._metallic=mask with{ScrollX=(short)(mask.ScrollX-4),MaskVisible=_passes<2};
            if((_scroll+=4)!=128)return;
            _scroll=0;owner._metallic=owner._metallic with{ScrollX=(short)(owner._metallic.ScrollX+128)};
            if(++_passes==2)
            {if(_restore)owner.SetBattlerGreyscaleOrOriginal(_slot,true);owner._metallic=owner._metallic with{BackgroundLoaded=false,PriorityPartner=-1};}
            else if(_passes==3)
            {owner._metallic=owner._metallic with{WindowEnabled=false};owner._blendEnabled=false;owner._alphaA=owner._alphaB=0;Finished=true;}
        }
    }
    private sealed class ScaleMonTask:VisualTask
    {
        private readonly int _slot;private int _dx,_dy,_left,_restore;
        public ScaleMonTask(SourceBattleAnimation owner,int[] args)
        {_slot=args[3]==0?owner._cue.Actor.Value:owner._cue.Target.Value;_dx=args[0];_dy=args[1];_left=_restore=args[2];owner._affineMode[_slot]=true;owner._xScale[_slot]=owner._yScale[_slot]=256;}
        public override void Step(SourceBattleAnimation owner)
        {
            owner._matrixB[_slot]=owner._matrixC[_slot]=0;
            owner._xScale[_slot]=(short)(owner._xScale[_slot]+_dx);owner._yScale[_slot]=(short)(owner._yScale[_slot]+_dy);
            if(--_left!=0)return;
            if(_restore>0){_dx=-_dx;_dy=-_dy;_left=_restore;_restore=0;}
            else{owner._xScale[_slot]=owner._yScale[_slot]=256;owner._matrixB[_slot]=owner._matrixC[_slot]=0;owner._affineMode[_slot]=false;Finished=true;}
        }
    }
    // Original normal palette fade visits BG and OBJ halves on alternate calls,
    // advances by 2 only after the OBJ half, and retains five finishing calls.
    private sealed class PaletteFade(uint mask,int from,int to,ushort color,int delay=0)
    {
        private readonly uint _mask=mask;private readonly int _target=to;private readonly ushort _color=color;
        private readonly int _delay=Math.Max(0,delay),_delta=2+Math.Max(0,-delay);
        private int _y=from,_toggle,_finishing=-1,_delayCounter=Math.Max(0,delay);
        public bool Active{get;private set;}=true;
        public void Update(SourceBattleAnimation owner)
        {
            if(!Active)return;
            if(_finishing>=0){if(_finishing++==4)Active=false;return;}
            if(_toggle==0){if(_delayCounter<_delay){_delayCounter++;return;}_delayCounter=0;}
            owner.BlendPalettes(_mask&(_toggle==0?0xffffu:0xffff0000u),_y,_color);
            _toggle^=1;
            if(_toggle!=0)return;
            if(_y==_target)_finishing=0;
            else _y=_y<_target?Math.Min(_y+_delta,_target):Math.Max(_y-_delta,_target);
        }
    }
    private sealed class BlendCycleTask:VisualTask
    {
        private readonly uint _mask;private readonly int _delay,_initial,_target;private readonly ushort _color;
        private int _remaining;private bool _restore;
        public BlendCycleTask(SourceBattleAnimation owner,int[] args)
        {_mask=owner.SelectPalettes(args[0]);_delay=args[1];_remaining=args[2];_initial=args[3];_target=args[4];_color=(ushort)args[5];Begin(owner,0,_target);}
        private void Begin(SourceBattleAnimation owner,int from,int to)
        {if(owner._fade?.Active!=true){owner._fade=new(_mask,from,to,_color,_delay);owner._fade.Update(owner);}_remaining--;_restore=!_restore;}
        public override void Step(SourceBattleAnimation owner)
        {
            if(owner._fade?.Active==true)return;
            if(_remaining<=0){Finished=true;return;}
            Begin(owner,_restore?_target:_initial,_remaining==1?0:_restore?_initial:_target);
        }
    }
    private sealed class ComplexPaletteSprite:VisualSprite
    {
        private int _delay,_toggle,_remaining,_phase;
        private readonly uint _mask;private readonly ushort _firstColor,_secondColor;private readonly int _firstBlend,_secondBlend;
        public ComplexPaletteSprite(int slot,int priority,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _mask=owner.SelectPalettes(args[0]);_delay=_toggle=args[1];_remaining=args[2];_firstColor=(ushort)args[3];_firstBlend=args[4];_secondColor=(ushort)args[5];_secondBlend=args[6];
            owner.BlendPalettes(_mask,_firstBlend,_firstColor);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gComplexPaletteBlendSpriteTemplate",default,default,0,false,false,false,256,null,Subpriority);
        public override void Step(SourceBattleAnimation owner)
        {
            if(_phase==1){if(owner._fade?.Active!=true){owner.BlendPalettes(_mask,0,0);Finished=true;}return;}
            if(_delay>0){_delay--;return;}if(owner._fade?.Active==true)return;
            if(_remaining==0){_phase=1;return;}
            owner.BlendPalettes(_mask,(_toggle&256)!=0?_firstBlend:_secondBlend,(_toggle&256)!=0?_firstColor:_secondColor);_toggle^=256;_delay=_toggle&255;_remaining--;
        }
    }
}
