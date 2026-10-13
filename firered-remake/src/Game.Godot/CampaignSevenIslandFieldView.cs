using System.Globalization;
using Game.Application;
using Game.Domain.Pokemon;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<Texture2D,ImageTexture> _sevenIslandShadeTextures=[];
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
        if(!_catalog.SevenIslandEnabled)return;
        if(before?.World.Player.MapId!=after.World.Player.MapId||before?.FieldWeather!=after.FieldWeather)
        {
            ClearSevenIslandShadeTextures();_sevenIslandShake=null;_sevenIslandShakeFrames=0;
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
    private Texture2D SevenIslandShadeTexture(Texture2D original,bool background)
    {
        // Every used source chamber background/border tile palette was authenticated as 0..12.
        // The chambers contain no NPCs; the player's alternate OBJ gamma table is unchanged at index3.
        if(!_catalog.SevenIslandEnabled||!background||_session.State.FieldWeather!="WEATHER_SHADE")return original;
        if(!UnownFormRules.IsChamber(_session.State.World.Player.MapId)&&!(_catalog.EventIslandsEnabled&&_session.State.World.Player.MapId.Value=="map.navel-rock-summit"))throw new InvalidDataException("Shade outside Tanoby chamber.");
        if(_sevenIslandShadeTextures.TryGetValue(original,out var cached))return cached;
        using var image=original.GetImage();image.Convert(Image.Format.Rgba8);byte[] pixels=image.GetData();
        for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]!=0)
            for(int c=0;c<3;c++){int value=SevenIslandFieldPresentation.ShadeChannel(pixels[i+c]>>3);pixels[i+c]=(byte)((value<<3)|(value>>2));}
        using var shaded=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
        var texture=ImageTexture.CreateFromImage(shaded);_sevenIslandShadeTextures.Add(original,texture);return texture;
    }
    private void ClearSevenIslandShadeTextures()
    {
        foreach(var texture in _sevenIslandShadeTextures.Values)texture.Dispose();_sevenIslandShadeTextures.Clear();
    }
    private void ResetSevenIslandFieldPresentation()
    {
        ClearSevenIslandShadeTextures();_sevenIslandShake=null;_sevenIslandShakeFrames=0;_sevenIslandShakeSequence=-1;
    }
}
