using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Application;
using Game.Domain.WildBattle;

internal sealed record SourceTrainerText(string Symbol, params (string Key, string Value)[] Values);
internal sealed record SourceTrainerEventDisplay(ImmutableArray<SourceTrainerText> Texts, bool Unsupported = false)
{
    internal bool Silent => !Unsupported && Texts.IsEmpty;
}

/// <summary>Read-only source-event projection. No RNG, HP, PP, party or command mutation.</summary>
internal static class SourceTrainerPresentationModel
{
    internal const string UnsupportedNotice = "This battle event is not\nsupported yet.";
    private static readonly SourceTrainerEventDisplay Silent = new([]);
    private static readonly SourceTrainerEventDisplay Unsupported = new([], true);
    // EffectWish schedules, animates and returns to MoveEnd without a setup text.
    // Delayed resolution remains on its existing text/healing presentation path.
    internal static bool IsSilentDelayedReceipt(MoveId move, string reason) =>
        reason == "countdown" || move.Value == "pret:MOVE_WISH" && reason == "scheduled";
    internal static uint DisplayStatus(uint status) => WildBattleSourceFieldEffects.IsValidToxic(status) ? WildBattleStatus.Poison : status;
    internal static string AbilityName(string source) => source switch {
        "ABILITY_NONE" => "-------", "ABILITY_COMPOUND_EYES" => "COMPOUNDEYES", "ABILITY_LIGHTNING_ROD" => "LIGHTNINGROD",
        _ when source.StartsWith("ABILITY_", StringComparison.Ordinal) => source[8..].Replace('_', ' '),
        _ => throw new InvalidDataException("Unknown source ability symbol.")
    };
    internal static string? AbilityPreventionSymbol(string ability)=>ability switch {
            "ABILITY_LEVITATE"=>"sText_PkmnMakesGroundMiss","ABILITY_WONDER_GUARD"=>"sText_AvoidedDamage",
            "ABILITY_VOLT_ABSORB" or "ABILITY_WATER_ABSORB"=>"sText_PkmnsXMadeYUseless","ABILITY_FLASH_FIRE"=>"sText_PkmnRaisedFirePowerWith",
            "ABILITY_DAMP"=>"sText_PkmnPreventsUsage","ABILITY_SUCTION_CUPS"=>"sText_PkmnAnchorsItselfWith",
            "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT"=>"sText_PkmnStayedAwakeUsing","ABILITY_LIMBER"=>"sText_PkmnPreventsParalysisWith",
            "ABILITY_IMMUNITY"=>"sText_PkmnPreventsPoisoningWith","ABILITY_WATER_VEIL"=>"sText_PkmnsXPreventsBurns",
            "ABILITY_OWN_TEMPO"=>"sText_PkmnPreventsConfusionWith","ABILITY_OBLIVIOUS"=>"sText_PkmnPreventsRomanceWith",
            "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE"=>"sText_PkmnPreventsStatLossWith",
            "ABILITY_HYPER_CUTTER" or "ABILITY_KEEN_EYE"=>"sText_PkmnsXPreventsYLoss",
            "ABILITY_INNER_FOCUS"=>"sText_PkmnsXPreventsFlinching",_=>null};

    internal static string AbilityPreventionStat(string ability)=>ability switch{"ABILITY_HYPER_CUTTER"=>"ATTACK","ABILITY_KEEN_EYE"=>"accuracy",_=>""};

    private static WildBattleSide Other(WildBattleSide side) => side == WildBattleSide.Player ? WildBattleSide.Opponent : WildBattleSide.Player;
    private static SourceTrainerEventDisplay Text(string symbol, params (string Key, string Value)[] fields) => new([new(symbol, fields)]);

    // The successful source commands own their text even when permanent party
    // storage already matches and therefore emits no conditional write receipt.
    internal static SourceTrainerEventDisplay RecycledItem(string actor, string item, Func<string, string>? itemName) =>
        itemName is null || item == "ITEM_NONE" ? Unsupported : Text("sText_XFoundOneY",
            ("B_ATK_NAME_WITH_PREFIX", actor), ("B_LAST_ITEM", itemName(item)));

    internal static SourceTrainerEventDisplay SwappedItems(string actor, string target,
        string actorItemBefore, string targetItemBefore, Func<string, string>? itemName)
    {
        if (itemName is null || actorItemBefore == "ITEM_NONE" && targetItemBefore == "ITEM_NONE") return Unsupported;
        SourceTrainerText obtained = actorItemBefore == "ITEM_NONE"
            ? new("sText_PkmnObtainedX", ("B_ATK_NAME_WITH_PREFIX", actor), ("B_BUFF1", itemName(targetItemBefore)))
            : targetItemBefore == "ITEM_NONE"
                ? new("sText_PkmnObtainedX2", ("B_DEF_NAME_WITH_PREFIX", target), ("B_BUFF2", itemName(actorItemBefore)))
                : new("sText_PkmnObtainedXYObtainedZ", ("B_ATK_NAME_WITH_PREFIX", actor),
                    ("B_BUFF1", itemName(targetItemBefore)), ("B_DEF_NAME_WITH_PREFIX", target), ("B_BUFF2", itemName(actorItemBefore)));
        return new([new("sText_PkmnSwitchedItems", ("B_ATK_NAME_WITH_PREFIX", actor)), obtained]);
    }

    // Stop at the next printed invocation: caller announcements can have no MoveUsed counterpart.
    internal static bool PairedMoveUsed(IReadOnlyList<WildBattleEvent> events, int index, WildBattleAttackStringEvent attack)
    {
        for (int i = index + 1; i < events.Count && events[i] is not WildBattleAttackStringEvent; i++)
            if (events[i] is WildBattleMoveUsedEvent used && used.Actor == attack.Actor && used.Move == attack.CurrentMove) return true;
        return false;
    }
    internal static MoveId? LastMove(IReadOnlyList<WildBattleEvent> events, int index, WildBattleSide side)
    {
        for (int i = index; i >= 0; i--)
        {
            MoveId? move = events[i] switch {
                WildBattlePpCommandEvent p when p.Actor == side => p.CurrentMove,
                WildBattleAttackStringEvent a when a.Actor == side => a.CurrentMove,
                WildBattleMoveUsedEvent u when u.Actor == side => u.Move,
                WildBattleCalledMoveTransitionEvent t when t.Invocation.Actor == side => t.Invocation.CurrentMove,
                _ => null
            };
            if (move is not null) return move;
        }
        return null;
    }
    private static bool PriorMatching(IReadOnlyList<WildBattleEvent> events, int index, Func<WildBattleEvent, bool> match)
    {
        for (int i = index - 1; i >= 0 && events[i] is not WildBattleAttackStringEvent; i--)
            if (match(events[i])) return true;
        return false;
    }

    internal static SourceTrainerEventDisplay? Describe(IReadOnlyList<WildBattleEvent> events, int index,
        Func<WildBattleSide, string> name, Func<MoveId, string> moveName, Func<WildBattleSide, int, string> partyName,
        Func<string, string>? itemName = null, Func<WildBattleSide, int, string?>? prefixedPartyName = null)
    {
        if (index < 0 || index >= events.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SourceTrainerEventDisplay Atk(string symbol, WildBattleSide side) => Text(symbol, ("B_ATK_NAME_WITH_PREFIX", name(side)));
        switch (events[index])
        {
            case WildBattleAbilityActivatedEvent { Ability: "ABILITY_COLOR_CHANGE" } color:
                return index + 1 < events.Count && events[index + 1] is WildBattleCalledOverlayChangedEvent nextColorOverlay &&
                    nextColorOverlay.Side == color.Side && nextColorOverlay.After?.Type1 is { } nextColorType && nextColorType == nextColorOverlay.After.Type2 &&
                    nextColorType.StartsWith("TYPE_", StringComparison.Ordinal)
                    ? Text("sText_PkmnChangedTypeWith", ("B_DEF_NAME_WITH_PREFIX", name(color.Side)),
                        ("B_DEF_ABILITY", "COLOR CHANGE"), ("B_BUFF1", nextColorType[5..])) : Unsupported;
            case WildBattleCalledOverlayChangedEvent changed when index > 0 &&
                events[index - 1] is WildBattleAbilityActivatedEvent { Ability: "ABILITY_COLOR_CHANGE" } color && color.Side == changed.Side:
                return changed.After?.Type1 is { } colorType && colorType == changed.After.Type2 &&
                    colorType.StartsWith("TYPE_", StringComparison.Ordinal) ? Silent : Unsupported;
            case WildBattleAbilityActivatedEvent { Ability: "ABILITY_ROUGH_SKIN" or "ABILITY_FLAME_BODY" or "ABILITY_VOLT_ABSORB" }:
                return Silent;
            case WildBattleAbilityDamageEvent ability:
                return Text("sText_PkmnHurtsWith", ("B_DEF_NAME_WITH_PREFIX", name(ability.Holder)),
                    ("B_DEF_ABILITY", AbilityName(ability.Ability)), ("B_ATK_NAME_WITH_PREFIX", name(ability.Target)));
            case WildBattleStatusChangedEvent { Reason: "flame-body", After: WildBattleStatus.Burn } burned:
                return Text("sText_PkmnBurnedBy", ("B_SCR_ACTIVE_NAME_WITH_PREFIX", name(Other(burned.Side))),
                    ("B_SCR_ACTIVE_ABILITY", "FLAME BODY"), ("B_EFF_NAME_WITH_PREFIX", name(burned.Side)));
            case WildBattleAbilityPreventedEvent { Ability: "ABILITY_VOLT_ABSORB" or "ABILITY_WATER_ABSORB" } absorb:
                return index + 1 < events.Count && events[index + 1] is WildBattleRecoveredEvent nextRecovery &&
                    nextRecovery.Side == absorb.Target && nextRecovery.Move == absorb.Move ? Silent :
                    Text("sText_PkmnsXMadeYUseless", ("B_DEF_NAME_WITH_PREFIX", name(absorb.Target)),
                        ("B_DEF_ABILITY", AbilityName(absorb.Ability)), ("B_CURRENT_MOVE", moveName(absorb.Move)));
            case WildBattleRecoveredEvent recovery when index > 0 &&
                events[index - 1] is WildBattleAbilityPreventedEvent { Ability: "ABILITY_VOLT_ABSORB" or "ABILITY_WATER_ABSORB" } absorb &&
                absorb.Target == recovery.Side && absorb.Move == recovery.Move:
                return Text("sText_PkmnRestoredHPUsing", ("B_DEF_NAME_WITH_PREFIX", name(recovery.Side)),
                    ("B_DEF_ABILITY", AbilityName(absorb.Ability)));
            case WildBattleAbilityPreventedEvent prevented when AbilityPreventionSymbol(prevented.Ability) is {} symbol:
                string preventionAbility=AbilityName(prevented.Ability);
                return Text(symbol,("B_DEF_NAME_WITH_PREFIX",name(prevented.Target)),("B_DEF_ABILITY",preventionAbility),
                    ("B_EFF_NAME_WITH_PREFIX",name(prevented.Target)),("B_EFF_ABILITY",preventionAbility),
                    ("B_SCR_ACTIVE_NAME_WITH_PREFIX",name(prevented.Target)),("B_SCR_ACTIVE_ABILITY",preventionAbility),
                    ("B_ATK_NAME_WITH_PREFIX",name(prevented.Actor)),("B_CURRENT_MOVE",moveName(prevented.Move)),("B_BUFF1",AbilityPreventionStat(prevented.Ability)));
            case WildBattleAttackStringEvent attack:
                return PairedMoveUsed(events, index, attack) ? Silent : Text("sText_AttackerUsedX",
                    ("B_ATK_NAME_WITH_PREFIX", name(attack.Actor)), ("B_BUFF2", moveName(attack.CurrentMove) + "!"));
            case WildBattleMoveUsedEvent used when used.Move.Value is "pret:MOVE_HEAL_BELL" or "pret:MOVE_AROMATHERAPY":
                return new([new("sText_AttackerUsedX", ("B_ATK_NAME_WITH_PREFIX", name(used.Actor)), ("B_BUFF2", moveName(used.Move) + "!")),
                    new(used.Move.Value == "pret:MOVE_HEAL_BELL" ? "sText_BellChimed" : "sText_SoothingAroma")]);
            case WildBattleHeldItemRecycledEvent recycled:
                return RecycledItem(name(recycled.Actor), recycled.Item, itemName);
            case WildBattleHeldItemsSwappedEvent swapped:
                return SwappedItems(name(swapped.Actor), name(swapped.Target), swapped.ActorItemBefore, swapped.TargetItemBefore, itemName);
            case WildBattlePpCommandEvent or WildBattleDamageAdjustedEvent or WildBattleCalledMoveTransitionEvent or WildBattleMoveTargetResolvedEvent or WildBattleCalledPartySwitchRequiredEvent or WildBattleCalledPartySwitchedEvent:
                return Silent;
            case WildBattleCallerSelectionEvent selection:
                return selection.Caller.Value == "pret:MOVE_NATURE_POWER" && selection.Result is { } selected
                    ? Text("sText_NaturePowerTurnedInto", ("B_CURRENT_MOVE", moveName(selected))) : Silent;
            case WildBattleSubstituteDamagedEvent substitute:
                var substituteTexts = ImmutableArray.CreateBuilder<SourceTrainerText>();
                substituteTexts.Add(new("sText_SubstituteDamaged", ("B_DEF_NAME_WITH_PREFIX", name(substitute.Target))));
                if (substitute.HpAfter == 0) substituteTexts.Add(new("sText_PkmnSubstituteFaded", ("B_DEF_NAME_WITH_PREFIX", name(substitute.Target))));
                return new(substituteTexts.ToImmutable());
            case WildBattleMoveCopiedEvent copied:
                return Text(copied.Permanent ? "sText_PkmnSketchedMove" : "sText_PkmnLearnedMove2",
                    ("B_ATK_NAME_WITH_PREFIX", name(copied.Actor)), ("B_BUFF1", moveName(copied.CopiedMove)));
            case WildBattleFocusPunchPreparedEvent focus:
                return Atk("sText_PkmnTighteningFocus", focus.Actor);
            case WildBattleFocusPunchLostEvent focus:
                return Atk("sText_PkmnLostFocus", focus.Actor);
            case WildBattleSnatchReadyEvent ready:
                return Atk("sText_PkmnWaitsForTarget", ready.Actor);
            case WildBattleMoveSnatchedEvent snatched:
                return Text("sText_PkmnSnatchedMove", ("B_DEF_NAME_WITH_PREFIX", name(snatched.Snatcher)),
                    ("B_SCR_ACTIVE_NAME_WITH_PREFIX", name(snatched.OriginalActor)));
            case WildBattleSnatchPressurePpEvent:
                return Silent;
            case WildBattleGrudgePpEvent grudge:
                return Text("sText_PkmnLostPPGrudge", ("B_ATK_NAME_WITH_PREFIX", name(grudge.Attacker)), ("B_BUFF1", moveName(grudge.Move)));
            case WildBattleMagicCoatEvent coat:
                return Text("sText_PkmnMoveBounced", ("B_ATK_NAME_WITH_PREFIX", name(coat.OriginalActor)), ("B_CURRENT_MOVE", moveName(coat.Move)));
            case WildBattleCalledBeatUpMemberEvent beat:
                return Text("sText_PkmnAttack", ("B_BUFF1", partyName(beat.Side, beat.PartyIndex)));
            case WildBattleCalledSpikesChangedEvent spikes:
                return spikes.After > spikes.Before ? Text("sText_SpikesScattered") : spikes.After == 0
                    ? Atk("sText_PkmnBlewAwaySpikes", spikes.Side) : Unsupported;
            case WildBattleCalledOverlayChangedEvent overlay:
                if (overlay.Move.Value is "pret:MOVE_CONVERSION" or "pret:MOVE_CONVERSION_2" or "pret:MOVE_CAMOUFLAGE")
                    return overlay.After?.Type1 is { } type && type.StartsWith("TYPE_", StringComparison.Ordinal)
                        ? Text("sText_PkmnChangedType", ("B_ATK_NAME_WITH_PREFIX", name(overlay.Side)), ("B_BUFF1", type[5..])) : Unsupported;
                if (overlay.Move.Value == "pret:MOVE_ROLE_PLAY")
                    return overlay.After?.Ability is { } ability ? Text("sText_PkmnCopiedFoe", ("B_ATK_NAME_WITH_PREFIX", name(overlay.Side)),
                        ("B_DEF_NAME_WITH_PREFIX", name(Other(overlay.Side))), ("B_DEF_ABILITY", AbilityName(ability))) : Unsupported;
                if (overlay.Move.Value == "pret:MOVE_SKILL_SWAP")
                    return PriorMatching(events, index, e => e is WildBattleCalledOverlayChangedEvent o && o.Move == overlay.Move)
                        ? Silent : Atk("sText_PkmnSwappedAbilities", overlay.Side);
                return Unsupported;
            case WildBattleCalledPartyChangedEvent party:
                return party.Reason is "heal-bell" or "aromatherapy" or "trick-party-write" or "recycle-party-write" or
                    "active-sync" or "consumed-item" or "steal-party-write" or "knock-off-battle-only" or "changed-item-applied"
                    ? Silent : Unsupported;
            case WildBattleCalledDelayedEvent delayed:
                if (IsSilentDelayedReceipt(delayed.Move, delayed.Reason)) return Silent;
                if (delayed.Move.Value == "pret:MOVE_WISH")
                {
                    var wish = CampaignWishResolutionPresentation.At(events, index);
                    if (wish?.Kind == CampaignWishResolutionKind.Expired) return Silent;
                    if (wish is { Kind: CampaignWishResolutionKind.Recovery or CampaignWishResolutionKind.FullHp, CasterPartyIndex: { } sourceIndex })
                    {
                        string? caster = prefixedPartyName is null ? partyName(delayed.Target, sourceIndex) : prefixedPartyName(delayed.Target, sourceIndex);
                        return caster is null ? Unsupported : Text("sText_PkmnWishCameTrue", ("B_BUFF1", caster));
                    }
                    return Unsupported;
                }
                if (delayed.Move.Value is "pret:MOVE_FUTURE_SIGHT" or "pret:MOVE_DOOM_DESIRE")
                {
                    if (delayed.Reason == "resolved") return Text("sText_PkmnTookAttack", ("B_DEF_NAME_WITH_PREFIX", name(delayed.Target)), ("B_BUFF1", moveName(delayed.Move)));
                    if (delayed.Reason == "scheduled") return delayed.Move.Value == "pret:MOVE_FUTURE_SIGHT"
                        ? Atk("sText_PkmnForesawAttack", Other(delayed.Target))
                        : Text("sText_PkmnChoseXAsDestiny", ("B_ATK_NAME_WITH_PREFIX", name(Other(delayed.Target))), ("B_CURRENT_MOVE", moveName(delayed.Move)));
                }
                return Unsupported;
            case WildBattleStatusChangedEvent status when status.Before == 0 && WildBattleSourceFieldEffects.IsValidToxic(status.After):
                return Text("sText_PkmnBadlyPoisoned", ("B_EFF_NAME_WITH_PREFIX", name(status.Side)));
            case WildBattleStatusDamageEvent status when WildBattleSourceFieldEffects.IsValidToxic(status.Status):
                return Atk("sText_PkmnHurtByPoison", status.Side);
            case WildBattleStatusChangedEvent { Reason: "uproar-wake" } status:
                return Atk("sText_PkmnWokeUpInUproar", status.Side);
            case WildBattleStatusChangedEvent { Reason: "natural-cure" or "heal-bell" or "aromatherapy" }:
                return Silent;
            case WildBattleConfusionChangedEvent { Reason: "teeter-dance" } confusion:
                return Text("sText_PkmnWasConfused", ("B_EFF_NAME_WITH_PREFIX", name(confusion.Side)));
            case WildBattleWeatherDamageEvent { Weather: "hail" } hail:
                return Atk("sText_PkmnPeltedByHail", hail.Side);
            case WildBattleHpChangedEvent { Move.Value: "pret:MOVE_SPIKES" } spikes:
                return Text("sText_PkmnHurtBySpikes", ("B_SCR_ACTIVE_NAME_WITH_PREFIX", name(spikes.Target)));
            case WildBattleMtMoonVolatileEvent v:
                return Volatile(events, index, v, name, moveName);
            case WildBattleMoveFailedEvent failed:
                return Failure(failed, name, moveName);
            default: return null;
        }
    }

    private static SourceTrainerEventDisplay? Failure(WildBattleMoveFailedEvent f, Func<WildBattleSide, string> name, Func<MoveId, string> moveName)
    {
        return f.Reason switch {
            "must-recharge" => Text("sText_PkmnMustRecharge", ("B_ATK_NAME_WITH_PREFIX", name(f.Actor))),
            "already-substitute" => Text("sText_PkmnHasSubstitute", ("B_ATK_NAME_WITH_PREFIX", name(f.Actor))),
            "substitute-insufficient-hp" => Text("sText_TooWeakForSubstitute"),
            "stockpile-full" => Text("sText_PkmnCantStockpile", ("B_ATK_NAME_WITH_PREFIX", name(f.Actor))),
            "stockpile-empty" => Text(f.Move.Value == "pret:MOVE_SWALLOW" ? "sText_FailedToSwallow" : "sText_FailedToSpitUp"),
            "accuracy" or "future-accuracy" or "missed" => Text("sText_AttackMissed", ("B_ATK_NAME_WITH_PREFIX", name(f.Actor))),
            "taunted" => Text("sText_PkmnCantUseMoveTaunt", ("B_ACTIVE_NAME_WITH_PREFIX", name(f.Actor)), ("B_CURRENT_MOVE", moveName(f.Move))),
            "safeguard" => Text("sText_PkmnUsedSafeguard", ("B_DEF_NAME_WITH_PREFIX", name(f.Target))),
            "mist" => Text("sText_PkmnProtectedByMist", ("B_SCR_ACTIVE_NAME_WITH_PREFIX", name(f.Target))),
            "bide-no-energy" or "mirror-move-no-history" or "assist-no-candidates" or "perish-song-already-active" or
            "future-slot-occupied" or "wish-slot-occupied" or "already-tormented" or "already-taunted" or
            "magic-coat-last-action" or "snatch-last-action" or "already-grudge" or "substitute" or "already-type" or "no-different-move-type" or
            "no-landed-move" or "source-still-multiple-turns" or "no-resistant-type" or "uncopyable-ability" or
            "spikes-at-maximum" or "no-eligible-reserve" or "no-eligible-contributor" or "stats-at-minimum" or
            "no-recyclable-item" or "item-swap-not-allowed" or "already-sun" or "already-hail" or "already-mist" or
            "copy-transformed" or "copy-forbidden" or "copy-already-known" or
            "already-light-screen" or "already-safeguard" => Text("sText_ButItFailed"),
            _ => null
        };
    }

    private static SourceTrainerEventDisplay? Volatile(IReadOnlyList<WildBattleEvent> events, int index,
        WildBattleMtMoonVolatileEvent v, Func<WildBattleSide, string> name, Func<MoveId, string> moveName)
    {
        SourceTrainerEventDisplay Atk(string symbol) => Text(symbol, ("B_ATK_NAME_WITH_PREFIX", name(v.Side)));
        SourceTrainerEventDisplay Def(string symbol) => Text(symbol, ("B_DEF_NAME_WITH_PREFIX", name(v.Side)));
        string team = v.Side == WildBattleSide.Player ? "Ally" : "Foe";
        switch (v.Volatile)
        {
            case "substitute": return v.Reason == "move" ? Atk("sText_PkmnMadeSubstitute") : Unsupported;
            case "stockpile": return v.Reason == "move" ? Text("sText_PkmnStockpiled", ("B_ATK_NAME_WITH_PREFIX", name(v.Side)), ("B_BUFF1", v.After.ToString())) : v.Reason is "swallow" or "spit-up" ? Silent : Unsupported;
            case "perish-song":
                if (v.Reason == "move") return PriorMatching(events, index, e => e is WildBattleMtMoonVolatileEvent { Volatile: "perish-song", Reason: "move" }) ? Silent : Text("sText_FaintInThree");
                return v.Reason is "countdown" or "perished" ? Text("sText_PkmnPerishCountFell", ("B_ATK_NAME_WITH_PREFIX", name(v.Side)), ("B_BUFF1", v.Before.ToString())) : Unsupported;
            case "torment": return v.Reason == "move" ? Def("sText_PkmnSubjectedToTorment") : Unsupported;
            case "taunt": return v.Reason == "move" ? Def("sText_PkmnFellForTaunt") : v.Reason == "countdown" ? Silent : Unsupported;
            case "grudge": return v.Reason == "move" ? Atk("sText_PkmnWantsGrudge") : v.Reason == "action-attempt" ? Silent : Unsupported;
            case "magic-coat": return v.Reason == "move" ? Text("sText_PkmnShroudedItself", ("B_ATK_NAME_WITH_PREFIX", name(v.Side)), ("B_CURRENT_MOVE", moveName(new("pret:MOVE_MAGIC_COAT")))) : v.Reason == "turn-end" ? Silent : Unsupported;
            case "sure-hit": return v.Reason == "move" ? Text("sText_PkmnTookAim", ("B_ATK_NAME_WITH_PREFIX", name(Other(v.Side))), ("B_DEF_NAME_WITH_PREFIX", name(v.Side))) : Silent;
            case "called-charge":
                if (v.Reason != "move") return Silent;
                // Sunny SolarBeam sets/clears the charge register in one action without charge text.
                if (events.Skip(index + 1).TakeWhile(e => e is not WildBattleAttackStringEvent)
                    .Any(e => e is WildBattleMtMoonVolatileEvent { Volatile: "called-charge", Reason: "released" } released && released.Side == v.Side)) return Silent;
                string? symbol = LastMove(events, index, v.Side)?.Value switch {
                    "pret:MOVE_RAZOR_WIND" => "sText_PkmnWhippedWhirlwind", "pret:MOVE_SOLAR_BEAM" => "sText_PkmnTookSunlight",
                    "pret:MOVE_SKULL_BASH" => "sText_PkmnLoweredHead", "pret:MOVE_SKY_ATTACK" => "sText_PkmnIsGlowing",
                    "pret:MOVE_FLY" => "sText_PkmnFlewHigh", "pret:MOVE_DIG" => "sText_PkmnDugHole",
                    "pret:MOVE_DIVE" => "sText_PkmnHidUnderwater", "pret:MOVE_BOUNCE" => "sText_PkmnSprangUp", _ => null };
                return symbol is null ? Unsupported : Atk(symbol);
            case "called-bide": return v.Reason == "storing-energy" ? Atk("sText_PkmnStoringEnergy") : v.Reason == "released" ? Atk("sText_PkmnUnleashedEnergy") : Silent;
            case "called-uproar": return v.Reason switch { "move" => Atk("sText_PkmnCausedUproar"), "countdown" => Atk("sText_PkmnMakingUproar"), "uproar-ended" => Atk("sText_PkmnCalmedDown"), _ => Silent };
            case "called-recharge" or "called-rampage" or "called-rollout": return Silent;
            case "sun": return v.Reason switch { "move" => Text("sText_SunlightGotBright"), "countdown" => Text("sText_SunlightStrong"), "expired" => Text("sText_SunlightFaded"), _ => Unsupported };
            case "hail": return v.Reason switch { "move" => Text("sText_StartedHail"), "countdown" => Text("sText_HailContinues"), "expired" => Text("sText_HailStopped"), _ => Unsupported };
            case "mist": return v.Reason == "move" ? Text("gBattleText_MistShroud", ("B_ATK_PREFIX2", team)) : v.Reason == "expired" ? Text("sText_PkmnsXWoreOff", ("B_ATK_PREFIX1", team), ("B_BUFF1", "MIST")) : v.Reason == "countdown" ? Silent : Unsupported;
            case "light-screen" when v.Reason == "brick-break":
                return PriorMatching(events, index, e => e is WildBattleMtMoonVolatileEvent { Reason: "brick-break" }) ? Silent : Text("sText_TheWallShattered");
            case "light-screen": return v.Reason == "move" ? Text("sText_PkmnRaisedSpDef", ("B_ATK_PREFIX2", team), ("B_CURRENT_MOVE", "LIGHT SCREEN")) : v.Reason == "expired" ? Text("sText_PkmnsXWoreOff", ("B_ATK_PREFIX1", team), ("B_BUFF1", "LIGHT SCREEN")) : v.Reason is "countdown" or "brick-break" ? Silent : Unsupported;
            case "safeguard": return v.Reason == "move" ? Text("sText_PkmnCoveredByVeil", ("B_ATK_PREFIX2", team)) : v.Reason == "expired" ? Text("sText_PkmnSafeguardExpired", ("B_ATK_PREFIX3", team)) : v.Reason == "countdown" ? Silent : Unsupported;
            case "reflect" when v.Reason == "brick-break":
                return PriorMatching(events, index, e => e is WildBattleMtMoonVolatileEvent { Reason: "brick-break" }) ? Silent : Text("sText_TheWallShattered");
            case "haze": return Text("sText_StatChangesGone");
            case "psych-up": return Text("sText_PkmnCopiedStatChanges", ("B_ATK_NAME_WITH_PREFIX", name(v.Side)), ("B_DEF_NAME_WITH_PREFIX", name(Other(v.Side))));
            case "fury-cutter" or "rampage" or "dig-underground": return Silent;
            case "protect" or "endure" or "reflect" or "sandstorm" or "identified" or "rain" or "charge" or "water-sport" or "mud-sport" or "follow-me" or "imprison": return null;
            default: return Unsupported;
        }
    }
}
