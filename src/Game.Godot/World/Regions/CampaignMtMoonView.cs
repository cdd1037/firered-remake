using Game.Domain.Battle;
using Game.Content;
using Game.Application;
using Game.Domain.WildBattle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;
using Godot;

// Source-authenticated Mt. Moon assets and field item selectors. Application commands
// remain the sole authority for evolution, item effects, inventory and RNG.
public partial class CampaignView
{
    private bool _mtMoonMoveSlotOpen;
    private int _mtMoonMoveSlot;
    private ItemId _mtMoonTargetItem;
    private long _ppBoostTargetInstance, _ppBoostSelectionRevision;
    private long _vitaminSelectionRevision, _evolutionSelectionRevision;
    private long[] _evolutionPartyInstances = [];
    private bool IsFieldEvolution(ItemId item) => _session.FieldEvolutionSupported(item);
    private bool EvolutionSelectionStale => _fieldMedicineTargetOpen && IsFieldEvolution(_mtMoonTargetItem) &&
        (_session.Revision != _evolutionSelectionRevision ||
         !_session.State.Party.Select(p => p.InstanceId).SequenceEqual(_evolutionPartyInstances));
    private long[] _vitaminPartyInstances = [];
    private bool IsFieldPpBoost(ItemId item) => _catalog.EarlyShop is {} shop &&
        SourceFieldPpBoostRules.Supports(shop, item);
    private bool IsFieldVitamin(ItemId item) => _catalog.EarlyShop is {} shop &&
        SourceFieldVitaminRules.Supports(shop, item);
    private bool VitaminSelectionStale => _fieldMedicineTargetOpen && IsFieldVitamin(_mtMoonTargetItem) &&
        (_session.Revision != _vitaminSelectionRevision ||
         !_session.State.Party.Select(p => p.InstanceId).SequenceEqual(_vitaminPartyInstances));
    private bool PpBoostSelectionStale => _mtMoonMoveSlotOpen && IsFieldPpBoost(_mtMoonTargetItem) &&
        (_session.Revision != _ppBoostSelectionRevision || _fieldMedicineTarget < 0 ||
         _fieldMedicineTarget >= _session.State.Party.Length ||
         _session.State.Party[_fieldMedicineTarget].InstanceId != _ppBoostTargetInstance);
    private bool IsExtendedFieldTarget(ItemId item) =>
        IsFieldPpBoost(item) || IsFieldVitamin(item) || IsFieldEvolution(item) ||
        FieldMedicineEffectFor(item) is FieldMedicineEffect.Revive or FieldMedicineEffect.RestoreMovePp or FieldMedicineEffect.RestoreAllPp ||
        EarlyItem(item)?.UseSupport == EarlyShopUseSupport.DeferredRareCandy;
    private bool MtMoonTargetActive => _fieldMedicineTargetOpen && IsExtendedFieldTarget(_mtMoonTargetItem);

    private void LoadMtMoonPresentation()
    {
        if (_catalog.MtMoonPokemon is not { } source) return;

        RegisterPokemonSprites(source);

        RegisterPokemonIcons(source);
        foreach (var asset in PretMtMoonTrainerArt.Load(_root))
        {
            using var image = NativeImages.LoadPng(asset.Png, 64, 64);
            _forestTrainerPortraits.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
        var items = _catalog.MtMoonItems ?? throw new InvalidDataException("Missing MtMoon item presentation.");
        foreach (var icon in items.Icons)
        {
            
            _earlyItemIcons.Add(icon.Item, ItemTexture("data/items/mtmoon", icon));
        }
        var document = _questPocketUi ?? throw new InvalidDataException("Missing source key item pocket.");
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        foreach (var fossil in items.Fossils)
        {
            var value = items.Items.Single(i => i.Metadata.Item.Id == fossil.Item).Metadata;
            var icon = items.Icons.Single(i => i.Item == fossil.Item);
            string id = "mtmoon-" + value.Item.SourceItemSymbol.ToLowerInvariant();
            
            _questPocketTextures.Add(id, ItemTexture("data/items/mtmoon", icon));
            document = document with
            {
                Items = document.Items.Add(new(value.Item.Id.Value, value.Item.SourceItemSymbol, value.Item.Name, value.Description, id, value.Item.FieldUseFunction)),
                Assets = document.Assets.Add(new(id,icon.PngPath,icon.Width,icon.Height,position.X,position.Y))
            };
        }
        _questPocketUi = document;
    }

    private bool TryOpenMtMoonItem(ItemId item)
    {
        // An ordinary medicine selected after a Moon item must not reuse its target mode.
        _mtMoonTargetItem = item;
        if (!IsExtendedFieldTarget(item)) return false;
        _mtMoonTargetItem = item; _fieldMedicineTarget = 0; _mtMoonMoveSlot = 0;
        _mtMoonMoveSlotOpen = false; _fieldMedicineTargetOpen = true;
        if (IsFieldEvolution(item))
        {
            _evolutionSelectionRevision = _session.Revision;
            _evolutionPartyInstances = _session.State.Party.Select(p => p.InstanceId).ToArray();
        }
        if (IsFieldVitamin(item))
        {
            _vitaminSelectionRevision = _session.Revision;
            _vitaminPartyInstances = _session.State.Party.Select(p => p.InstanceId).ToArray();
        }
        return true;
    }

    private bool MtMoonTargetInput(InputEventKey key)
    {
        if (PpBoostSelectionStale || VitaminSelectionStale || EvolutionSelectionStale)
        {
            _fieldMedicineTargetOpen = false; _mtMoonMoveSlotOpen = false;
            _itemNotice = "The selected POKéMON changed.\nPlease choose again.";
            return true;
        }
        if (!_mtMoonMoveSlotOpen)
        {
            int cancel = _session.State.Party.Length;
            NativePartyMove(key.Keycode,ref _fieldMedicineTarget,cancel);
            if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _fieldMedicineTarget == cancel)
            { _fieldMedicineTargetOpen = false; return true; }
            if (key.Keycode != Key.Enter) return true;
            if (FieldMedicineEffectFor(_mtMoonTargetItem) == FieldMedicineEffect.RestoreMovePp || IsFieldPpBoost(_mtMoonTargetItem))
            {
                _ppBoostTargetInstance = _session.State.Party[_fieldMedicineTarget].InstanceId;
                _ppBoostSelectionRevision = _session.Revision;
                _mtMoonMoveSlotOpen = true; _mtMoonMoveSlot = 0; return true;
            }
        }
        else
        {
            int cancel = _session.State.Party[_fieldMedicineTarget].Pokemon.Moves.Length;
            if (key.Keycode == Key.Up) _mtMoonMoveSlot = Math.Max(0, _mtMoonMoveSlot - 1);
            if (key.Keycode == Key.Down) _mtMoonMoveSlot = Math.Min(cancel, _mtMoonMoveSlot + 1);
            if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _mtMoonMoveSlot == cancel)
            { _mtMoonMoveSlotOpen = false; return true; }
            if (key.Keycode != Key.Enter) return true;
        }
        bool ppBoost = IsFieldPpBoost(_mtMoonTargetItem);
        bool evolution = IsFieldEvolution(_mtMoonTargetItem);
        bool accepted = Apply(new(evolution ? "field_evolution" : _mtMoonTargetItem.Value == "pret:ITEM_RARE_CANDY" ? "field_rare_candy" : "field_medicine", Text: _mtMoonTargetItem.Value,
            PartyIndex: _fieldMedicineTarget, MoveSlot: _mtMoonMoveSlotOpen ? _mtMoonMoveSlot : -1, Quantity: 1)
            { TargetInstanceId = evolution ? _evolutionPartyInstances[_fieldMedicineTarget] : ppBoost ? _ppBoostTargetInstance :
                IsFieldVitamin(_mtMoonTargetItem) ? _vitaminPartyInstances[_fieldMedicineTarget] : null });
        _fieldMedicineTargetOpen = false; _mtMoonMoveSlotOpen = false;
        if (!accepted) _itemNotice = FieldConsumableNotice(_status);
        _itemCursor = Math.Min(_itemCursor, _session.State.Inventory!.BagItems.Length);
        return true;
    }

    private string FieldConsumableNotice(string error) => error switch
    {
        "escape_rope_map_not_allowed" => $"OAK: {_session.State.PlayerName}!\nThis isn't the time to use that!",
        "escape_rope_destination_missing" or "escape_rope_destination_mismatch" => "Leave and re-enter this area\nbefore using an ESCAPE ROPE.",
        "rare_candy_evolution_target_not_supported" => "This evolution isn't available\nin the current build.",
        _ => error
    };

    private void DrawMtMoonTargets()
    {
        if (PpBoostSelectionStale || VitaminSelectionStale || EvolutionSelectionStale)
        { ItemMessageBox("The selected POKéMON changed.\nPlease choose again."); return; }
        if (!_mtMoonMoveSlotOpen) { DrawFieldMedicineTargets(); return; }
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 112));
        var member = _session.State.Party[_fieldMedicineTarget];
        bool ppBoost = IsFieldPpBoost(_mtMoonTargetItem);
        var boostMoves = ppBoost ? SourceFieldPpBoostRules.SelectMoves(_catalog.EarlyShop!,
            _catalog.PokemonContent!, member, _mtMoonTargetItem) : null;
        ItemLabel(new(16, 8), _catalog.PokemonDisplayName(member));
        for (int i = 0; i < member.Pokemon.Moves.Length; i++)
        {
            var move = member.Pokemon.Moves[i];
            ItemLabel(new(24, 26 + i * 16), MoveName(move.Move));
            ItemLabel(new(174, 26 + i * 16), $"{move.CurrentPp}/{move.MaximumPp}", 8);
            if (boostMoves is { Succeeded: true } && !boostMoves.Moves[i].Eligible)
                ItemLabel(new(156, 26 + i * 16), "×", 8);
        }
        ItemLabel(new(24, 26 + member.Pokemon.Moves.Length * 16), _itemSkin.Text("gFameCheckerText_Cancel"));
        ItemLabel(new(12, 26 + _mtMoonMoveSlot * 16), _itemSkin.Text("gText_SelectorArrow2"));
        ItemMessageBox(ppBoost ? string.Join("\n", _catalog.TextPages("gText_BoostPp")) : _battleText["gText_RestoreWhichMove"]);
    }
    private bool MtMoonSilentEvent(CampaignWildBattle battle) => WildCurrentEvent(battle) is { } current && RockTunnelWildMessage(current) is null && MtMoonWildMessage(battle, current) is { Symbol.Length: 0 };

    private ForestMessage? MtMoonWildMessage(CampaignWildBattle battle, SourceBattleEvent current) => current switch
    {
        SourceMtMoonVolatileEvent { Volatile: "charge", Reason: "move" } charge => new("sText_PkmnChargingPower", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(charge.Side))),
        SourceMtMoonVolatileEvent { Volatile: "water-sport", Reason: "move" } => new("sText_FireWeakened"),
        SourceMtMoonVolatileEvent { Volatile: "mud-sport", Reason: "move" } => new("sText_ElectricityWeakened"),
        SourceMtMoonVolatileEvent { Volatile: "follow-me" } follow => new("sText_PkmnCenterAttention", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(follow.Side))),
        SourceMtMoonVolatileEvent or SourceSelfDestructEvent => new(""),
        SourceEncoreChangedEvent { Reason: "move" } encore => new("sText_PkmnGotEncore", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(encore.Side))),
        SourceEncoreChangedEvent { Reason: "expired" } encore => new("sText_PkmnEncoreEnded", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(encore.Side))),
        SourceEncoreChangedEvent => new(""),
        SourceDrainEvent drain => new("sText_PkmnEnergyDrained", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(drain.Target))),
        SourceMagnitudeEvent magnitude => new("sText_MagnitudeStrength", ("B_BUFF1", magnitude.Magnitude.ToString())),
        SourceHeldItemStolenEvent stolen => new("sText_PkmnStoleItem", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(stolen.Actor)),
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stolen.Target)), ("B_LAST_ITEM", _catalog.DisplayNames.ItemName(stolen.Item))),
        SourceMoveFailedEvent { Reason: "no-encore-target" or "no-ally-in-single-battle" or "already-mud-sport" } => new("sText_ButItFailed"),
        SourceConfusionChangedEvent { Reason: "rampage-ended" } confusion => new("sText_PkmnWasConfused", ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(confusion.Side))),
        SourceLeechSeedChangedEvent { Reason: "rapid-spin" } seed => new("sText_PkmnShedLeechSeed", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(seed.Side))),
        SourceBindingChangedEvent { Reason: "rapid-spin" } bind => new("sText_PkmnFreedFrom", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(bind.Side)), ("B_BUFF1", MoveName(bind.Move ?? new("pret:MOVE_BIND")))),
        SourceAbilityPreventedEvent { Ability: "ABILITY_LEVITATE" } ability => new("sText_PkmnMakesGroundMiss", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability))),
        SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } effect when battle.Events.Any(e => e.Sequence == effect.Sequence - 1 && e is SourceAbilityPreventedEvent { Ability: "ABILITY_LEVITATE" }) => new(""),
        SourceAbilityPreventedEvent { Ability: "ABILITY_SOUNDPROOF" or "ABILITY_STICKY_HOLD" } ability => new("sText_PkmnsXBlocksY", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)),
            ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability)), ("B_CURRENT_MOVE", MoveName(ability.Move))),
        SourceStatusChangedEvent { Reason: "effect-spore", Before: 0 } change => new(change.After switch
            { >= 1 and <= 5 => "sText_PkmnMadeSleep", WildBattleStatus.Poison => "sText_PkmnPoisonedBy", WildBattleStatus.Paralysis => "sText_PkmnWasParalyzedBy", _ => throw new InvalidDataException("Unsupported Effect Spore status.") },
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(Opposite(change.Side))), ("B_SCR_ACTIVE_ABILITY", _catalog.DisplayNames.AbilityName("ABILITY_EFFECT_SPORE")), ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        _ => null
    };

}
