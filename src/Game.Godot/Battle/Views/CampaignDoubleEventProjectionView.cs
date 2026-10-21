using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{
    private static bool DoubleEventChangesHp(SourceBattleEvent? current) => SourceBattleHpPresentation.ChangesHp(current);
    private int DoubleEventHp(int slot,WildBattleCombatant body)
    {
        var battle=_session.State.DoubleBattle!;
        var span=SourceBattleHpPresentation.Span(battle.CurrentEvent,new(slot),battle.DisplayedMembers[slot]);
        return span is {} hp?(int)Math.Round(Mathf.Lerp(hp.Before,hp.After,(float)Math.Clamp(_doubleElapsed/.4,0,1))):body.Pokemon.CurrentHp;
    }
    private uint DoubleEventStatus(int slot,WildBattleCombatant body)=>_session.State.DoubleBattle!.CurrentEvent is SourceStatusChangedEvent x&&x.Side.Value==slot
        ?(_doubleElapsed<.12?x.Before:x.After):body.Status;
    private void DrawDoubleSubstitute(int slot)
    {
        EnsureSourceMoveAssets();bool player=(slot&1)==0;
        DrawTexture(_sourceSubstituteTextures[player?"substitute-back":"substitute-front"],(player?new Vector2(40,65):new Vector2(144,24))+DoubleSpriteOffsets[slot]);
    }
}
