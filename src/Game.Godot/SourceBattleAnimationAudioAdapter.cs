using Game.Presentation.BattleAnimations;

/// <summary>Exact script sound ID/frame/pan through the existing original Synth.
/// Pan is the signed external m4a panX, applied only to this animation's SE token.</summary>
public sealed class SourceBattleAnimationAudioAdapter(SynthSourceAudioPlayer backend,
    Action<SourceAnimationSound>? controlObserver=null):ISourceAnimationAudio
{
    private readonly HashSet<long> _ownedTokens=[];
    public bool PanningImplemented=>true;
    public bool IsPlaying=>backend.Snapshot().Effects.Any(x=>x.Playing&&_ownedTokens.Contains(x.Token));
    public void Play(SourceAnimationSound cue)
    {
        if(cue.Pan is < -128 or > 127)throw new ArgumentOutOfRangeException(nameof(cue));
        cue.ValidateSourcePlayer(backend.Metadata(cue.Id).SourcePlayer);
        long token=backend.PlaySoundEffect(cue.Id);
        if(token==0)throw new InvalidOperationException("Source animation sound was not admitted: "+cue.Id);
        _ownedTokens.Add(token);
        if(!backend.SetBattleAnimationSoundPan(token,cue.Pan))
            throw new InvalidOperationException("Source animation sound has no owned active SE track: "+cue.Id);
        controlObserver?.Invoke(cue);
    }
    public void SetPan(int pan,int frame)
    {foreach(long token in _ownedTokens)backend.SetBattleAnimationSoundPan(token,pan);}
    public void StopAnimationEffects(){backend.StopBattleAnimationSoundEffects(_ownedTokens);_ownedTokens.Clear();}
}

public partial class SynthSourceAudioPlayer
{
    // An effect replacement gets a new token, so stale animation controls cannot
    // pan or stop it. This intentionally bounds the original SE1/SE2-wide control
    // to the requested animation cue, preserving unrelated effects and BGM.
    public bool SetBattleAnimationSoundPan(long ownedToken,int pan,ushort trackBits=ushort.MaxValue)
    {
        if(pan is < -128 or > 127)throw new ArgumentOutOfRangeException(nameof(pan));
        if(ownedToken<=0)return false;
        foreach(int player in new[]{1,2})
            if(_effects.TryGetValue(player,out var cue)&&cue.Token==ownedToken&&!Completed(cue))
                return _synth.SetPlayerPan(CoreSlot(player),pan,trackBits)>0;
        return false;
    }
    public void StopBattleAnimationSoundEffects(IReadOnlySet<long> ownedTokens)
    {
        foreach(int player in new[]{1,2})
            if(_effects.TryGetValue(player,out var cue)&&ownedTokens.Contains(cue.Token)){_effects.Remove(player);_synth.Stop(CoreSlot(player));Emit("stopped",cue,"animation-end-90-frame-limit");}
    }
}
