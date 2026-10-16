using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override SourceObedienceContext? ObedienceContext => Frame.Obedience;
    internal override PartyMemberKey ObedienceMember(BattlerSlot slot) => Frame.Get(slot).Member!.Value;
    internal override bool ObedienceMultiTurn(BattlerSlot slot) => Frame.Get(slot).Registers.Turn?.Lock is { Kind: not WildBattleCalledLockKind.Recharge };

    private bool ObedienceRedirectsAction(string purpose)
    {
        var decision = CheckObedience(catalog, purpose);
        if (decision.Outcome == SourceObedienceOutcome.Obey) return false;
        if (decision.Outcome == SourceObedienceOutcome.AlternateMove)
            EnterCalledMove(CalledDefinition(catalog.Moves[invocation.CurrentMove].SourceMoveId), true, rawTarget: true);
        else
        {
            // Return 1 marks MISSED; the self-hit return 2 retains OBEYS and no miss flag.
            commandNoEffect = decision.SourceReturn == 1;
            CompleteScriptTarget();
        }
        return true;
    }
}
