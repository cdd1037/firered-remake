namespace Game.Presentation.BattleAnimations;

public enum SourceAnimationTerrain { Grass=0,LongGrass=1,Sand=2,Underwater=3,Water=4,Pond=5,Mountain=6,Cave=7,Building=8,Plain=9 }
public readonly record struct SourceBattlerVisibility(bool Present,bool LogicalInvisible,bool SpriteInvisible);
public sealed record SourceBattlerVisibilityFrame(int Slot,bool Present,bool LogicalInvisible,bool SpriteInvisible,bool OamInvisible);

public sealed partial class SourceBattleAnimation
{
    private readonly SourceBattlerVisibility[] _initialSourceVisibility=new SourceBattlerVisibility[4];
    private readonly bool[] _sourcePresent=new bool[4],_sourceLogicalInvisible=new bool[4],_sourceSpriteInvisible=new bool[4],_sourceOamInvisible=new bool[4];
    // Legacy Visible is only a constructor compatibility input. Production supplies
    // event-time presence and both native flags explicitly; callbacks never mutate it.
    private static SourceBattlerVisibility InitialSourceVisibility(AnimationBattler b)=>b.SourceVisibility??new(b.Visible,!b.Visible,!b.Visible);
    private void InitializeSourceVisibility()
    {foreach(var b in _battlers.Values)_initialSourceVisibility[b.Slot.Value]=InitialSourceVisibility(b);ResetSourceVisibility();}
    private void ResetSourceVisibility()
    {for(int i=0;i<4;i++){var s=_initialSourceVisibility[i];_sourcePresent[i]=s.Present;_sourceLogicalInvisible[i]=s.LogicalInvisible;_sourceSpriteInvisible[i]=_sourceOamInvisible[i]=s.SpriteInvisible;}}
    private bool IsSourceBattlerPresent(int slot)=>slot is >=0 and <4&&_sourcePresent[slot];
    // Original IsBattlerSpriteVisible is an OR of the inverse native flags.
    private bool IsSourceBattlerVisible(int slot)=>IsSourceBattlerPresent(slot)&&(!_sourceLogicalInvisible[slot]||!_sourceSpriteInvisible[slot]);
    private bool SourceBattlerLogicalInvisible(int slot)=>_sourceLogicalInvisible[slot];
    private bool SourceBattlerSpriteInvisible(int slot)=>_sourceSpriteInvisible[slot];
    private void SetSourceBattlerLogicalInvisible(int slot,bool value)=>_sourceLogicalInvisible[slot]=value;
    private void SetSourceBattlerSpriteInvisible(int slot,bool value)=>_sourceSpriteInvisible[slot]=value;
    private void CaptureSourceVisibilityOam()=>Array.Copy(_sourceSpriteInvisible,_sourceOamInvisible,4);
    private SourceBattlerVisibilityFrame[] SourceVisibilitySnapshot()=>Enumerable.Range(0,_battlerCount).Select(i=>new SourceBattlerVisibilityFrame(i,_sourcePresent[i],_sourceLogicalInvisible[i],_sourceSpriteInvisible[i],_sourceOamInvisible[i])).ToArray();
    private int? SourceAnimBattlerSlot(int participant)
    {
        int slot=participant switch{0=>_cue.Actor.Value,1=>_cue.Target.Value,2=>_cue.Actor.Value^2,3=>_cue.Target.Value^2,_=>-1};
        return (participant<2?IsSourceBattlerPresent(slot):IsSourceBattlerVisible(slot))?slot:null;
    }
    private void RestoreMonBackgroundObjects(string participant)
    {
        int slot=Number(participant) is 0 or 2?_cue.Actor.Value:_cue.Target.Value;
        if(_backgrounds.Contains(slot))SetSourceBattlerSpriteInvisible(slot,false);
        if(_doubles&&_backgrounds.Contains(slot^2))SetSourceBattlerSpriteInvisible(slot^2,false);
    }
}
