using Game.Application;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Godot;

// Source-authenticated art/text only. The application still owns every battle/field command.
public partial class CampaignView
{
    private bool _runningShiftHeld;
    private int _fieldStepTicks = 16;
    private string _fieldStepAction = "walk";

    private bool HandleRunningShift(InputEvent input)
    {
        if (input is not InputEventKey { PhysicalKeycode: Key.Shift } key) return false;
        if (!key.Echo) _runningShiftHeld = key.Pressed;
        return true;
    }

    // Source field_player_avatar.c:516 and bike.c:253. FR's IsFortreeBridge is always false.
    // The desktop Shift key represents the source GBA B button; desktop B keeps opening Bag.
    private int FieldStepTicksForDirection(CampaignState state,Game.Domain.World.Direction direction) =>
        CampaignFieldMovementTiming.Resolve(state,new("move",direction){RunHeld=_runningShiftHeld},_catalog).Frames;

    private void LoadRoute3PokemonPresentation()
    {
        if (_catalog.Route3Pokemon is not { } source) return;
        RegisterPokemonSprites(source);

    }

    private ForestMessage? Route3WildMessage(CampaignWildBattle battle, WildBattleEvent current) => current switch
    {
        WildBattleMoveFailedEvent {Reason: "nothing-happened"} => new("sText_ButNothingHappened"),
        WildBattleMoveFailedEvent {Reason: "no-disable-target"} => new("sText_ButItFailed"),
        WildBattleMoveFailedEvent {Reason: "disabled"} failed => DisabledMoveMessage(failed.Actor, failed.Move),
        WildBattleDisableChangedEvent {Reason: "move", Move: { } move} change => new("sText_PkmnMoveWasDisabled",
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(change.Side)), ("B_BUFF1", MoveName(move))),
        WildBattleDisableChangedEvent {Reason: "expired"} change => new("sText_PkmnMoveDisabledNoMore", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        WildBattleDisableChangedEvent or WildBattleRolloutChangedEvent or WildBattleAbilityActivatedEvent => new(""),
        WildBattleInfatuationChangedEvent {Reason: "cute-charm", After: { } source} change => new("sText_PkmnsXInfatuatedY",
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(source)), ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName("ABILITY_CUTE_CHARM")), ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        WildBattleInfatuationChangedEvent => new(""),
        WildBattleInfatuationBlockedEvent blocked => new("sText_PkmnImmobilizedByLove", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(blocked.Side))),
        WildBattleStatusChangedEvent {Reason: "poison-point"} poison => new("sText_PkmnPoisonedBy",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(Opposite(poison.Side))), ("B_SCR_ACTIVE_ABILITY", _catalog.DisplayNames.AbilityName("ABILITY_POISON_POINT")), ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(poison.Side))),
        WildBattleHeldItemConsumedEvent {Item: "ITEM_ORAN_BERRY"} item => new("sText_PkmnsItemRestoredHealth",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(item.Side)), ("B_LAST_ITEM", _catalog.DisplayNames.ItemName(item.Item))),
        WildBattleBindingChangedEvent {Reason: "wrap"} bind => new("sText_PkmnWrappedBy",
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(bind.Side)), ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(Opposite(bind.Side)))),
        WildBattleStatChangedEvent stat when battle.Events.Any(e => e.Sequence == stat.Sequence - 1 &&
            e is WildBattleAbilityActivatedEvent {Ability: "ABILITY_INTIMIDATE"} ability && ability.Side == stat.Actor) => new("sText_PkmnCutsAttackWith",
                ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(stat.Actor)), ("B_SCR_ACTIVE_ABILITY", _catalog.DisplayNames.AbilityName("ABILITY_INTIMIDATE")), ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stat.Target))),
        _ => null
    };

    private ForestMessage DisabledMoveMessage(WildBattleSide side, MoveId move) => new("sText_PkmnMoveIsDisabled",
        ("B_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(side)), ("B_CURRENT_MOVE", MoveName(move)));

    private void SelectWildMoveMenu(CampaignWildBattle battle)
    {
        _wildMenu = "moves";
        var player = battle.State.Player;
        _wildCursor = SourceBattleForcedMoveSlot(battle) ?? (SourceBattleRequiresStruggle(battle) ? 0 :
            SourceBattleMoveSelectable(battle, _wildLastMoveCursor) ? _wildLastMoveCursor :
            Enumerable.Range(0, player.Pokemon.Moves.Length).First(i => SourceBattleMoveSelectable(battle, i)));
    }
}
