using Game.Domain.Battle;
using Game.Domain.Items;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Optional authenticated Route11 assets and source event text. Application commands own battle state and RNG.</summary>
public partial class CampaignView
{
    private void LoadRoute11Presentation()
    {
        var source = _catalog.Route11Pokemon ?? throw new InvalidDataException("Route11 Pokemon presentation is missing.");

        RegisterPokemonSprites(source);

        RegisterPokemonIcons(source);
        var art = PretRoute11Art.Load(_root);
        foreach (var portrait in art.Portraits)
        {
            using var image = NativeImages.LoadPng(portrait.Png, 64, 64);
            _forestTrainerPortraits.Add(portrait.Id, ImageTexture.CreateFromImage(image));
        }
        foreach (var asset in art.StatusAssets)
        {
            using var image = NativeImages.LoadPng(asset.Png, asset.Width, asset.Height);
            _forestStatusTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
        var items = _catalog.Route11Items ?? throw new InvalidDataException("Route11 item presentation is missing.");
        foreach (var icon in items.Icons.Concat(ItemIconAssets.LoadProject(_root, "content/generated/data/machine-learning/route11.json", MachineRules.Hm05Id)).Where(i => !_earlyItemIcons.ContainsKey(i.Item)))
        {
            
            _earlyItemIcons.Add(icon.Item, ItemTexture(items.Icons.Contains(icon) ? "data/items/route11" : "data/machine-learning", icon));
        }
    }

    private ForestMessage SealedMoveMessage(BattlerSlot side, MoveId move) => new("sText_PkmnCantUseMoveSealed",
        ("B_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(side)), ("B_CURRENT_MOVE", MoveName(move)));

    private string? Route11SealedMoveNotice(CampaignWildBattle battle, int slot)
    {
        if (slot < 0 || slot >= battle.State.Player.Pokemon.Moves.Length ||
            battle.State.Opponent is not { Imprison: true, Pokemon.CurrentHp: > 0 }) return null;
        var move = battle.State.Player.Pokemon.Moves[slot].Move;
        if (!battle.State.Opponent.Pokemon.Moves.Any(m => m.Move == move)) return null;
        var message = SealedMoveMessage(new BattlerSlot(0), move);
        return string.Join('\n', WildSourcePages(message.Symbol, message.Values));
    }

    private ForestMessage? Route11WildMessage(CampaignWildBattle battle, SourceBattleEvent current)
    {
        // Water Absorb's HP event owns the single recovery page. The preceding
        // prevention event is silent when an actual recovery follows it.
        if (current is SourceAbilityPreventedEvent { Ability: "ABILITY_WATER_ABSORB" } absorb)
        {
            bool recovers = battle.EventIndex + 1 < battle.Events.Length && battle.Events[battle.EventIndex + 1] is
                SourceRecoveredEvent heal && heal.Side == absorb.Target && heal.Move == absorb.Move;
            return recovers ? new("") : new("sText_PkmnsXMadeYUseless", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(absorb.Target)),
                ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName(absorb.Ability)), ("B_CURRENT_MOVE", MoveName(absorb.Move)));
        }
        if (current is SourceRecoveredEvent recovered && battle.EventIndex > 0 && battle.Events[battle.EventIndex - 1] is
            SourceAbilityPreventedEvent { Ability: "ABILITY_WATER_ABSORB" } previous && previous.Target == recovered.Side && previous.Move == recovered.Move)
            return new("sText_PkmnRestoredHPUsing", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(recovered.Side)), ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName(previous.Ability)));
        return current switch
        {
            SourceStatusChangedEvent { Reason: "move", Before: 0, After: WildBattleStatus.Freeze } change =>
                new("sText_PkmnWasFrozen", ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
            SourceActionBlockedEvent { Status: WildBattleStatus.Freeze } blocked =>
                new("sText_PkmnIsFrozen", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(blocked.Side))),
            SourceStatusChangedEvent { Reason: "thawed-out", Before: WildBattleStatus.Freeze, After: 0 } thaw =>
                new("sText_PkmnWasDefrosted2", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(thaw.Side))),
            SourceStatusChangedEvent { Reason: "fire-hit-thaw", Before: WildBattleStatus.Freeze, After: 0 } thaw =>
                new("sText_PkmnWasDefrosted", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(thaw.Side))),
            SourceMtMoonVolatileEvent { Volatile: "imprison", Reason: "move" } seal =>
                new("sText_PkmnSealedOpponentMove", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(seal.Side))),
            SourceMtMoonVolatileEvent { Volatile: "rain" } rain => new(rain.Reason switch
            { "move" => "sText_StartedToRain", "countdown" => "sText_RainContinues", "expired" => "sText_RainStopped", _ => throw new InvalidDataException("Unknown rain event.") }),
            SourceMoveFailedEvent { Reason: "imprisoned" } failed => SealedMoveMessage(failed.Actor, failed.Move),
            SourceMoveFailedEvent { Reason: "already-imprisoning" or "no-shared-move" or "already-raining" } => new("sText_ButItFailed"),
            SourceMoveFailedEvent { Reason: "already-burned" } failed => new("sText_PkmnAlreadyHasBurn", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(failed.Target))),
            SourceMoveFailedEvent { Reason: "fire-type-immunity" } failed => new("sText_ItDoesntAffect", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(failed.Target))),
            SourceAbilityPreventedEvent { Ability: "ABILITY_DAMP" } ability => new("sText_PkmnPreventsUsage",
                ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability)),
                ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(ability.Actor)), ("B_CURRENT_MOVE", MoveName(ability.Move))),
            SourceAbilityPreventedEvent { Ability: "ABILITY_WATER_VEIL" } ability => new("sText_PkmnsXPreventsBurns",
                ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_EFF_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability))),
            SourceHeldItemConsumedEvent { Item: "ITEM_ASPEAR_BERRY" } item => new("sText_PkmnsItemDefrostedIt",
                ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(item.Side)), ("B_LAST_ITEM", _catalog.DisplayNames.ItemName(item.Item))),
            _ => null
        };
    }

    private bool Route11SilentEvent(CampaignWildBattle battle) => WildCurrentEvent(battle) is { } current &&
        Route11WildMessage(battle, current) is { Symbol.Length: 0 };
}
