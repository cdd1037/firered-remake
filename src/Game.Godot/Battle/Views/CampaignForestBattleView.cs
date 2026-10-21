using Game.Domain.Battle;
using Game.Application;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Opt-in forest pixels and typed-event presentation. Display interpolation and
/// source text never choose moves, spend PP, apply status, or draw battle RNG.</summary>
public partial class CampaignView
{
    private sealed record ForestMessage(string Symbol, params (string Key, string Value)[] Values);
    private readonly Dictionary<string, ImageTexture> _forestStatusTextures = new(StringComparer.Ordinal);

    private void LoadForestBattlePresentation()
    {
        var source = _catalog.ForestPokemon ?? throw new InvalidDataException("Forest Pokemon content is missing.");
        foreach (var sprite in source.Sprites)
        {
            using var image = NativeImages.LoadPng(System.IO.File.ReadAllBytes(System.IO.Path.Combine(source.GeneratedDirectory, sprite.TexturePath)), 64, 64);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        var evolution= PretEvolutionArt.Load(_root);
        foreach(var sprite in evolution.Sprites)
        {
            using var image=NativeImages.LoadPng(evolution.ReadTexture(new(sprite.SpeciesId),sprite.Side,sprite.PaletteVariant=="shiny"), 64, 64);
            _battleArt.Add((sprite.SourceSpeciesId,sprite.Side,sprite.PaletteVariant=="shiny"),new(SceneTextures.FromImage(image),new(sprite.SingleBattleTopLeft.X,sprite.SingleBattleTopLeft.Y)));
        }
        if(_catalog.PewterPokemon is {} pewter)
        {
            foreach(var sprite in pewter.Sprites)
            {
                using var image = NativeImages.LoadPng(System.IO.File.ReadAllBytes(System.IO.Path.Combine(pewter.GeneratedDirectory,sprite.TexturePath)), 64, 64);
                _battleArt.Add((sprite.SourceSpeciesId,sprite.Side,sprite.PaletteVariant=="shiny"),new(SceneTextures.FromImage(image),new(sprite.SingleBattleTopLeft.X,sprite.SingleBattleTopLeft.Y)));
            }
        }
        LoadRoute22PokemonPresentation();
        LoadRoute3PokemonPresentation();
        LoadForestStatusTextures();
    }

    private ForestMessage? ForestWildMessage(SourceBattleEvent battleEvent) => battleEvent switch
    {
        SourceEffectivenessEvent {Effectiveness:WildBattleEffectiveness.SuperEffective}=>new("sText_SuperEffective"),
        SourceEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NotVeryEffective}=>new("sText_NotVeryEffective"),
        SourceEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NoEffect} effect=>new("sText_ItDoesntAffect",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(effect.Target))),
        SourceMoveFailedEvent failed => failed.Reason switch
        {
            "already-poisoned" => new("sText_PkmnAlreadyPoisoned",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(failed.Target))),
            "already-asleep" => new("sText_PkmnAlreadyAsleep",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(failed.Target))),
            "already-paralyzed" => new("sText_PkmnIsAlreadyParalyzed",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(failed.Target))),
            "already-confused" => new("sText_PkmnAlreadyConfused",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(failed.Target))),
            "poison-type-immunity" or "grass-type-immunity" => new("sText_ItDoesntAffect",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(failed.Target))),
            "already-seeded" => new("sText_PkmnEvadedAttack",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(failed.Target))),
            "existing-status" or "already-focused" or "type-immunity" or "no-force-out-reserve" or "force-out-level" => new("sText_ButItFailed"),
            _ => null
        },
        SourceMoveMissedEvent {Move.Value:"pret:MOVE_LEECH_SEED"} missed => new("sText_PkmnEvadedAttack",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(missed.Target))),
        SourceMoveMissedEvent {Move.Value:"pret:MOVE_POISON_POWDER" or "pret:MOVE_SLEEP_POWDER" or "pret:MOVE_STUN_SPORE" or "pret:MOVE_THUNDER_WAVE" or "pret:MOVE_SUPERSONIC"} => new("sText_ButItFailed"),
        SourceConfusionChangedEvent {Reason:"move"} confusion => new("sText_PkmnWasConfused",("B_EFF_NAME_WITH_PREFIX",WildPrefixedName(confusion.Side))),
        SourceConfusionChangedEvent {Reason:"countdown"} confusion => new("sText_PkmnIsConfused",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(confusion.Side))),
        SourceConfusionChangedEvent {Reason:"snapped-out"} confusion => new("sText_PkmnHealedConfusion",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(confusion.Side))),
        SourceConfusionChangedEvent => new(""),
        SourceConfusionSelfHitEvent => new("sText_ItHurtConfusion"),
        SourceFocusEnergyEvent focus => new("gBattleText_GetPumped",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(focus.Side))),
        SourceFlinchEvent {Applied:false} flinch => new("sText_PkmnFlinched",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(flinch.Side))),
        SourceFlinchEvent or SourceForcedOutEvent => new(""),
        SourceMultiHitEvent hits => new("sText_HitXTimes",("B_BUFF1",hits.ActualHits.ToString())),
        SourceLeechSeedChangedEvent {Reason:"move"} seed => new("sText_PkmnSeeded",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(seed.Side))),
        SourceLeechSeedChangedEvent => new(""),
        SourceLeechSeedDrainEvent seed => new("sText_PkmnSappedByLeechSeed",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(seed.DrainedSide))),
        SourceStatusChangedEvent {Reason:"sleep-countdown"} => new(""),
        SourceStatusChangedEvent {Reason:"woke-up"} wake => new("sText_PkmnWokeUp",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(wake.Side))),
        SourceActionBlockedEvent blocked when WildBattleStatus.IsSleeping(blocked.Status) => new("sText_PkmnFastAsleep",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(blocked.Side))),
        SourceDefenseCurledEvent=>new(""),
        SourceBindingChangedEvent {Reason:"bind"} bind=>new("sText_PkmnSqueezedByBind",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(bind.Side)),("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(Opposite(bind.Side)))),
        SourceBindingChangedEvent {Reason:"expired"} bind=>new("sText_PkmnFreedFrom",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(bind.Side)),("B_BUFF1",MoveName(bind.Move??new("pret:MOVE_BIND")))),
        SourceBindingChangedEvent=>new(""),
        SourceBindingDamageEvent bind=>new("sText_PkmnHurtBy",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(bind.Side)),("B_BUFF1",MoveName(bind.Move))),
        SourceStatChangedEvent stat when stat.After > stat.Before => new("sText_AttackersStatRose",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat)), ("B_BUFF2", SourceTrainerPresentationModel.StatChangeText(_battleText,true,stat.After-stat.Before>1))),
        SourceStatChangedEvent stat when stat.Before-stat.After>1 => new("sText_DefendersStatFell",
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat)), ("B_BUFF2", SourceTrainerPresentationModel.StatChangeText(_battleText,false,true))),
        SourceStatAtMaximumEvent stat => new("sText_StatsWontIncrease",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat))),
        SourceStatusChangedEvent { Reason: "faint", After: 0 } => new(""),
        SourceStatusChangedEvent { Reason: "shed-skin", After: 0 } change => new("sText_PkmnsXCuredYProblem",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(change.Side)), ("B_SCR_ACTIVE_ABILITY", _catalog.DisplayNames.AbilityName("ABILITY_SHED_SKIN")),
            ("B_BUFF1", ForestStatusName(change.Before))),
        SourceStatusChangedEvent { Reason: "static", Before: 0, After: WildBattleStatus.Paralysis } change =>
            new("sText_PkmnWasParalyzedBy", ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(Opposite(change.Side))),
                ("B_SCR_ACTIVE_ABILITY", _catalog.DisplayNames.AbilityName("ABILITY_STATIC")), ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        SourceStatusChangedEvent { Reason: "move", Before: 0 } change => new(change.After switch
        {
            >=1 and <=5 => "sText_PkmnFellAsleep",
            WildBattleStatus.Poison => "sText_PkmnWasPoisoned", WildBattleStatus.Burn => "sText_PkmnWasBurned",
            WildBattleStatus.Paralysis => "sText_PkmnWasParalyzed", _ => throw new InvalidDataException("Unsupported forest status message.")
        }, ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        SourceStatusDamageEvent damage => new(damage.Status switch
        {
            WildBattleStatus.Poison => "sText_PkmnHurtByPoison", WildBattleStatus.Burn => "sText_PkmnHurtByBurn",
            _ => throw new InvalidDataException("Unsupported forest residual message.")
        }, ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(damage.Side))),
        SourceActionBlockedEvent { Status: WildBattleStatus.Paralysis } blocked => new("sText_PkmnIsParalyzed",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(blocked.Side))),
        _ => null
    };
    private static BattlerSlot Opposite(BattlerSlot slot) => slot.Opposite;
    private string ForestStatusName(uint status) => _battleText[ForestPresentationStatus(status) switch
    {
        >=1 and <=5 => "gText_Sleep",
        WildBattleStatus.Freeze => "gText_Ice", WildBattleStatus.Poison => "gText_Poison", WildBattleStatus.Burn => "gText_Burn", WildBattleStatus.Paralysis => "gText_Paralysis",
        _ => throw new InvalidDataException("Unsupported forest status name.")
    }];

    private uint ForestDisplayedStatus(CampaignWildBattle battle, WildBattleSide side)
    {
        uint status = (SingleDisplayedBody(battle, side) ??
            throw new InvalidDataException("Single battle display body is missing.")).Status;
        // Teachy TV commits its display after the narrated step. Preserve its current
        // status receipt's text-boundary state without advancing that separate cursor.
        if (TeachyBattle is not null && battle.Phase == CampaignBattlePhase.Events)
            status = WildCurrentEvent(battle) switch
            {
                SourceStatusChangedEvent change when change.Side.Value == (int)side => change.After,
                SourceMedicineAppliedEvent medicine when battle.DisplayBatch!.Display.Members[(int)side] ==
                    new PartyMemberKey(medicine.TargetSide ?? BattleSide.Player, medicine.PartyIndex) => medicine.StatusAfter,
                _ => status
            };
        status = ForestPresentationStatus(status);
        if (!WildBattleStatus.IsSupported(status,true,true)) throw new InvalidDataException("Unsupported displayed forest status.");
        return status;
    }

    private int WildDisplayedHp(CampaignWildBattle battle, PokemonInstance mon, WildBattleSide side)
    {
        if (battle.Phase != CampaignBattlePhase.Events) return mon.CurrentHp;
        var displayed = battle.DisplayBatch?.Display ?? throw new InvalidDataException("Single battle display seed is missing.");
        var span = SourceBattleHpPresentation.Span(WildCurrentEvent(battle), new((int)side), displayed.Members[(int)side]);
        return span is {} hp ? (int)Math.Round(Mathf.Lerp(hp.Before, hp.After, (float)Math.Clamp(_wildElapsed / .4, 0, 1))) : mon.CurrentHp;
    }

    private string ForestStatusId(uint status) => ForestPresentationStatus(status) switch
    {
        >=1 and <=5 => "sleep",
        WildBattleStatus.Freeze => "freeze", WildBattleStatus.Poison => "poison", WildBattleStatus.Burn => "burn", WildBattleStatus.Paralysis => "paralysis",
        _ => throw new InvalidDataException("Unsupported forest status icon.")
    };

    private void LoadForestStatusTextures()
    {
        var source = PretForestStatusUi.Load(_root);
        foreach (var asset in source.Assets)
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(PretForestStatusUi.ReadTexture(_root, asset)) != Error.Ok ||
                image.GetWidth() != asset.Width || image.GetHeight() != asset.Height ||
                image.GetFormat() != Image.Format.Rgba8)
                throw new InvalidDataException("Forest status pixels differ: " + asset.Id);
            _forestStatusTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
        foreach(var asset in PretEarlyStarterStatusUi.Load(_root).Assets)
            {
                using var image=new Image();
                if(image.LoadPngFromBuffer(PretEarlyStarterStatusUi.ReadTexture(_root,asset))!=Error.Ok||image.GetWidth()!=asset.Width||image.GetHeight()!=asset.Height||image.GetFormat()!=Image.Format.Rgba8)
                    throw new InvalidDataException("Early starter status pixels differ: "+asset.Id);
                _forestStatusTextures.Add(asset.Id,ImageTexture.CreateFromImage(image));
            }
    }

    private bool ForestHealthShowsHpLabel(CampaignWildBattle battle, WildBattleSide side) =>
        side == WildBattleSide.Player || ForestDisplayedStatus(battle, side) == 0;

    private void DrawForestHealthStatus(CampaignWildBattle battle, WildBattleSide side)
    {
        uint status = ForestDisplayedStatus(battle, side);
        if (status == 0) return;
        bool player = side == WildBattleSide.Player;
        DrawTexture(_forestStatusTextures["battle-" + (player ? "player-" : "opponent-") + ForestStatusId(status)],
            player ? new(142, 96) : new(20, 30));
        if (!player) DrawTexture(_forestStatusTextures["battle-opponent-hp-border"], new(44, 30));
    }

    // Reused by the existing compact party/capture view. Its row geometry is a
    // functional layout, while the indicator pixels are the distinct source party art.
    private void DrawForestPartyStatus(PartyPokemon owned, Vector2 origin)
    {
        if (owned.IsEgg) return;
        var battle = PresentedWildBattle;
        uint status = battle is not null && owned.InstanceId == battle.OwnedBefore.InstanceId
            ? ForestDisplayedStatus(battle, WildBattleSide.Player) : owned.Status;
        string? id = owned.Pokemon.CurrentHp == 0 ? "fainted" : status == 0 ? null : ForestStatusId(status);
        if (id is not null) DrawTexture(_forestStatusTextures["party-" + id], origin);
    }
}
