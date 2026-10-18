using System.Collections.Immutable;
using Game.Application;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<(Texture2D Source,int Blend),ImageTexture> _sixIslandPaletteTextures=[];
    private string? _sixIslandFieldId;
    private double _sixIslandFieldFrames;
    private int _sixIslandProcessedFrames;
    private bool _sixIslandCry, _sixIslandFallSound, _sixIslandLandSound;
    private FieldWarpPresentation.Departure? _sixIslandDeparture;
    private SixIslandSourcePresentation.Landing? _sixIslandLanding;
    private SourceMusicFade? _sixIslandMusicFade;
    private long _sixIslandShakeSequence=-1;
    private double _sixIslandShakeFrames=37;
    private bool SixIslandFieldLocked => _session?.State.RuinValleyCut is not null || CommonFall(_session?.State) is not null;
    private int SixIslandFieldFrame => (int)Math.Floor(_sixIslandFieldFrames+1e-7);
    private bool SixIslandLandingFixedPriority => CommonFall(_session?.State)?.Phase==DottedHoleFallPhase.Arrival && _sixIslandLanding?.FixedPriority==true;
    private Vector2 SixIslandCameraOffset => new(0,_sixIslandLanding?.CameraY ?? SixIslandSourcePresentation.SapphireShakeY((int)Math.Floor(_sixIslandShakeFrames+1e-7)));

    private void SynchronizeSixIslandPresentation(CampaignState after)
    {
        string? id=after.RuinValleyCut?.Movement.PendingPresentationId.Value ?? CommonFall(after)?.PresentationId;
        if (id!=_sixIslandFieldId)
        {
            ClearSixIslandPaletteTextures();
            _sixIslandFieldId=id; _sixIslandFieldFrames=0; _sixIslandProcessedFrames=0;
            _sixIslandCry=false; _sixIslandFallSound=false; _sixIslandLandSound=false;
            _sixIslandDeparture=null; _sixIslandLanding=null; _sixIslandMusicFade=null;
            if (CommonFall(after)?.Phase==DottedHoleFallPhase.Arrival)
            {
                var sprite=CurrentPlayerSprite;
                _sixIslandLanding=new(80-(int)sprite.Anchor.Y+sprite.Height);
                _tracks.Clear(); _fade=0; _poses["LOCALID_PLAYER"].Visible=false;
            }
        }
        var shake=after.PresentationEffects.LastOrDefault(e=>e.Opcode=="special" && e.Args.SequenceEqual(new[]{"ShakeScreen"}));
        if (after.World.Player.MapId.Value=="map.six-island-dotted-hole-sapphire-room" && shake is not null && shake.Sequence!=_sixIslandShakeSequence)
        {
            if (!new[]{"VAR_0x8004","VAR_0x8005","VAR_0x8006","VAR_0x8007"}.Select(v=>after.Script.Variables.GetValueOrDefault(v)).SequenceEqual(new[]{3,0,12,3}))
                throw new InvalidDataException("Six Island screen-shake source context differs.");
            _sixIslandShakeSequence=shake.Sequence; _sixIslandShakeFrames=0;
            SourceSound("SE_M_STRENGTH"); // ShakeScreen itself plays this in addition to the preceding script cue.
        }
        if (SixIslandFieldLocked) { _held=null; _repeat=0; }
    }

    private int SixIslandCutPartyIndex()
    {
        var move=_session.State.RuinValleyCut?.Movement ?? throw new InvalidDataException("Ruin Valley Cut owner missing.");
        int index=move.PartyIndex;
        if (index<0 || index>=_session.State.Party.Length || _session.State.Party[index].InstanceId!=move.OwnerInstanceId)
            throw new InvalidDataException("Ruin Valley Cut selected owner differs.");
        return index;
    }
    private bool ProcessSixIslandPresentation(double delta)
    {
        if (!double.IsFinite(delta) || delta<0) throw new ArgumentOutOfRangeException(nameof(delta));
        _sixIslandShakeFrames=Math.Min(37,_sixIslandShakeFrames+delta*60);
        if (!SixIslandFieldLocked) return false;
        _held=null; _repeat=0; _sixIslandFieldFrames+=delta*60;
        var state=_session.State;
        if (state.RuinValleyCut is {} cut)
        {
            if (!_sixIslandCry && SixIslandFieldFrame>=FieldCutSourcePresentation.PlayerFrameDurations.Sum()+FieldCutSourcePresentation.OutdoorBannerOpenFrames+FieldCutSourcePresentation.MonSlideInFrames)
            { _sixIslandCry=true; SourceCry(state.Party[SixIslandCutPartyIndex()].Pokemon); }
            if (!_busy && SixIslandFieldFrame>=FieldCutSourcePresentation.ShowMonCueFrames)
                Apply(new("six_island_cut_ack",Text:cut.Movement.PendingPresentationId.Value));
            return true;
        }
        var fall=CommonFall(state) ?? throw new InvalidDataException("Common source fall owner missing.");
        if (fall.Phase==DottedHoleFallPhase.EntryStep)
        {
            if (!_busy && SixIslandFieldFrame>=state.PresentationTicks && _tracks.All(t=>t.Time+1e-9>=t.Duration))
                Apply(new(fall.Command,Text:fall.PresentationId));
            return true;
        }
        while (_sixIslandProcessedFrames<SixIslandFieldFrame)
        {
            int frame=++_sixIslandProcessedFrames;
            if (fall.Phase==DottedHoleFallPhase.Departure)
            {
                if (frame==DottedHoleFallPresentation.BeforeInvisibleDelay)
                { _poses["LOCALID_PLAYER"].Visible=false; SourceSound("SE_FALL"); }
                int delay=DottedHoleFallPresentation.BeforeInvisibleDelay+DottedHoleFallPresentation.BeforeWarpDelay;
                if (frame==delay)
                {
                    _sixIslandDeparture=new();
                    if (!state.Script.Flags.GetValueOrDefault("FLAG_DONT_TRANSITION_MUSIC") && (state.IsCycling || _audioMusic is not null && _audioMusic!=_catalog.MapDefinition(fall.Destination).Music))
                    {
                        // Underground is not IsMapTypeIndoors: source speed4, identical m4a fade as Seagallop.
                        _sixIslandMusicFade=new();
                        if (_sourceAudio is SynthSourceAudioPlayer synth) synth.BeginSourceMusicFade();
                    }
                }
                else if (frame>delay)
                {
                    _sixIslandDeparture!.Advance(_sixIslandMusicFade is null || _sixIslandMusicFade.Volume==0 && _sourceAudio?.Snapshot().Music?.Playing!=true);
                    _sixIslandMusicFade?.Advance();
                }
            }
            else _sixIslandLanding!.Advance();
        }
        if (fall.Phase==DottedHoleFallPhase.Departure)
        {
            if (!_busy && _sixIslandDeparture is {Complete:true})
                Apply(new(fall.Command,Text:fall.PresentationId));
            return true;
        }
        var landing=_sixIslandLanding!; var player=_poses["LOCALID_PLAYER"];
        player.Visible=landing.Visible; player.DrawYOffset=landing.OffsetY; player.Frame=0;
        if (landing.FallCue && !_sixIslandFallSound) { _sixIslandFallSound=true; SourceSound("SE_FALL"); }
        if (landing.LandingCue && !_sixIslandLandSound)
        { _sixIslandLandSound=true; SourceSound("SE_M_STRENGTH"); _cinnabarDustTile=state.World.Player; _cinnabarDustFrames=landing.LandingFrames; }
        if (!_busy && landing.Complete) Apply(new(fall.Command,Text:fall.PresentationId));
        return true;
    }
    private double? SixIslandMotionSeconds(CampaignState after,string actor,string action)
    {
        if (actor=="LOCALID_PLAYER" && CommonFall(after)?.Phase==DottedHoleFallPhase.EntryStep) return after.PresentationTicks/60d;
        if (after.World.Player.MapId.Value!="map.six-island-dotted-hole-sapphire-room") return null;
        return action=="slide_down"?2d/60:action.StartsWith("face_",StringComparison.Ordinal)?1d/60:null;
    }
    private float? SixIslandStepFraction(string actor,string action,double elapsed)
    {
        if (actor!="LOCALID_PLAYER" || CommonFall(_session.State)?.Phase!=DottedHoleFallPhase.EntryStep) return null;
        ImmutableArray<int> pixels=action.StartsWith("ride_water_current_",StringComparison.Ordinal)?FourIslandForcedMovementRules.Fast2Pixels:
            action.StartsWith("player_run_",StringComparison.Ordinal)?FourIslandForcedMovementRules.Fast1Pixels:Enumerable.Repeat(1,16).ToImmutableArray();
        int updates=Math.Clamp((int)Math.Floor(elapsed*60+1e-7),0,pixels.Length);
        return pixels.Take(updates).Sum()/16f;
    }
    private Texture2D SixIslandPaletteTexture(Texture2D original,bool background)
    {
        original=EventIslandPaletteTexture(SevenIslandShadeTexture(original,background),background);
        var palette=_sixIslandLanding?.Palette ?? _sixIslandDeparture?.Palette;
        int blend=palette is null?0:background?palette.BackgroundBlend:palette.ObjectBlend;
        if (blend==0) return original;
        if (_sixIslandPaletteTextures.TryGetValue((original,blend),out var cached)) return cached;
        using var image=original.GetImage(); image.Convert(Image.Format.Rgba8);
        byte[] pixels=image.GetData();
        for (int i=0;i<pixels.Length;i+=4) if (pixels[i+3]!=0)
            for (int c=0;c<3;c++) { int rgb5=((pixels[i+c]>>3)*(16-blend))>>4; pixels[i+c]=(byte)((rgb5<<3)|(rgb5>>2)); }
        using var shaded=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
        var texture=ImageTexture.CreateFromImage(shaded); _sixIslandPaletteTextures.Add((original,blend),texture); return texture;
    }
    private void ClearSixIslandPaletteTextures()
    {
        foreach (var texture in _sixIslandPaletteTextures.Values) texture.Dispose();
        _sixIslandPaletteTextures.Clear();
    }
    private void ResetSixIslandFieldPresentation()
    {
        ClearSixIslandPaletteTextures();
        _sixIslandFieldId=null; _sixIslandFieldFrames=0; _sixIslandProcessedFrames=0;
        _sixIslandDeparture=null; _sixIslandLanding=null; _sixIslandMusicFade=null;
        _sixIslandShakeSequence=-1; _sixIslandShakeFrames=37;
    }
}
