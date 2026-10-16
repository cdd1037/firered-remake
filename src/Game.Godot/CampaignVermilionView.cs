using Game.Application;
using Game.Domain.WildBattle;
using Godot;

// Presentation consumes authenticated source pixels and committed events only.
// Item consumption, Pickup rolls and Pay Day settlement remain application rules.
public partial class CampaignView
{
    private void LoadVermilionPresentation()
    {
        if (_catalog.VermilionPokemon is not { } source) return;
        Image Decode(byte[] bytes, int width, int height)
        {
            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok) { image.Dispose(); throw new InvalidDataException("Invalid Vermilion PNG."); }
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != width || image.GetHeight() != height)
            { image.Dispose(); throw new InvalidDataException("Vermilion source pixels differ."); }
            return image;
        }
        foreach (var sprite in source.Sprites)
        {
            using var image = Decode(source.ReadSpriteTexture(new(sprite.SpeciesId), sprite.Side, sprite.PaletteVariant == "shiny"), 64, 64);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var foot in source.Footprints)
        {
            using var image = Decode(source.ReadFootprintTexture(foot.SourceSpeciesId), 16, 16);
            _route22Footprints.Add(foot.SourceSpeciesId, ImageTexture.CreateFromImage(image));
        }
        foreach (var icon in source.Icons)
        {
            using var image = Decode(source.ReadIconTexture(new(icon.SpeciesId)), 32, 32);
            var rgba = image.GetData();
            // Use the same source RGB5 display expansion as the earlier storage icons.
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int value = rgba[i + c] >> 3; rgba[i + c] = (byte)((value << 3) | (value >> 2)); }
            using var display = Image.CreateFromData(32, 32, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), ImageTexture.CreateFromImage(display));
        }
    }

    // Source battle_scripts_1.s:3883 and 4212–4281 select these battle_message.c strings.
    // Status/confusion changes precede their single held-item message in the event stream.
    private ForestMessage? VermilionWildMessage(WildBattleEvent current) => !_catalog.VermilionEnabled ? null : current switch
    {
        WildBattlePayDayEvent => new("sText_CoinsScattered"),
        WildBattleAbilityPreventedEvent { Ability: "ABILITY_LIMBER" } ability => new("sText_PkmnPreventsParalysisWith",
            ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_DEF_ABILITY", "LIMBER")),
        WildBattleStatusChangedEvent { Reason: "limber", After: 0 } change => new("sText_PkmnsXCuredItsYProblem",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(change.Side)), ("B_SCR_ACTIVE_ABILITY", "LIMBER"), ("B_BUFF1", ForestStatusName(change.Before))),
        WildBattleStatusChangedEvent { Reason: "held-berry" } or WildBattleConfusionChangedEvent { Reason: "persim-berry" } => new(""),
        WildBattleHeldItemConsumedEvent { Item: "ITEM_CHERI_BERRY" or "ITEM_CHESTO_BERRY" or "ITEM_PECHA_BERRY" or "ITEM_RAWST_BERRY" or "ITEM_PERSIM_BERRY" } item =>
            new(item.Item switch
            {
                "ITEM_CHERI_BERRY" => "sText_PkmnsItemCuredParalysis",
                "ITEM_CHESTO_BERRY" => "sText_PkmnsItemWokeIt",
                "ITEM_PECHA_BERRY" => "sText_PkmnsItemCuredPoison",
                "ITEM_RAWST_BERRY" => "sText_PkmnsItemHealedBurn",
                _ => "sText_PkmnsItemSnappedOut"
            }, ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(item.Side)), ("B_LAST_ITEM", VermilionHeldItemName(item.Item)!)),
        WildBattleHeldItemStolenEvent stolen when VermilionHeldItemName(stolen.Item) is { } name => new("sText_PkmnStoleItem",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(stolen.Actor)), ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stolen.Target)), ("B_LAST_ITEM", name)),
        _ => null
    };

    private bool VermilionSilentEvent(CampaignWildBattle battle) => WildCurrentEvent(battle) is { } current &&
        VermilionWildMessage(current) is { Symbol.Length: 0 };

    // Source src/data/items.json names for the added Pickup-held items. Oran and
    // the pre-existing Mt. Moon held items retain their earlier presentation path.
    private static string? VermilionHeldItemName(string symbol) => symbol switch
    {
        "ITEM_CHERI_BERRY" => "CHERI BERRY", "ITEM_CHESTO_BERRY" => "CHESTO BERRY",
        "ITEM_PECHA_BERRY" => "PECHA BERRY", "ITEM_RAWST_BERRY" => "RAWST BERRY",
        "ITEM_ASPEAR_BERRY" => "ASPEAR BERRY", "ITEM_PERSIM_BERRY" => "PERSIM BERRY",
        "ITEM_TM10" => "TM10", "ITEM_PP_UP" => "PP UP", "ITEM_RARE_CANDY" => "RARE CANDY", "ITEM_NUGGET" => "NUGGET",
        "ITEM_SPELON_BERRY" => "SPELON BERRY", "ITEM_PAMTRE_BERRY" => "PAMTRE BERRY",
        "ITEM_WATMEL_BERRY" => "WATMEL BERRY", "ITEM_DURIN_BERRY" => "DURIN BERRY", "ITEM_BELUE_BERRY" => "BELUE BERRY",
        _ => null
    };

    private int VermilionTrainerPayDayPageCount => _catalog.VermilionEnabled &&
        _session.State.ForestTrainerBattle is { Combat.Battle: { Outcome: WildBattleOutcome.Won, PayDayMoney: > 0 } } ? 1 : 0;

    private bool VermilionPayDayResultPage(CampaignWildBattle battle, Action<string, (string Key, string Value)[]> source)
    {
        if (battle.Frame?.TrainerTowerEncounter is not null || !_catalog.VermilionEnabled || battle.Phase != CampaignBattlePhase.Results || battle.Pages.IsEmpty ||
            battle.PageIndex != battle.Pages.Length - 1) return false;
        var trainer = _session.State.ForestTrainerBattle;
        bool settled = trainer is null ? battle.PostBattleItemsSettled : trainer.Stage == CampaignForestTrainerStage.Finished;
        var result = trainer?.Combat.Battle ?? battle.State;
        if (!settled || result.Outcome != WildBattleOutcome.Won || result.PayDayMoney == 0) return false;
        // Source Cmd_givepaydaymoney prints the collected amount even at the wallet cap.
        source("sText_PkmnPickedUpItem", [("B_BUFF1", result.PayDayMoney.ToString())]);
        return true;
    }
}
