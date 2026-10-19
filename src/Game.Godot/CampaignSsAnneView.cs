using System.Collections.Immutable;
using System.Globalization;
using Game.Application;
using Game.Content;
using Game.Domain.Items;
using Game.Domain.WildBattle;
using Game.Domain.Trainers;
using Godot;

/// <summary>Authenticated ship pixels and a presentation clock. No gameplay time or RNG is advanced here.</summary>
public partial class CampaignView
{
    private PretSsAnneBattleArt? _ssAnneArt;
    private Texture2D? _ssAnneWake, _ssAnneSmoke;
    private long? _ssAnneDepartureSequence;
    private double _ssAnneDepartureFrames;
    private int _ssAnneInitialSpriteX, _ssAnneDepartureDuration;
    private bool _ssAnneSecondHorn;
    private bool SsAnneDepartureActive => _ssAnneDepartureSequence is not null &&
        _session.State.World.Player.MapId.Value == SsAnneSourceDeparture.Exterior &&
        _session.State.Actors.TryGetValue(SsAnneSourceDeparture.Boat, out var boat) && boat.Visible;
    private bool SsAnneDepartureInputLocked => SsAnneDepartureActive && _session.State.Script.Wait is not null;
    private int SsAnneDepartureFrame => Math.Min(_ssAnneDepartureDuration, (int)Math.Floor(_ssAnneDepartureFrames + 1e-7));
    private int SsAnneBoatOffset => SsAnneSourceDeparture.OffsetAt(SsAnneDepartureFrame, _ssAnneInitialSpriteX);
    private ImmutableArray<InventoryStack> FieldItemStacks => _session.State.ItemMenu is { Origin: "bag" } menu &&
        (menu.View == "berries" || menu.View == "message" && menu.ReturnView == "berries")
        ? _session.State.Inventory!.Berries ?? [] : _session.State.Inventory!.BagItems;
    private string HmCannotForgetText => string.Join('\n', PretBattleText.Pages(_battleText, "sText_HMMovesCantBeForgotten", ImmutableDictionary<string, string>.Empty));

    private void LoadSsAnnePresentation()
    {
        if (_catalog.SsAnnePokemon is not { } source) return;

        RegisterPokemonSprites(source);

        RegisterPokemonIcons(source);
        _ssAnneArt = PretSsAnneBattleArt.Load(_root);
        foreach (var portrait in _ssAnneArt.Portraits)
        {
            using var image = NativeImages.LoadPng(portrait.Png, 64, 64);
            _forestTrainerPortraits.Add(portrait.Id, ImageTexture.CreateFromImage(image));
        }
        Texture2D Sheet(SsAnneDepartureSheet sheet)
        {
            using var image = NativeImages.LoadPng(_ssAnneArt.ReadPng(sheet.Id), sheet.Width, sheet.Height);
            return ImageTexture.CreateFromImage(image);
        }
        _ssAnneWake = Sheet(_ssAnneArt.Wake); _ssAnneSmoke = Sheet(_ssAnneArt.Smoke);
        var items = PretSsAnneItems.LoadProject(_root);
        foreach (var icon in items.Icons.Where(i => !_earlyItemIcons.ContainsKey(i.Item)))
        {
            
            _earlyItemIcons.Add(icon.Item, ItemTexture("data/items/ssanne", icon));
        }
        var document = _questPocketUi ?? throw new InvalidDataException("S.S. Anne key pocket missing.");
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        foreach (var value in items.Items.Add(items.BerryPouch).Where(x => x.Metadata.Item.Id.Value is "pret:ITEM_BERRY_POUCH" or "pret:ITEM_GREAT_BALL"))
        {
            var item = value.Metadata; var icon = items.Icons.Single(i => i.Item == item.Item.Id);
            string id = "ssanne-" + item.Item.SourceItemSymbol.ToLowerInvariant();
            
            _questPocketTextures.Add(id, ItemTexture("data/items/ssanne", icon));
            document = document with
            {
                Items = document.Items.Add(new(item.Item.Id.Value, item.Item.SourceItemSymbol, item.Item.Name, item.Description, id, item.Item.FieldUseFunction)),
                Assets = document.Assets.Add(new(id,icon.PngPath,icon.Width,icon.Height,position.X,position.Y))
            };
        }
        _questPocketUi = document;
    }

    private void SynchronizeSsAnnePresentation(CampaignState? before, CampaignState after)
    {
        if (before is null || before.World.Player.MapId != after.World.Player.MapId)
        { _ssAnneDepartureSequence = null; _ssAnneDepartureFrames = 0; _ssAnneSecondHorn = false; }
        if (before?.ItemMenu is { Origin: "bag", View: "berries" } && after.ItemMenu is { Origin: "bag", View: "bag" })
        {
            _questPocket = 1;
            _questPocketCursor = Math.Max(0, Enumerable.Range(0, QuestPocketStacks.Length).FirstOrDefault(i => QuestPocketStacks[i].Item.Value == "pret:ITEM_BERRY_POUCH", 0));
        }
        if (after.World.Player.MapId.Value != SsAnneSourceDeparture.Exterior) return;
        var effect = after.PresentationEffects.LastOrDefault(e => e.Opcode == "ssanne_departure");
        if (effect is null || effect.Sequence == _ssAnneDepartureSequence) return;
        if (effect.Args.Length != 2 || !int.TryParse(effect.Args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(effect.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int frames) || frames != SsAnneSourceDeparture.TotalFrames(x))
            throw new InvalidDataException("S.S. Anne departure effect differs.");
        _ssAnneDepartureSequence = effect.Sequence; _ssAnneDepartureFrames = 0;
        _ssAnneInitialSpriteX = x; _ssAnneDepartureDuration = frames; _ssAnneSecondHorn = false; _held = null;
        SourceSound(_ssAnneArt!.Departure.SoundId);
    }

    private void ProcessSsAnnePresentation(double delta)
    {
        if (!SsAnneDepartureActive) return;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _ssAnneDepartureFrames = Math.Min(_ssAnneDepartureDuration, _ssAnneDepartureFrames + delta * 60);
        if (!_ssAnneSecondHorn && SsAnneDepartureFrame >= SsAnneSourceDeparture.InitFrames + SsAnneSourceDeparture.RunFrames(_ssAnneInitialSpriteX))
        { _ssAnneSecondHorn = true; SourceSound(_ssAnneArt!.Departure.SoundId); }
    }

    private void DrawSsAnneActor(string id, SpriteAsset asset, Pose pose, Vector2 camera)
    {
        if (id != SsAnneSourceDeparture.Boat || !SsAnneDepartureActive) { DrawActor(asset, pose, camera); return; }
        // x2 is a render offset, never an application actor movement.
        DrawActor(asset, pose, camera - new Vector2(SsAnneBoatOffset, 0));
    }

    private void DrawSsAnneWake()
    {
        if (!SsAnneDepartureActive || SsAnneDepartureFrame < SsAnneSourceDeparture.InitFrames) return;
        var art = _ssAnneArt!; int age = SsAnneDepartureFrame - SsAnneSourceDeparture.InitFrames;
        int x = _ssAnneInitialSpriteX + SsAnneBoatOffset + art.Departure.WakeBoatOffsetX + Math.Min((age + 1) / art.Departure.WakeDriftTickDivisor, art.Departure.WakeMaximumDrift);
        if (x < art.Departure.WakeDestroyXExclusive) return;
        var frame = SsAnneSheetFrame(art.Wake, age);
        DrawTextureRectRegion(_ssAnneWake!, new(x - 8, art.Departure.WakeCenterY - 16, frame.Width, frame.Height), new(frame.X, frame.Y, frame.Width, frame.Height));
    }

    private void DrawSsAnneSmoke()
    {
        if (!SsAnneDepartureActive) return;
        var art = _ssAnneArt!; int run = SsAnneDepartureFrame - SsAnneSourceDeparture.InitFrames;
        int lifetime = art.Smoke.Frames.Sum(f => f.Duration);
        for (int born = art.Departure.SmokePeriodTicks; born <= Math.Min(run, SsAnneSourceDeparture.RunFrames(_ssAnneInitialSpriteX)); born += art.Departure.SmokePeriodTicks)
        {
            int age = run - born;
            // AnimateSprites calls callback before AnimateSprite: animEnded is observed one tick later.
            if (age < 0 || age > lifetime) continue;
            // The source spawns smoke before assigning the new displacement that tick.
            int x = _ssAnneInitialSpriteX - (born - 1) / art.Departure.BoatMoveTickDivisor + art.Departure.SmokeBoatOffsetX;
            if (x < art.Departure.SmokeMinimumSpawnX) continue;
            var frame = SsAnneSheetFrame(art.Smoke, age);
            DrawTextureRectRegion(_ssAnneSmoke!, new(x + (age + 1) / art.Departure.SmokeDriftTickDivisor - 8, art.Departure.SmokeCenterY - 8, frame.Width, frame.Height), new(frame.X, frame.Y, frame.Width, frame.Height));
        }
    }

    private SsAnneTrainerPlayerForcedOutEvent? SsAnnePendingPlayerForceOut(CampaignWildBattle battle)
    {
        if (battle.Phase != CampaignBattlePhase.Events || _session.State.ForestTrainerBattle is not { } trainer) return null;
        long sequence = trainer.CurrentTrainerEvent?.Sequence ?? WildCurrentEvent(battle)?.Sequence ?? long.MaxValue;
        return trainer.TrainerEvents.OfType<SsAnneTrainerPlayerForcedOutEvent>().FirstOrDefault(e => e.Sequence > sequence);
    }

    private ForestMessage? SsAnneWildMessage(WildBattleEvent current) => current switch
    {
        WildBattleAbilityPreventedEvent { Ability: "ABILITY_FLASH_FIRE" } ability => new("sText_PkmnRaisedFirePowerWith",
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability))),
        WildBattleAbilityPreventedEvent { Ability: "ABILITY_CLEAR_BODY" } ability => new("sText_PkmnPreventsStatLossWith",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_SCR_ACTIVE_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability))),
        WildBattleAbilityPreventedEvent { Ability: "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT" } ability => new("sText_PkmnStayedAwakeUsing",
            ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability))),
        WildBattleStatusChangedEvent { Reason: "rest" } rest => new(rest.Before == 0 ? "sText_PkmnWentToSleep" : "sText_PkmnSleptHealthy",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(rest.Side))),
        WildBattleMoveFailedEvent { Move.Value: "pret:MOVE_REST", Reason: "already-asleep" } failed => new("sText_PkmnAlreadyAsleep2",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(failed.Actor))),
        WildBattleMoveFailedEvent { Reason: "already-full-hp" } failed => new("sText_PkmnHPFull", ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(failed.Target))),
        WildBattleDrainEvent { Restored: < 0 } or WildBattleLeechSeedDrainEvent { Restored: < 0 } => new("sText_ItSuckedLiquidOoze"),
        _ => null
    };

    private static SsAnneArtFrame SsAnneSheetFrame(SsAnneDepartureSheet sheet, int age)
    {
        int length = sheet.Frames.Sum(f => f.Duration); int tick = sheet.Loops ? age % length : Math.Min(age, length - 1);
        foreach (var frame in sheet.Frames) { if (tick < frame.Duration) return frame; tick -= frame.Duration; }
        throw new InvalidDataException("S.S. Anne source animation frame missing.");
    }
}
