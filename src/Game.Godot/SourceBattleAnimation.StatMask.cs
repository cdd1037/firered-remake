using System.Buffers.Binary;
using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

// Register state for the source's BG1 layer, gated by a copied battler OBJ window.
// General_StatsChange never changes the battle scene option or advances a rules event.
public sealed record SourceStatMaskFrame(bool WindowEnabled,bool BackgroundLoaded,int Palette,int ScrollX,int ScrollY,
    int MaskSlot,bool MaskVisible,int PriorityPartner);
public sealed class SourceStatMaskMaterial
{
    private readonly byte[] _data;
    internal SourceStatMaskMaterial(byte[] data){if(data.Length!=8448)throw new InvalidDataException("Stat material shape differs.");_data=(byte[])data.Clone();}
    public ushort Pixel(int direction,int palette,int x,int y)
    {
        if(direction is <0 or >1||palette is <1 or >8)throw new InvalidDataException("Stat mask selection differs.");
        x&=255;y&=255;
        int at=4096+direction*2048+((y>>3)*32+(x>>3))*2;
        int entry=BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(at));
        int tx=x&7,ty=y&7;if((entry&0x400)!=0)tx=7-tx;if((entry&0x800)!=0)ty=7-ty;
        int tile=entry&1023,index=_data[(tile/16*8+ty)*128+(tile%16*8+tx)];
        if(index==0)return 0;
        return (ushort)(0x8000|BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(8192+(palette-1)*32+index*2)));
    }
}
public static class SourceStatAnimationCatalog
{

    public static (SourceAnimationScript Script,SourceStatMaskMaterial Material) Read(string folder)
    {
        var bytes=System.IO.File.ReadAllBytes(Path.Combine(folder,"manifest.json"));
        using var doc=JsonDocument.Parse(bytes);var row=doc.RootElement.GetProperty("script");
        var script=new SourceAnimationScript(0,row.GetProperty("label").GetString()!,row.GetProperty("commands").EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray());
        var data=System.IO.File.ReadAllBytes(Path.Combine(folder,"stat-mask.bin"));
        return (script,new(data));
    }
}
public sealed partial class SourceBattleAnimation
{
    private SourceStatMaskFrame? _statMask;
    private void ValidateStatRoot()
    {
        bool general=_script.Label=="General_StatsChange";
        if(!general&&(_cue.StatAnimationArgument is not null||_script.Commands.Any(c=>c.Args.FirstOrDefault()=="AnimTask_StatsChange")))throw new InvalidDataException("Stat callback requires exact General_StatsChange root.");
        if(!general)return;
        if(_script.MoveId!=0||_cue.Actor!=_cue.Target||_cue.StatAnimationArgument is not {} arg||!ValidStatArgument(arg)||!IsSourceBattlerVisible(_cue.Actor.Value)||
            _script.Commands.Length!=3||_script.Commands[0].Op!="createvisualtask"||!_script.Commands[0].Args.SequenceEqual(new[]{"AnimTask_StatsChange","5"})||
            _script.Commands[1].Op!="waitforvisualfinish"||_script.Commands[1].Args.Length!=0||_script.Commands[2].Op!="end"||_script.Commands[2].Args.Length!=0)
            throw new InvalidDataException("General stat source script/argument differs.");
    }
    public static bool ValidStatArgument(int arg)=>arg is >=15 and <=28 or >=39 and <=52 or >=55 and <=58;
    // Original mapping in AnimTask_StatsChange. palette IDs follow the resource switch
    // in StatsChangeAnimation_Step2; every stat shares the same task state machine.
    private sealed class StatMaskTask:VisualTask
    {
        private readonly int _direction,_palette,_peak,_hold,_slot;
        private int _phase,_counter,_coefficient,_held,_createdFrame=-1;
        public StatMaskTask(SourceBattleAnimation owner)
        {
            int arg=owner._cue.StatAnimationArgument!.Value;_slot=owner._cue.Actor.Value;
            bool sharp=arg is >=39 and <=52 or 56 or 58;
            _direction=arg is >=22 and <=28 or >=46 and <=52 or 57 or 58?1:0;
            int stat=arg>=55?7:(arg-(sharp?(_direction==1?46:39):(_direction==1?22:15)));
            _palette=new[]{2,1,4,7,8,3,6,5}[stat];_peak=sharp?13:10;_hold=sharp?30:20;
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_phase==0)
            {
                int partner=owner._doubles&&_slot is 0 or 3&&owner.IsSourceBattlerVisible(_slot^2)?_slot^2:-1;
                owner._statMask=new(true,false,0,0,0,_slot,false,partner);owner._blendEnabled=true;owner._alphaA=0;owner._alphaB=16;_phase++;return;
            }
            if(_phase==1)
            {
                _createdFrame=owner._frame;owner._statMask=owner._statMask! with{BackgroundLoaded=true,Palette=_palette,ScrollX=_direction==1?64:0};
                owner._audio.Play(new(_direction==1?"SE_M_STAT_DECREASE":"SE_M_STAT_INCREASE",owner._cue.Actor.IsPlayer?-64:64,owner._frame));_phase++;return;
            }
            var mask=owner._statMask!;
            owner._statMask=mask with{ScrollY=(short)(mask.ScrollY+(_direction==1?-3:3)),MaskVisible=owner._frame>_createdFrame};
            switch(_phase)
            {
                case 2:
                    if(_counter++>0){_counter=0;_coefficient++;owner._alphaA=_coefficient;owner._alphaB=16-_coefficient;if(_coefficient==_peak)_phase++;}break;
                case 3:if(++_held==_hold)_phase++;break;
                case 4:
                    if(_counter++>0){_counter=0;_coefficient--;owner._alphaA=_coefficient;owner._alphaB=16-_coefficient;if(_coefficient==0){owner._statMask=owner._statMask! with{BackgroundLoaded=false};_phase++;}}break;
                case 5:
                    owner._statMask=owner._statMask! with{WindowEnabled=false,PriorityPartner=-1};owner._blendEnabled=false;owner._alphaA=owner._alphaB=0;Finished=true;break;
            }
        }
    }
}
