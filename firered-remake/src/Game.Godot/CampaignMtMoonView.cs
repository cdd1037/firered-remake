using System.Security.Cryptography;
using Game.Content;
using Game.Application;
using Game.Domain.WildBattle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Godot;

// Source-authenticated Mt. Moon assets and field item selectors. Application commands
// remain the sole authority for evolution, item effects, inventory and RNG.
public partial class CampaignView
{
    private bool _mtMoonMoveSlotOpen;
    private int _mtMoonMoveSlot;
    private ItemId _mtMoonTargetItem;
    private bool IsExtendedFieldTarget(ItemId item) =>
        FieldMedicineEffectFor(item) is FieldMedicineEffect.Revive or FieldMedicineEffect.RestoreMovePp or FieldMedicineEffect.RestoreAllPp ||
        _catalog.MtMoonEnabled && EarlyItem(item)?.UseSupport is EarlyShopUseSupport.SeparateEvolution or EarlyShopUseSupport.DeferredRareCandy;
    private bool MtMoonTargetActive => _fieldMedicineTargetOpen && IsExtendedFieldTarget(_mtMoonTargetItem);

    private void LoadMtMoonPresentation()
    {
        if (_catalog.MtMoonPokemon is not { } source) return;
        if (_battleArt.Count != 160 || _storageIcons.Count != 40 || _route22Footprints.Count != 11)
            throw new InvalidDataException("MtMoon must extend the existing forty species.");
        Image Decode(byte[] bytes, int width, int height, string sha)
        {
            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok) { image.Dispose(); throw new InvalidDataException("Invalid MtMoon PNG."); }
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != width || image.GetHeight() != height || Convert.ToHexStringLower(SHA256.HashData(image.GetData())) != sha)
            { image.Dispose(); throw new InvalidDataException("MtMoon source pixels differ."); }
            return image;
        }
        foreach (var sprite in source.Sprites)
        {
            using var image = Decode(source.ReadSpriteTexture(new(sprite.SpeciesId), sprite.Side, sprite.PaletteVariant == "shiny"), 64, 64, sprite.RgbaSha256);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(ImageTexture.CreateFromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var foot in source.Footprints)
        {
            using var image = Decode(source.ReadFootprintTexture(foot.SourceSpeciesId), 16, 16, foot.RgbaSha256);
            _route22Footprints.Add(foot.SourceSpeciesId, ImageTexture.CreateFromImage(image));
        }
        foreach (var icon in source.Icons)
        {
            using var image = Decode(source.ReadIconTexture(new(icon.SpeciesId)), 32, 32, icon.RgbaSha256);
            var rgba = image.GetData();
            // Match the existing storage adapter's source RGB5 display expansion.
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int value = rgba[i + c] >> 3; rgba[i + c] = (byte)((value << 3) | (value >> 2)); }
            using var display = Image.CreateFromData(32, 32, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), ImageTexture.CreateFromImage(display));
        }
        foreach (var asset in PretMtMoonTrainerArt.Load(_root))
        {
            using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, asset.Rgba);
            _forestTrainerPortraits.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
        var items = _catalog.MtMoonItems ?? throw new InvalidDataException("Missing MtMoon item presentation.");
        foreach (var icon in items.Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, PretPewterMachines.IconRgba(icon));
            _earlyItemIcons.Add(icon.Item, ImageTexture.CreateFromImage(image));
        }
        var document = _questPocketUi ?? throw new InvalidDataException("Missing source key item pocket.");
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        foreach (var fossil in items.Fossils)
        {
            var value = items.Items.Single(i => i.Metadata.Item.Id == fossil.Item).Metadata;
            var icon = items.Icons.Single(i => i.Item == fossil.Item);
            string id = "mtmoon-" + value.Item.SourceItemSymbol.ToLowerInvariant();
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, PretPewterMachines.IconRgba(icon));
            _questPocketTextures.Add(id, ImageTexture.CreateFromImage(image));
            document = document with
            {
                Items = document.Items.Add(new(value.Item.Id.Value, value.Item.SourceItemSymbol, value.Item.Name, value.Description, id, value.Item.FieldUseFunction)),
                Assets = document.Assets.Add(new(id, icon.PngPath, icon.Width, icon.Height, position.X, position.Y, icon.PngSha256, icon.RgbaSha256))
            };
        }
        _questPocketUi = document;
        if (_battleArt.Count != 224 || _storageIcons.Count != 56 || _route22Footprints.Count != 27)
            throw new InvalidDataException("MtMoon presentation inventory differs.");
    }

    private bool TryOpenMtMoonItem(ItemId item)
    {
        // An ordinary medicine selected after a Moon item must not reuse its target mode.
        _mtMoonTargetItem = item;
        if (!IsExtendedFieldTarget(item)) return false;
        _mtMoonTargetItem = item; _fieldMedicineTarget = 0; _mtMoonMoveSlot = 0;
        _mtMoonMoveSlotOpen = false; _fieldMedicineTargetOpen = true;
        return true;
    }

    private bool MtMoonTargetInput(InputEventKey key)
    {
        if (!_mtMoonMoveSlotOpen)
        {
            int cancel = _session.State.Party.Length;
            if (key.Keycode == Key.Up) _fieldMedicineTarget = Math.Max(0, _fieldMedicineTarget - 1);
            if (key.Keycode == Key.Down) _fieldMedicineTarget = Math.Min(cancel, _fieldMedicineTarget + 1);
            if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _fieldMedicineTarget == cancel)
            { _fieldMedicineTargetOpen = false; return true; }
            if (key.Keycode != Key.Enter) return true;
            if (FieldMedicineEffectFor(_mtMoonTargetItem) == FieldMedicineEffect.RestoreMovePp)
            { _mtMoonMoveSlotOpen = true; _mtMoonMoveSlot = 0; return true; }
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
        long revision = _session.Revision;
        bool evolution = EarlyItem(_mtMoonTargetItem)?.UseSupport == EarlyShopUseSupport.SeparateEvolution;
        Apply(new(evolution ? "field_evolution" : _mtMoonTargetItem.Value == "pret:ITEM_RARE_CANDY" ? "field_rare_candy" : "field_medicine", Text: _mtMoonTargetItem.Value,
            PartyIndex: _fieldMedicineTarget, MoveSlot: _mtMoonMoveSlotOpen ? _mtMoonMoveSlot : -1));
        _fieldMedicineTargetOpen = false; _mtMoonMoveSlotOpen = false;
        if (_session.Revision == revision) _itemNotice = FieldConsumableNotice(_status);
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
        if (!_mtMoonMoveSlotOpen) { DrawFieldMedicineTargets(); return; }
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 112));
        var member = _session.State.Party[_fieldMedicineTarget];
        ItemLabel(new(16, 8), member.Nickname);
        for (int i = 0; i < member.Pokemon.Moves.Length; i++)
        {
            var move = member.Pokemon.Moves[i];
            ItemLabel(new(24, 26 + i * 16), MoveName(move.Move));
            ItemLabel(new(174, 26 + i * 16), $"{move.CurrentPp}/{move.MaximumPp}", 8);
        }
        ItemLabel(new(24, 26 + member.Pokemon.Moves.Length * 16), _itemSkin.Text("gFameCheckerText_Cancel"));
        ItemLabel(new(12, 26 + _mtMoonMoveSlot * 16), _itemSkin.Text("gText_SelectorArrow2"));
        ItemMessageBox(_battleText["gText_RestoreWhichMove"]);
    }
    private bool MtMoonSilentEvent(CampaignWildBattle battle) => WildCurrentEvent(battle) is { } current && RockTunnelWildMessage(current) is null && MtMoonWildMessage(battle, current) is { Symbol.Length: 0 };

    private ForestMessage? MtMoonWildMessage(CampaignWildBattle battle, WildBattleEvent current) => !_catalog.MtMoonEnabled ? null : current switch
    {
        WildBattleMtMoonVolatileEvent { Volatile: "charge", Reason: "move" } charge => new("sText_PkmnChargingPower", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(charge.Side))),
        WildBattleMtMoonVolatileEvent { Volatile: "water-sport", Reason: "move" } => new("sText_FireWeakened"),
        WildBattleMtMoonVolatileEvent { Volatile: "mud-sport", Reason: "move" } => new("sText_ElectricityWeakened"),
        WildBattleMtMoonVolatileEvent { Volatile: "follow-me" } follow => new("sText_PkmnCenterAttention", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(follow.Side))),
        WildBattleMtMoonVolatileEvent or WildBattleSelfDestructEvent => new(""),
        WildBattleEncoreChangedEvent { Reason: "move" } encore => new("sText_PkmnGotEncore", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(encore.Side))),
        WildBattleEncoreChangedEvent { Reason: "expired" } encore => new("sText_PkmnEncoreEnded", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(encore.Side))),
        WildBattleEncoreChangedEvent => new(""),
        WildBattleDrainEvent drain => new("sText_PkmnEnergyDrained", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(drain.Target))),
        WildBattleMagnitudeEvent magnitude => new("sText_MagnitudeStrength", ("B_BUFF1", magnitude.Magnitude.ToString())),
        WildBattleHeldItemStolenEvent stolen => new("sText_PkmnStoleItem", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(stolen.Actor)),
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stolen.Target)), ("B_LAST_ITEM", MtMoonHeldItemName(stolen.Item))),
        WildBattleMoveFailedEvent { Reason: "no-encore-target" or "no-ally-in-single-battle" or "already-mud-sport" } => new("sText_ButItFailed"),
        WildBattleConfusionChangedEvent { Reason: "rampage-ended" } confusion => new("sText_PkmnWasConfused", ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(confusion.Side))),
        WildBattleLeechSeedChangedEvent { Reason: "rapid-spin" } seed => new("sText_PkmnShedLeechSeed", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(seed.Side))),
        WildBattleBindingChangedEvent { Reason: "rapid-spin" } bind => new("sText_PkmnFreedFrom", ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(bind.Side)), ("B_BUFF1", MoveName(bind.Move ?? new("pret:MOVE_BIND")))),
        WildBattleAbilityPreventedEvent { Ability: "ABILITY_LEVITATE" } ability => new("sText_PkmnMakesGroundMiss", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_DEF_ABILITY", "LEVITATE")),
        WildBattleEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } effect when battle.Events.Any(e => e.Sequence == effect.Sequence - 1 && e is WildBattleAbilityPreventedEvent { Ability: "ABILITY_LEVITATE" }) => new(""),
        WildBattleAbilityPreventedEvent { Ability: "ABILITY_SOUNDPROOF" or "ABILITY_STICKY_HOLD" } ability => new("sText_PkmnsXBlocksY", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)),
            ("B_DEF_ABILITY", ability.Ability == "ABILITY_SOUNDPROOF" ? "SOUNDPROOF" : "STICKY HOLD"), ("B_CURRENT_MOVE", MoveName(ability.Move))),
        WildBattleStatusChangedEvent { Reason: "effect-spore", Before: 0 } change => new(change.After switch
            { >= 1 and <= 5 => "sText_PkmnMadeSleep", WildBattleStatus.Poison => "sText_PkmnPoisonedBy", WildBattleStatus.Paralysis => "sText_PkmnWasParalyzedBy", _ => throw new InvalidDataException("Unsupported Effect Spore status.") },
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(Opposite(change.Side))), ("B_SCR_ACTIVE_ABILITY", "EFFECT SPORE"), ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        _ => null
    };

    private string MtMoonHeldItemName(string symbol) => symbol switch
    {
        "ITEM_ORAN_BERRY" => "ORAN BERRY", "ITEM_TINY_MUSHROOM" => "TINYMUSHROOM", "ITEM_BIG_MUSHROOM" => "BIG MUSHROOM",
        "ITEM_MOON_STONE" => "MOON STONE", _ => throw new InvalidDataException("Unadmitted stolen item name: " + symbol)
    };

}
