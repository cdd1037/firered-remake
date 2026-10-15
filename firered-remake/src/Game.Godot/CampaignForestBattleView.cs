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
        if (!_catalog.ForestEnabled) return;
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

    private ForestMessage? ForestWildMessage(WildBattleEvent battleEvent) => battleEvent switch
    {
        WildBattleEffectivenessEvent {Effectiveness:WildBattleEffectiveness.SuperEffective}=>new("sText_SuperEffective"),
        WildBattleEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NotVeryEffective}=>new("sText_NotVeryEffective"),
        WildBattleEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NoEffect} effect=>new("sText_ItDoesntAffect",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(effect.Target))),
        WildBattleMoveFailedEvent failed => failed.Reason switch
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
        WildBattleMoveMissedEvent {Move.Value:"pret:MOVE_LEECH_SEED"} missed => new("sText_PkmnEvadedAttack",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(missed.Target))),
        WildBattleMoveMissedEvent {Move.Value:"pret:MOVE_POISON_POWDER" or "pret:MOVE_SLEEP_POWDER" or "pret:MOVE_STUN_SPORE" or "pret:MOVE_THUNDER_WAVE" or "pret:MOVE_SUPERSONIC"} => new("sText_ButItFailed"),
        WildBattleConfusionChangedEvent {Reason:"move"} confusion => new("sText_PkmnWasConfused",("B_EFF_NAME_WITH_PREFIX",WildPrefixedName(confusion.Side))),
        WildBattleConfusionChangedEvent {Reason:"countdown"} confusion => new("sText_PkmnIsConfused",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(confusion.Side))),
        WildBattleConfusionChangedEvent {Reason:"snapped-out"} confusion => new("sText_PkmnHealedConfusion",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(confusion.Side))),
        WildBattleConfusionChangedEvent => new(""),
        WildBattleConfusionSelfHitEvent => new("sText_ItHurtConfusion"),
        WildBattleFocusEnergyEvent focus => new("gBattleText_GetPumped",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(focus.Side))),
        WildBattleFlinchEvent {Applied:false} flinch => new("sText_PkmnFlinched",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(flinch.Side))),
        WildBattleFlinchEvent or WildBattleForcedOutEvent => new(""),
        WildBattleMultiHitEvent hits => new("sText_HitXTimes",("B_BUFF1",hits.ActualHits.ToString())),
        WildBattleLeechSeedChangedEvent {Reason:"move"} seed => new("sText_PkmnSeeded",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(seed.Side))),
        WildBattleLeechSeedChangedEvent => new(""),
        WildBattleLeechSeedDrainEvent seed => new("sText_PkmnSappedByLeechSeed",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(seed.DrainedSide))),
        WildBattleStatusChangedEvent {Reason:"sleep-countdown"} => new(""),
        WildBattleStatusChangedEvent {Reason:"woke-up"} wake => new("sText_PkmnWokeUp",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(wake.Side))),
        WildBattleActionBlockedEvent blocked when WildBattleStatus.IsSleeping(blocked.Status) => new("sText_PkmnFastAsleep",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(blocked.Side))),
        WildBattleDefenseCurledEvent=>new(""),
        WildBattleBindingChangedEvent {Reason:"bind"} bind=>new("sText_PkmnSqueezedByBind",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(bind.Side)),("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(Opposite(bind.Side)))),
        WildBattleBindingChangedEvent {Reason:"expired"} bind=>new("sText_PkmnFreedFrom",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(bind.Side)),("B_BUFF1",MoveName(bind.Move??new("pret:MOVE_BIND")))),
        WildBattleBindingChangedEvent=>new(""),
        WildBattleBindingDamageEvent bind=>new("sText_PkmnHurtBy",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(bind.Side)),("B_BUFF1",MoveName(bind.Move))),
        WildBattleStatChangedEvent stat when stat.After > stat.Before => new("sText_AttackersStatRose",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat)), ("B_BUFF2", _battleText["gBattleText_Rose"])),
        WildBattleStatChangedEvent stat when stat.Before-stat.After>1 => new("sText_DefendersStatFell",
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat)), ("B_BUFF2", _battleText["sText_StatHarshly"]+"fell!")),
        WildBattleStatAtMaximumEvent stat => new("sText_StatsWontIncrease",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(stat.Target)), ("B_BUFF1", WildStatName(stat.Stat))),
        WildBattleStatusChangedEvent { Reason: "faint", After: 0 } => new(""),
        WildBattleStatusChangedEvent { Reason: "shed-skin", After: 0 } change => new("sText_PkmnsXCuredYProblem",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(change.Side)), ("B_SCR_ACTIVE_ABILITY", "SHED SKIN"),
            ("B_BUFF1", ForestStatusName(change.Before))),
        WildBattleStatusChangedEvent { Reason: "static", Before: 0, After: WildBattleStatus.Paralysis } change =>
            new("sText_PkmnWasParalyzedBy", ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(Opposite(change.Side))),
                ("B_SCR_ACTIVE_ABILITY", "STATIC"), ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        WildBattleStatusChangedEvent { Reason: "move", Before: 0 } change => new(change.After switch
        {
            >=1 and <=5 => "sText_PkmnFellAsleep",
            WildBattleStatus.Poison => "sText_PkmnWasPoisoned", WildBattleStatus.Burn => "sText_PkmnWasBurned",
            WildBattleStatus.Paralysis => "sText_PkmnWasParalyzed", _ => throw new InvalidDataException("Unsupported forest status message.")
        }, ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(change.Side))),
        WildBattleStatusDamageEvent damage => new(damage.Status switch
        {
            WildBattleStatus.Poison => "sText_PkmnHurtByPoison", WildBattleStatus.Burn => "sText_PkmnHurtByBurn",
            _ => throw new InvalidDataException("Unsupported forest residual message.")
        }, ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(damage.Side))),
        WildBattleActionBlockedEvent { Status: WildBattleStatus.Paralysis } blocked => new("sText_PkmnIsParalyzed",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(blocked.Side))),
        _ => null
    };
    private static WildBattleSide Opposite(WildBattleSide side) => side == WildBattleSide.Player ? WildBattleSide.Opponent : WildBattleSide.Player;
    private string ForestStatusName(uint status) => _battleText[ForestPresentationStatus(status) switch
    {
        >=1 and <=5 => "gText_Sleep",
        WildBattleStatus.Freeze => "gText_Ice", WildBattleStatus.Poison => "gText_Poison", WildBattleStatus.Burn => "gText_Burn", WildBattleStatus.Paralysis => "gText_Paralysis",
        _ => throw new InvalidDataException("Unsupported forest status name.")
    }];

    // State already contains the whole committed turn. Undo only later events to
    // recover the status visible now; this also handles acquisition then cure/faint
    // in one queue and the outgoing page of a voluntary switch.
    private uint ForestDisplayedStatus(CampaignWildBattle battle, WildBattleSide side)
    {
        if (!_catalog.ForestEnabled) return 0;
        if (side == WildBattleSide.Player && WildWithdrawing(battle))
            return ForestPresentationStatus(((WildBattlePlayerSwitchedEvent)WildCurrentEvent(battle)!).Previous.Status);
        var forced = side == WildBattleSide.Player ? SsAnnePendingPlayerForceOut(battle) : null;
        uint status = forced?.Previous.Status ?? (side == WildBattleSide.Player ? battle.State.Player.Status : battle.State.Opponent.Status);
        if (battle.Phase == CampaignBattlePhase.Events)
            for (int i = battle.Events.Length - 1; i > battle.EventIndex; i--)
                if (forced is null || battle.Events[i].Sequence < forced.Sequence) switch (battle.Events[i])
                {
                    case WildBattleStatusChangedEvent change when change.Side == side: status = change.Before; break;
                    case WildBattleMedicineAppliedEvent {ActiveTarget:true} medicine when side==(medicine.TargetSide??WildBattleSide.Player):status=medicine.StatusBefore;break;
                    case WildBattlePlayerSwitchedEvent change when side == WildBattleSide.Player: status = change.Previous.Status; break;
                    case WildBattlePlayerReplacedEvent change when side == WildBattleSide.Player: status = change.Previous.Status; break;
                }
        status = ForestPresentationStatus(status);
        if (!WildBattleStatus.IsSupported(status,_catalog.EarlyStarterEnabled,_catalog.Route11Enabled)) throw new InvalidDataException("Unsupported displayed forest status.");
        return status;
    }

    private int WildDisplayedHp(CampaignWildBattle battle, PokemonInstance mon, WildBattleSide side)
    {
        int hp = mon.CurrentHp;
        if (battle.Phase != CampaignBattlePhase.Events) return hp;
        var transition = WildCurrentEvent(battle)! switch
        {
            WildBattleTowerHpEvent tower when tower.Side == side => (tower.HpBefore, tower.HpAfter),
            WildBattleHpChangedEvent change when change.Target == side => (change.HpBefore, change.HpAfter),
            WildBattleIngrainHealEvent roots when roots.Side == side => (roots.HpBefore, roots.HpAfter),
            WildBattleRecoveredEvent recovered when recovered.Side == side => (recovered.HpBefore, recovered.HpAfter),
            WildBattlePotionRecoveredEvent potion when potion.Side == side => (potion.HpBefore, potion.HpAfter),
            WildBattleHeldItemConsumedEvent item when item.Side == side => (item.HpBefore, item.HpAfter),
            WildBattleMedicineAppliedEvent {ActiveTarget:true} medicine when side==(medicine.TargetSide??WildBattleSide.Player)=>(medicine.HpBefore,medicine.HpAfter),
            WildBattleRecoilEvent recoil when recoil.Actor == side => (recoil.HpBefore, recoil.HpAfter),
            WildBattleAbilityDamageEvent ability when ability.Target == side => (ability.HpBefore, ability.HpAfter),
            WildBattleConfusionSelfHitEvent self when self.Side == side => (self.HpBefore, self.HpAfter),
            WildBattleBindingDamageEvent bind when bind.Side == side => (bind.HpBefore, bind.HpAfter),
            WildBattleStatusDamageEvent damage when damage.Side == side => (damage.HpBefore, damage.HpAfter),
            WildBattleWeatherDamageEvent damage when damage.Side == side => (damage.HpBefore, damage.HpAfter),
            WildBattleDrainEvent drain when drain.Actor==side => (drain.HpBefore,drain.HpAfter),
            WildBattleLeechSeedDrainEvent seed when seed.DrainedSide==side => (seed.HpBefore,seed.HpAfter),
            WildBattleLeechSeedDrainEvent seed when seed.ReceivingSide==side => (seed.ReceiverHpBefore,seed.ReceiverHpAfter),
            _ => (hp, hp)
        };
        return (int)Math.Round(Mathf.Lerp(transition.Item1, transition.Item2, (float)Math.Clamp(_wildElapsed / .4, 0, 1)));
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
        if(_catalog.EarlyStarterEnabled)
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
        if (!_catalog.ForestEnabled || owned.IsEgg) return;
        var battle = PresentedWildBattle;
        uint status = battle is not null && owned.InstanceId == battle.OwnedBefore.InstanceId
            ? ForestDisplayedStatus(battle, WildBattleSide.Player) : owned.Status;
        string? id = owned.Pokemon.CurrentHp == 0 ? "fainted" : status == 0 ? null : ForestStatusId(status);
        if (id is not null) DrawTexture(_forestStatusTextures["party-" + id], origin);
    }
}
