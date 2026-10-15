using Game.Application;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Godot;

// Source-authenticated art/text only. The application still owns every battle/field command.
public partial class CampaignView
{
    private bool _runningShiftHeld;
    private int _fieldStepTicks = 16;

    private bool HandleRunningShift(InputEvent input)
    {
        if (input is not InputEventKey { PhysicalKeycode: Key.Shift } key) return false;
        if (!key.Echo) _runningShiftHeld = key.Pressed;
        return true;
    }

    // Source field_player_avatar.c:516 and bike.c:253. FR's IsFortreeBridge is always false.
    // The desktop Shift key represents the source GBA B button; desktop B keeps opening Bag.
    private int FieldStepTicks(CampaignState state)
        => FieldStepTicksForDirection(state,state.World.Player.Facing);

    private int FieldStepTicksForDirection(CampaignState state,Game.Domain.World.Direction direction)
    {
        if (_catalog.SurfEnabled && state.IsSurfing) return 8;
        if (_catalog.CityServicesEnabled && state.IsCycling)
        {
            var p=state.World.Player;int y=direction==Game.Domain.World.Direction.South?p.Y+1:p.Y;
            int? cyclingBehavior=state.MapEdits?.TryGetValue(p.X+":"+y,out var edit)==true
                ?_catalog.MetatileDefinition(p.MapId,edit.SourceMetatileId)?.Behavior
                :_catalog.SourceMaps[p.MapId].SourceBehaviorAt(p.X,y);
            return direction is Game.Domain.World.Direction.North or Game.Domain.World.Direction.South&&cyclingBehavior==0x2a?8:6;
        }
        if (!_catalog.Route3Enabled || !_runningShiftHeld || !state.Script.Flags.GetValueOrDefault("FLAG_SYS_B_DASH")) return 16;
        var source = _catalog.SourceMaps[state.World.Player.MapId];
        return source.Events.GetProperty("allow_running").GetBoolean() &&
            source.SourceBehaviorAt(state.World.Player.X, state.World.Player.Y) is { } behavior && behavior != 0x0a ? 8 : 16;
    }

    private void LoadRoute3PokemonPresentation()
    {
        if (_catalog.Route3Pokemon is not { } source) return;
        foreach (var sprite in source.Sprites)
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(source.ReadSpriteTexture(new(sprite.SpeciesId), sprite.Side, sprite.PaletteVariant == "shiny")) != Error.Ok)
                throw new InvalidDataException("Invalid Route3 sprite PNG.");
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != 64 || image.GetHeight() != 64)
                throw new InvalidDataException("Route3 sprite pixels differ.");
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(ImageTexture.CreateFromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var foot in source.Footprints)
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(source.ReadFootprintTexture(foot.SourceSpeciesId)) != Error.Ok)
                throw new InvalidDataException("Invalid Route3 footprint PNG.");
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != 16 || image.GetHeight() != 16)
                throw new InvalidDataException("Route3 footprint pixels differ.");
            _route22Footprints.Add(foot.SourceSpeciesId, ImageTexture.CreateFromImage(image));
        }
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
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(source)), ("B_DEF_ABILITY", "CUTE CHARM"), ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        WildBattleInfatuationChangedEvent => new(""),
        WildBattleInfatuationBlockedEvent blocked => new("sText_PkmnImmobilizedByLove", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(blocked.Side))),
        WildBattleStatusChangedEvent {Reason: "poison-point"} poison => new("sText_PkmnPoisonedBy",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(Opposite(poison.Side))), ("B_SCR_ACTIVE_ABILITY", "POISON POINT"), ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(poison.Side))),
        WildBattleHeldItemConsumedEvent {Item: "ITEM_ORAN_BERRY"} item => new("sText_PkmnsItemRestoredHealth",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(item.Side)), ("B_LAST_ITEM", "ORAN BERRY")),
        WildBattleBindingChangedEvent {Reason: "wrap"} bind => new("sText_PkmnWrappedBy",
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(bind.Side)), ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(Opposite(bind.Side)))),
        WildBattleStatChangedEvent stat when battle.Events.Any(e => e.Sequence == stat.Sequence - 1 &&
            e is WildBattleAbilityActivatedEvent {Ability: "ABILITY_INTIMIDATE"} ability && ability.Side == stat.Actor) => new("sText_PkmnCutsAttackWith",
                ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(stat.Actor)), ("B_SCR_ACTIVE_ABILITY", "INTIMIDATE"), ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stat.Target))),
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
