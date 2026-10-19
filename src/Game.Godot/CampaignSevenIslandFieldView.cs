using System.Globalization;
using Game.Application;
using Game.Domain.Pokemon;
using Godot;

public partial class CampaignView
{
    private long _sevenIslandShakeSequence=-1;
    private (int Horizontal,int Vertical,int Shakes,int Delay)? _sevenIslandShake;
    private double _sevenIslandShakeFrames;
    private Vector2 SevenIslandCameraOffset
    {
        get
        {
            if(_sevenIslandShake is not {} shake)return Vector2.Zero;
            var p=SevenIslandFieldPresentation.ShakeOffset(shake.Horizontal,shake.Vertical,shake.Shakes,shake.Delay,(int)Math.Floor(_sevenIslandShakeFrames+1e-7));
            return new(p.X,p.Y);
        }
    }
    private void SynchronizeSevenIslandFieldPresentation(CampaignState? before,CampaignState after)
    {
        if(before?.World.Player.MapId!=after.World.Player.MapId||before?.FieldWeather!=after.FieldWeather)
        {
            ClearFieldShadeTextures();_sevenIslandShake=null;_sevenIslandShakeFrames=0;
        }
        var effects=after.PresentationEffects.Where(e=>e.Opcode=="TanobyShakeResolved").OrderBy(e=>e.Sequence).ToArray();
        if(before is null)
        {
            _sevenIslandShakeSequence=effects.LastOrDefault()?.Sequence??-1;return; // Restoring a stable field does not replay historical effects.
        }
        foreach(var effect in effects.Where(e=>e.Sequence>_sevenIslandShakeSequence))
        {
            if(after.World.Player.MapId.Value!="map.seven-island-sevault-canyon-tanoby-key"||effect.Args.Length!=4)
                throw new InvalidDataException("Tanoby shake presentation context differs.");
            int[] p=effect.Args.Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();
            SevenIslandFieldPresentation.ShakeOffset(p[0],p[1],p[2],p[3],0);
            _sevenIslandShake=(p[0],p[1],p[2],p[3]);_sevenIslandShakeFrames=0;_sevenIslandShakeSequence=effect.Sequence;
            SourceSound("SE_M_STRENGTH");
        }
    }
    private void ProcessSevenIslandFieldPresentation(double delta)
    {
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        if(_sevenIslandShake is not {} shake)return;
        _sevenIslandShakeFrames+=delta*60;
        if(_sevenIslandShakeFrames>=shake.Shakes*shake.Delay)_sevenIslandShake=null;
    }
    private void ResetSevenIslandFieldPresentation()
    {
        ClearFieldShadeTextures();_sevenIslandShake=null;_sevenIslandShakeFrames=0;_sevenIslandShakeSequence=-1;
    }
}
