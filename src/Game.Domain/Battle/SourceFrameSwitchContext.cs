using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override bool ForceSwitchTrainer => Frame.BattleKind == WildBattleCalledBattleKind.Trainer;
    internal override bool ForceSwitchRootedCheck => true;
    internal override bool ForceSwitchHasReserve(BattlerSlot target) =>
        // Cmd_forcerandomswitch first counts every living non-egg party member, including
        // active positions; non-multi doubles requires at least three even after a partner KO.
        Frame.Party(target.Side).Members.Count(m => !m.IsEgg && m.Pokemon.CurrentHp > 0) >= (Frame.Layout == SourceBattleLayout.Singles ? 2 : 3) &&
        !SourceBattleIdentityRules.LegalReserveIndices(Frame,target.Side).IsEmpty;
    internal override bool ForceSwitchAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose)
    {
        if (!SourceGenericAccuracy(actor,target,move,purpose)) { SourceGenericMiss(actor,target,move); return false; }
        return true;
    }
    internal override void ForceSwitch(PokemonCatalog catalog, BattlerSlot target, MoveId move)
    {
        int index = SourceSwitchRules.ChooseForcedReserve(SourceBattleIdentityRules.LegalReserveIndices(Frame,target.Side),Draw);
        var result = SourceFourActionTurns.BindActionSwitch(catalog,Frame,target,new(target.Side,index),WildBattleCalledSwitchKind.Forced, invocation.Actor);
        if (!result.Succeeded) throw new SourceRecipeUnsupported(result.Fault!);
        Frame = result.State; Events.AddRange(result.Events); Trace.AddRange(result.Trace);
        invocation = invocation with { CurrentMove = new("pret:MOVE_NONE") };
    }
    // BattleScript_SuccessForceOut shares Teleport's outcome, but has no fled message.
    internal override void ForceSwitchEscape() => EndWildMoveEscape(null);
    internal override ImmutableArray<int> BatonPassReserves(BattlerSlot actor) => SourceBattleIdentityRules.LegalReserveIndices(Frame,actor.Side);
    internal override void PauseBatonPass(BattlerSlot actor, ImmutableArray<int> legal)
    {
        var pending = new SourcePendingSwitch(actor,WildBattleCalledSwitchKind.BatonPass,legal,invocation);
        Frame = Frame with { PendingSwitches = [pending] };
        Add(n => new SourceCalledPartySwitchRequiredEvent(n,Turn,pending));
    }
}
