using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

public partial class CampaignView
{
    private static readonly SourceTrainerEventDisplay DoubleSilent = new([]);
    private static SourceTrainerEventDisplay DoubleText(string symbol,params (string Key,string Value)[] values)=>new([new(symbol,values)]);
    private string DoubleAbility(string symbol)=>_catalog.DisplayNames.AbilityName(symbol);
    private string DoubleItem(string symbol)=>_catalog.DisplayNames.ItemName(symbol);
    private string DoublePartyName(CampaignDoubleBattle b,BattleSide side,int member)=>side==BattleSide.Player
        ?_session.State.Party[member].Nickname:_catalog.PokemonContent!.SpeciesNames[b.Frame.Party(side).Members[member].Pokemon.SourceSpeciesId];
    private static SourceMoveUsedEvent? DoubleInvocation(CampaignDoubleBattle b,SourceBattleEvent current)=>
        b.Events.LastOrDefault(e=>e.Sequence<=current.Sequence&&e is SourceMoveUsedEvent) as SourceMoveUsedEvent;
    private static MoveId? DoubleLastMove(CampaignDoubleBattle b,SourceBattleEvent current,BattlerSlot actor)
    {
        foreach(var e in b.Events.Where(e=>e.Sequence<=current.Sequence).Reverse())
        {
            switch(e)
            {
                case SourcePpCommandEvent x when x.Actor==actor:return x.CurrentMove;
                case SourceMoveUsedEvent x when x.Actor==actor:return x.Move;
                case SourceAttackStringEvent x when x.Actor==actor:return x.CurrentMove;
                case SourceCalledMoveTransitionEvent x when x.Invocation.Actor==actor:return x.Invocation.CurrentMove;
            }
        }
        return null;
    }
    private static bool DoublePrior(CampaignDoubleBattle b,SourceBattleEvent current,Func<SourceBattleEvent,bool> match)=>
        b.Events.Where(e=>e.Sequence<current.Sequence).Reverse().TakeWhile(e=>e is not SourceAttackStringEvent).Any(match);
    private static SourceAbilityActivatedEvent? DoublePriorAbility(CampaignDoubleBattle b,SourceBattleEvent current)=>
        b.Events.Where(e=>e.Sequence<current.Sequence).Reverse().TakeWhile(e=>e is not (SourceAttackStringEvent or SourceMoveUsedEvent)).OfType<SourceAbilityActivatedEvent>().FirstOrDefault();

    // Format event families against actual positions. No synthetic singles state and no final-frame HP lookup.
    private SourceTrainerEventDisplay? DoubleExtendedEventDisplay(CampaignDoubleBattle b,SourceBattleEvent e)
    {
        SourceTrainerEventDisplay Atk(string symbol,BattlerSlot slot)=>DoubleText(symbol,("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(slot)));
        SourceTrainerEventDisplay Def(string symbol,BattlerSlot slot)=>DoubleText(symbol,("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(slot)));
        if(CampaignStatAnimationPresentation.IsSilentResultReceipt(b,moves:_catalog.PokemonContent!.Catalog.Moves))return DoubleSilent;
        if(DoubleItemEventDisplay(b,e) is {} item)return item;
        var invocation=DoubleInvocation(b,e);
        switch(e)
        {
            case SourceMoveUsedEvent x when x.Move.Value is "pret:MOVE_HEAL_BELL" or "pret:MOVE_AROMATHERAPY":
                return new([new("sText_AttackerUsedX",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),("B_BUFF2",MoveName(x.Move)+"!")),
                    new(x.Move.Value=="pret:MOVE_HEAL_BELL"?"sText_BellChimed":"sText_SoothingAroma")]);
            case SourceCallerSelectionEvent {Caller.Value:"pret:MOVE_NATURE_POWER",Result:{} move}:
                return DoubleText("sText_NaturePowerTurnedInto",("B_CURRENT_MOVE",MoveName(move)));
            case SourceTransformedEvent x:
                return DoubleText("sText_PkmnTransformedInto",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),
                    ("B_BUFF1",_catalog.PokemonContent!.SpeciesNames[_catalog.PresetDoublePokemon!.Species[x.AppearanceSpecies].SourceSpeciesId]));
            case SourceMoveCopiedEvent x:return DoubleText(x.Permanent?"sText_PkmnSketchedMove":"sText_PkmnLearnedMove2",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),("B_BUFF1",MoveName(x.CopiedMove)));
            case SourceCalledOverlayChangedEvent x:return DoubleOverlayMessage(b,x);
            case SourceHeldItemRecycledEvent x:return SourceTrainerPresentationModel.RecycledItem(DoublePrefixed(x.Actor),x.Item,DoubleItem);
            case SourceHeldItemsSwappedEvent x:return SourceTrainerPresentationModel.SwappedItems(DoublePrefixed(x.Actor),DoublePrefixed(x.Target),x.ActorItemBefore,x.TargetItemBefore,DoubleItem);
            case SourceCalledPartyChangedEvent:return DoubleSilent;
            case SourceHeldItemConsumedEvent x:return DoubleConsumedMessage(b,x);
            case SourceHeldItemHealEvent x:return DoubleText("sText_PkmnsItemRestoredHPALittle",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),("B_LAST_ITEM",DoubleItem(x.Item)));
            case SourceHeldItemKnockedOffEvent x:return DoubleText("sText_PkmnKnockedOff",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_LAST_ITEM",DoubleItem(x.Item)));
            case SourceHeldItemStolenEvent x:return DoubleText("sText_PkmnStoleItem",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_LAST_ITEM",DoubleItem(x.Item)));
            case SourceAbilityActivatedEvent x:
                if(x.Ability=="ABILITY_SPEED_BOOST")return DoubleText("sText_PkmnRaisedSpeed",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_SCR_ACTIVE_ABILITY",DoubleAbility(x.Ability)));
                if(x.Ability=="ABILITY_LIGHTNING_ROD")return DoubleText("sText_PkmnsXTookAttack",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_DEF_ABILITY",DoubleAbility(x.Ability)));
                // The other current activation markers precede their typed result, cure, weather or overlay page.
                if(x.Ability is "ABILITY_COLOR_CHANGE" or "ABILITY_TRACE" or "ABILITY_FORECAST" or "ABILITY_DRIZZLE" or "ABILITY_DROUGHT" or "ABILITY_SAND_STREAM" or
                    "ABILITY_RAIN_DISH" or "ABILITY_VOLT_ABSORB" or "ABILITY_WATER_ABSORB" or "ABILITY_FLASH_FIRE" or "ABILITY_LIQUID_OOZE" or
                    "ABILITY_SYNCHRONIZE" or "ABILITY_IMMUNITY" or "ABILITY_LIMBER" or "ABILITY_WATER_VEIL" or "ABILITY_MAGMA_ARMOR" or
                    "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT" or "ABILITY_OWN_TEMPO" or "ABILITY_OBLIVIOUS")return DoubleSilent;
                if(x.Ability=="ABILITY_SOUNDPROOF")return DoubleText("sText_PkmnsXBlocksY",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_DEF_ABILITY",DoubleAbility(x.Ability)),("B_CURRENT_MOVE","PERISH SONG"));
                return null;
            case SourceAbilityPreventedEvent x when !DoubleIntimidateActive(b,x.Actor,x.Sequence):return DoubleAbilityPrevention(b,x);
            case SourceAbilityRecoveredEvent x:return DoubleText("sText_PkmnsXRestoredHPALittle2",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_ATK_ABILITY",DoubleAbility(x.Ability)));
            case SourcePermanentWeatherChangedEvent x:return DoubleText(x.After switch {SourcePermanentWeatherKind.Rain=>"sText_PkmnMadeItRain",SourcePermanentWeatherKind.Sun=>"sText_PkmnsXIntensifiedSun",SourcePermanentWeatherKind.Sandstorm=>"sText_PkmnsXWhippedUpSandstorm",_=>throw new InvalidDataException("Doubles permanent weather differs.")},("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_SCR_ACTIVE_ABILITY",DoubleAbility(x.Ability)));
            case SourceStatChangedEvent x when b.Events.Any(e=>e.Sequence==x.Sequence+1&&e is SourceAbilityActivatedEvent {Ability:"ABILITY_SPEED_BOOST"} a&&a.Side==x.Target):return DoubleSilent;
            case SourceRecoveredEvent x when DoublePriorAbility(b,x) is {Ability:"ABILITY_VOLT_ABSORB" or "ABILITY_WATER_ABSORB"} a:
                return DoubleText("sText_PkmnRestoredHPUsing",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_DEF_ABILITY",DoubleAbility(a.Ability)));
            case SourceRecoveredEvent {Move.Value:"pret:MOVE_REST"}:return DoubleSilent;
            case SourceRecoilEvent {Move.Value:"pret:MOVE_JUMP_KICK" or "pret:MOVE_HI_JUMP_KICK"} x:return Atk("sText_PkmnCrashed",x.Actor);
            case SourceHpChangedEvent {Move.Value:"pret:MOVE_BELLY_DRUM"} x:return Atk("sText_PkmnCutHPMaxedAttack",x.Actor);
            case SourceStatChangedEvent x when DoubleLastMove(b,x,x.Actor)?.Value is "pret:MOVE_HAZE" or "pret:MOVE_PSYCH_UP" or "pret:MOVE_BELLY_DRUM":return DoubleSilent;
            case SourceStatusChangedEvent {Reason:"refresh"} x:return Atk("sText_PkmnStatusNormal",x.Side);
            case SourceDrainEvent x when x.Restored<0:return DoubleText("sText_ItSuckedLiquidOoze");
            case SourceDrainEvent {Move.Value:"pret:MOVE_DREAM_EATER"} x:return Def("sText_PkmnDreamEaten",x.Target);
            case SourceLeechSeedChangedEvent {Reason:"move"} x:return Def("sText_PkmnSeeded",x.Side);
            case SourceLeechSeedChangedEvent {Reason:"rapid-spin"} x:return Atk("sText_PkmnShedLeechSeed",x.Side);
            case SourceLeechSeedChangedEvent:return DoubleSilent;
            case SourceLeechSeedDrainEvent x:return x.Restored<0?DoubleText("sText_ItSuckedLiquidOoze"):Atk("sText_PkmnSappedByLeechSeed",x.DrainedSide);
            case SourceBindingDamageEvent x:return DoubleText("sText_PkmnHurtBy",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_BUFF1",MoveName(x.Move)));
            case SourceBindingChangedEvent x:
                if(x.Reason is "expired" or "rapid-spin")return DoubleText("sText_PkmnFreedFrom",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_BUFF1",MoveName(x.Move??new("pret:MOVE_BIND"))));
                if(x.After>x.Before&&invocation is {})return DoubleText(x.Move?.Value switch {
                    "pret:MOVE_WRAP"=>"sText_PkmnWrappedBy","pret:MOVE_CLAMP"=>"sText_PkmnClamped","pret:MOVE_SAND_TOMB"=>"sText_PkmnTrappedBySandTomb",
                    "pret:MOVE_FIRE_SPIN" or "pret:MOVE_WHIRLPOOL"=>"sText_PkmnTrappedInVortex",_=>"sText_PkmnSqueezedByBind"},("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(invocation.Actor)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));
                return DoubleSilent;
            case SourceRootsChangedEvent x:return x.Rooted?Atk("sText_PkmnPlantedRoots",x.Side):DoubleSilent;
            case SourceIngrainHealEvent x:return Atk("sText_PkmnAbsorbedNutrients",x.Side);
            case SourceYawnEvent x:return x.Reason=="move"&&invocation is {}?DoubleText("sText_PkmnWasMadeDrowsy",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(invocation.Actor)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side))):DoubleSilent;
            case SourceWeatherContinuedEvent x:return CampaignWeatherContinuationPresentation.At(b.Events,b.EventIndex) is null?DoubleSilent:DoubleText(CampaignWeatherContinuationPresentation.ContinuationSymbol(x.Weather));
            case SourceWeatherDamageEvent x:return Atk(x.Weather=="hail"?"sText_PkmnPeltedByHail":"sText_PkmnBuffetedBySandstorm",x.Side);
            case SourceSubstituteDamagedEvent x:
                return new(x.HpAfter==0?[new("sText_SubstituteDamaged",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target))),new("sText_PkmnSubstituteFaded",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)))]:[new("sText_SubstituteDamaged",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)))]);
            case SourceSnatchReadyEvent x:return Atk("sText_PkmnWaitsForTarget",x.Actor);
            case SourceMoveSnatchedEvent x:return DoubleText("sText_PkmnSnatchedMove",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Snatcher)),("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.OriginalActor)));
            case SourceMagicCoatEvent x:return DoubleText("sText_PkmnMoveBounced",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.OriginalActor)),("B_CURRENT_MOVE",MoveName(x.Move)));
            case SourceGrudgePpEvent x:return DoubleText("sText_PkmnLostPPGrudge",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Attacker)),("B_BUFF1",MoveName(x.Move)));
            case SourceSpiteEvent x:return DoubleText("sText_PkmnReducedPP",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_BUFF1",MoveName(x.Move)),("B_BUFF2",(x.PpBefore-x.PpAfter).ToString()));
            case SourceMagnitudeEvent x:return DoubleText("sText_MagnitudeStrength",("B_BUFF1",x.Magnitude.ToString()));
            case SourcePayDayEvent:return DoubleText("sText_CoinsScattered");
            case SourceCalledBeatUpMemberEvent x:return DoubleText("sText_PkmnAttack",("B_BUFF1",DoublePartyName(b,x.Side,x.PartyIndex)));
            case SourceCalledDelayedEvent x:return DoubleDelayedMessage(b,x);
            case SourceCalledSpikesChangedEvent {After:0} x when invocation is {}:return Atk("sText_PkmnBlewAwaySpikes",invocation.Actor);
            case SourceMtMoonVolatileEvent x:return DoubleVolatileMessage(b,x);
            case SourceTowerVolatileEvent x:return !x.Applied||x.Effect=="curse"?DoubleSilent:x.Effect switch {
                "mean-look"=>Def("sText_TargetCantEscapeNow",x.Side),"nightmare"=>Def("sText_PkmnFellIntoNightmare",x.Side),"destiny-bond"=>Atk("sText_PkmnTryingToTakeFoe",x.Side),_=>null};
            case SourceTowerHpEvent x:return x.Effect switch {
                "curse"=>Atk("sText_PkmnAfflictedByCurse",x.Side),"nightmare"=>Atk("sText_PkmnLockedInNightmare",x.Side),
                "curse-cost" when invocation is {}=>DoubleText("sText_PkmnLaidCurse",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(invocation.Target))),
                "destiny-bond" when b.Events.LastOrDefault(e=>e.Sequence<x.Sequence&&e is SourceFaintedEvent) is SourceFaintedEvent faint=>DoubleText("sText_PkmnTookFoe",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(faint.Side))),_=>DoubleSilent};
            case SourceStatusChangedEvent x when x.Reason is "rest":return Atk(x.Before==0?"sText_PkmnWentToSleep":"sText_PkmnSleptHealthy",x.Side);
            case SourceStatusChangedEvent {Reason:"uproar-wake"} x:return Atk("sText_PkmnWokeUpInUproar",x.Side);
            case SourceStatusChangedEvent {After:0,Reason:"transform-immunity"} x when DoublePriorAbility(b,x) is {} cure:
                return DoubleText("sText_PkmnsXCuredYProblem",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_SCR_ACTIVE_ABILITY",DoubleAbility(cure.Ability)),("B_BUFF1",DoubleStatusProblem(x.Before)));
            case SourceStatusChangedEvent {Reason:"synchronize",Before:0} x when DoublePriorAbility(b,x) is {} synchronize:
                return DoubleText(SourceTrainerPresentationModel.DisplayStatus(x.After) switch {WildBattleStatus.Poison=>"sText_PkmnPoisonedBy",WildBattleStatus.Burn=>"sText_PkmnBurnedBy",WildBattleStatus.Paralysis=>"sText_PkmnWasParalyzedBy",_=>throw new InvalidDataException("Synchronize status differs.")},
                    ("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(synchronize.Side)),("B_SCR_ACTIVE_ABILITY",DoubleAbility("ABILITY_SYNCHRONIZE")),("B_EFF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));
            case SourceStatusChangedEvent x when x.Before==WildBattleStatus.Freeze&&x.After==0&&x.Reason!="held-berry":return Atk("sText_PkmnWasDefrosted2",x.Side);
            case SourceStatusChangedEvent x when x.After==0&&x.Reason is "shed-skin" or "immunity" or "limber" or "water-veil" or "magma-armor" or "insomnia" or "vital-spirit":
                return DoubleText("sText_PkmnsXCuredYProblem",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_SCR_ACTIVE_ABILITY",DoubleAbility("ABILITY_"+x.Reason.ToUpperInvariant().Replace('-','_'))),("B_BUFF1",DoubleStatusProblem(x.Before)));
            case SourceConfusionChangedEvent {After:0,Reason:"own-tempo"} x:return DoubleText("sText_PkmnsXCuredYProblem",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_SCR_ACTIVE_ABILITY",DoubleAbility("ABILITY_OWN_TEMPO")),("B_BUFF1","confusion"));
            case SourceConfusionChangedEvent x when x.Reason is "rampage-ended" or "fatigue":return Atk("sText_PkmnFatigueConfusion",x.Side);
            case SourceInfatuationChangedEvent {After:null,Reason:"oblivious"} x:return DoubleText("sText_PkmnsXCuredYProblem",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_SCR_ACTIVE_ABILITY",DoubleAbility("ABILITY_OBLIVIOUS")),("B_BUFF1","love"));
            case SourceInfatuationChangedEvent {After:null}:return DoubleSilent;
            case SourceMoveFailedEvent x:return DoubleFailureMessage(x);
            case SourcePokeFluteEvent x:return x.Phase switch {
                PokeFluteBattlePhase.Played=>DoubleText("sText_PlayedThe",("B_LAST_ITEM","POKé FLUTE")),
                PokeFluteBattlePhase.NoEffect=>DoubleText("sText_PlayedFluteCatchyTune",("B_LAST_ITEM","POKé FLUTE")),
                PokeFluteBattlePhase.Awakened=>DoubleText("sText_PkmnHearingFluteAwoke"),_=>DoubleSilent};
            case SourceSelfDestructEvent or SourceSleepTalkEvent or SourceSnatchPressurePpEvent or SourceMagicCoatPressurePpEvent or SourcePursuitInterceptedEvent or SourceForcedOutEvent:return DoubleSilent;
            default:return null;
        }
    }
    private static string DoubleStatusProblem(uint status)=>WildBattleStatus.IsSleeping(status)?"sleep":SourceTrainerPresentationModel.DisplayStatus(status) switch {
        WildBattleStatus.Poison=>"poison",WildBattleStatus.Burn=>"burn",WildBattleStatus.Paralysis=>"paralysis",WildBattleStatus.Freeze=>"ice",_=>throw new InvalidDataException("Unknown cured status.")};
    private SourceTrainerEventDisplay? DoubleFailureMessage(SourceMoveFailedEvent x)
    {
        string? symbol=x.Reason switch {
            "must-recharge"=>"sText_PkmnMustRecharge","truant"=>"sText_PkmnLoafing","already-substitute"=>"sText_PkmnHasSubstitute",
            "substitute-insufficient-hp"=>"sText_TooWeakForSubstitute","stockpile-full"=>"sText_PkmnCantStockpile",
            "stockpile-empty"=>x.Move.Value=="pret:MOVE_SWALLOW"?"sText_FailedToSwallow":"sText_FailedToSpitUp",
            "taunted"=>"sText_PkmnCantUseMoveTaunt","imprisoned"=>"sText_PkmnCantUseMoveSealed","disabled"=>"sText_PkmnMoveIsDisabled",
            "safeguard"=>"sText_PkmnUsedSafeguard","mist"=>"sText_PkmnProtectedByMist","rooted"=>"sText_PkmnAnchoredItself",
            "hp-full" or "already-full-hp"=>"sText_PkmnHPFull","already-asleep"=>"sText_PkmnAlreadyAsleep",
            "already-poisoned"=>"sText_PkmnAlreadyPoisoned","already-paralyzed"=>"sText_PkmnIsAlreadyParalyzed",
            "already-burned"=>"sText_PkmnAlreadyHasBurn","already-confused"=>"sText_PkmnAlreadyConfused",
            "accuracy" or "future-accuracy" or "missed"=>"sText_AttackMissed","no-pp"=>"sText_NoPPLeft",
            "splash"=>"sText_ButNothingHappened",_=>null};
        return symbol is null?null:DoubleText(symbol,("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),("B_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),
            ("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_CURRENT_MOVE",MoveName(x.Move)));
    }
    private SourceTrainerEventDisplay DoubleAbilityPrevention(CampaignDoubleBattle b,SourceAbilityPreventedEvent x)
    {
        if(x.Ability is "ABILITY_VOLT_ABSORB" or "ABILITY_WATER_ABSORB"&&b.Events.Any(e=>e.Sequence==x.Sequence+1&&e is SourceRecoveredEvent r&&r.Side==x.Target))return DoubleSilent;
        string symbol=SourceTrainerPresentationModel.AbilityPreventionSymbol(x.Ability)??"sText_PkmnsXMadeYIneffective";
        string ability=DoubleAbility(x.Ability);
        return DoubleText(symbol,("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_DEF_ABILITY",ability),("B_EFF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),
            ("B_EFF_ABILITY",ability),("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_SCR_ACTIVE_ABILITY",ability),
            ("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),("B_CURRENT_MOVE",MoveName(x.Move)),("B_BUFF1",SourceTrainerPresentationModel.AbilityPreventionStat(x.Ability)));
    }
    private SourceTrainerEventDisplay? DoubleOverlayMessage(CampaignDoubleBattle b,SourceCalledOverlayChangedEvent x)
    {
        var ability=DoublePriorAbility(b,x);var invocation=DoubleInvocation(b,x);
        if(ability?.Ability=="ABILITY_COLOR_CHANGE"&&x.After?.Type1 is {} color)return DoubleText("sText_PkmnChangedTypeWith",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_DEF_ABILITY",DoubleAbility(ability.Ability)),("B_BUFF1",_catalog.DisplayNames.TypeName(color)));
        if(ability?.Ability=="ABILITY_TRACE"&&x.Source is {} donor&&x.After?.Ability is {} traced)return DoubleText("sText_PkmnTraced",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_BUFF1",DoublePrefixed(donor)),("B_BUFF2",DoubleAbility(traced)));
        if(ability?.Ability=="ABILITY_FORECAST")return DoubleText("sText_PkmnTransformed",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));
        if(x.Move.Value is "pret:MOVE_CONVERSION" or "pret:MOVE_CONVERSION_2" or "pret:MOVE_CAMOUFLAGE"&&x.After?.Type1 is {} type)
            return DoubleText("sText_PkmnChangedType",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_BUFF1",_catalog.DisplayNames.TypeName(type)));
        if(x.Move.Value=="pret:MOVE_ROLE_PLAY"&&invocation is {}&&x.After?.Ability is {} copied)
            return DoubleText("sText_PkmnCopiedFoe",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(invocation.Target)),("B_DEF_ABILITY",DoubleAbility(copied)));
        if(x.Move.Value=="pret:MOVE_SKILL_SWAP")return DoublePrior(b,x,e=>e is SourceCalledOverlayChangedEvent prior&&prior.Move==x.Move)?DoubleSilent:
            DoubleText("sText_PkmnSwappedAbilities",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(invocation?.Actor??x.Side)));
        return null;
    }
    private SourceTrainerEventDisplay DoubleConsumedMessage(CampaignDoubleBattle b,SourceHeldItemConsumedEvent x)
    {
        string? symbol=x.Item switch {
            "ITEM_ORAN_BERRY" or "ITEM_SITRUS_BERRY"=>"sText_PkmnsItemRestoredHealth","ITEM_CHERI_BERRY"=>"sText_PkmnsItemCuredParalysis",
            "ITEM_CHESTO_BERRY"=>"sText_PkmnsItemWokeIt","ITEM_PECHA_BERRY"=>"sText_PkmnsItemCuredPoison","ITEM_RAWST_BERRY"=>"sText_PkmnsItemHealedBurn",
            "ITEM_ASPEAR_BERRY"=>"sText_PkmnsItemDefrostedIt","ITEM_PERSIM_BERRY"=>"sText_PkmnsItemSnappedOut","ITEM_LUM_BERRY"=>"sText_PkmnsItemNormalizedStatus",
            "ITEM_FIGY_BERRY" or "ITEM_WIKI_BERRY" or "ITEM_MAGO_BERRY" or "ITEM_AGUAV_BERRY" or "ITEM_IAPAPA_BERRY"=>"sText_PkmnsItemRestoredHealth",_=>null};
        if(symbol is not null)return DoubleText(symbol,("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_LAST_ITEM",DoubleItem(x.Item)));
        if(x.Item=="ITEM_LANSAT_BERRY")return DoubleText("sText_PkmnUsedXToGetPumped",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_LAST_ITEM",DoubleItem(x.Item)));
        var statChange=b.Events.Where(e=>e.Sequence<x.Sequence).Reverse().TakeWhile(e=>e is not (SourceAttackStringEvent or SourceMoveUsedEvent)).OfType<SourceStatChangedEvent>().FirstOrDefault(s=>s.Target==x.Side&&s.After>s.Before);
        WildBattleStat? stat=x.Item switch {"ITEM_LIECHI_BERRY"=>WildBattleStat.Attack,"ITEM_GANLON_BERRY"=>WildBattleStat.Defense,"ITEM_SALAC_BERRY"=>WildBattleStat.Speed,
            "ITEM_PETAYA_BERRY"=>WildBattleStat.SpecialAttack,"ITEM_APICOT_BERRY"=>WildBattleStat.SpecialDefense,"ITEM_STARF_BERRY"=>statChange?.Stat,_=>null};
        if(stat is {} raised)return DoubleText("sText_UsingItemTheStatOfPkmnRose",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_LAST_ITEM",DoubleItem(x.Item)),("B_BUFF1",WildStatName(raised)),("B_BUFF2",x.Item=="ITEM_STARF_BERRY"?"rose sharply!":"rose!"));
        if(x.Item=="ITEM_LEPPA_BERRY"&&b.Events.Where(e=>e.Sequence<x.Sequence).Reverse().TakeWhile(e=>e is not (SourceAttackStringEvent or SourceMoveUsedEvent)).OfType<SourcePpCommandEvent>().FirstOrDefault(p=>p.PpOwner==x.Side&&p.After>p.Before) is {} pp)
            return DoubleText("sText_PkmnsItemRestoredPP",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_LAST_ITEM",DoubleItem(x.Item)),("B_BUFF1",MoveName(pp.CurrentMove)));
        return new([],true);
    }
    private SourceTrainerEventDisplay? DoubleDelayedMessage(CampaignDoubleBattle b,SourceCalledDelayedEvent x)
    {
        if(SourceTrainerPresentationModel.IsSilentDelayedReceipt(x.Move,x.Reason))return DoubleSilent;
        if(x.Move.Value=="pret:MOVE_WISH")
        {
            var wish=CampaignWishResolutionPresentation.At(b.Events,b.EventIndex);
            if(wish?.Kind==CampaignWishResolutionKind.Expired)return DoubleSilent;
            if(wish is {Kind:CampaignWishResolutionKind.Recovery or CampaignWishResolutionKind.FullHp,CasterPartyIndex:{} member}&&CampaignWishResolutionPresentation.CasterAvailable(b.Frame,x.Target,member))
                return DoubleText("sText_PkmnWishCameTrue",("B_BUFF1",(x.Target.Side==BattleSide.Opponent?b.Frame.BattleKind==WildBattleCalledBattleKind.Trainer?"Foe ":"Wild ":"")+DoublePartyName(b,x.Target.Side,member)));
            return new([],true);
        }
        if(x.Reason=="resolved")return DoubleText("sText_PkmnTookAttack",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_BUFF1",MoveName(x.Move)));
        if(x.Reason=="scheduled"&&DoubleInvocation(b,x) is {} invocation)return DoubleText(x.Move.Value=="pret:MOVE_FUTURE_SIGHT"?"sText_PkmnForesawAttack":"sText_PkmnChoseXAsDestiny",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(invocation.Actor)),("B_CURRENT_MOVE",MoveName(x.Move)));
        return null;
    }
    private SourceTrainerEventDisplay? DoubleVolatileMessage(CampaignDoubleBattle b,SourceMtMoonVolatileEvent x)
    {
        SourceTrainerEventDisplay Atk(string symbol)=>DoubleText(symbol,("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));
        SourceTrainerEventDisplay Def(string symbol)=>DoubleText(symbol,("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));
        string team=x.Side.Side==BattleSide.Player?"Ally":"Foe";var invocation=DoubleInvocation(b,x);
        if(x.Volatile is "rain" or "sun" or "sandstorm" or "hail")
        {
            string[] symbols=x.Volatile switch {
                "rain"=>["sText_StartedToRain","sText_RainContinues","sText_RainStopped"],
                "sun"=>["sText_SunlightGotBright","sText_SunlightStrong","sText_SunlightFaded"],
                "sandstorm"=>["sText_SandstormBrewed","sText_SandstormRages","sText_SandstormSubsided"],
                _=>["sText_StartedHail","sText_HailContinues","sText_HailStopped"]};
            return x.Reason is "move" or "countdown" or "expired"?DoubleText(symbols[x.Reason=="move"?0:x.Reason=="countdown"?1:2]):null;
        }
        if(x.Volatile is "reflect" or "light-screen" or "mist" or "safeguard")
        {
            string move=x.Volatile.Replace('-',' ').ToUpperInvariant();
            if(x.Reason=="countdown")return DoubleSilent;
            if(x.Reason=="brick-break")return DoublePrior(b,x,e=>e is SourceMtMoonVolatileEvent {Reason:"brick-break"})?DoubleSilent:DoubleText("sText_TheWallShattered");
            if(x.Reason=="expired")return DoubleText(x.Volatile=="safeguard"?"sText_PkmnSafeguardExpired":"sText_PkmnsXWoreOff",("B_ATK_PREFIX1",team),("B_ATK_PREFIX3",team),("B_BUFF1",move));
            if(x.Reason=="move")
            {
                bool twoPresent=Enumerable.Range(0,b.DisplayedSlots.Length).Count(i=>(i&1)==(int)x.Side.Side&&b.DisplayedSlots[i].Pokemon.CurrentHp>0)>1;
                return DoubleText(x.Volatile switch {"reflect"=>twoPresent?"sText_PkmnRaisedDefALittle":"sText_PkmnRaisedDef","light-screen"=>twoPresent?"sText_PkmnRaisedSpDefALittle":"sText_PkmnRaisedSpDef","mist"=>"gBattleText_MistShroud",_=>"sText_PkmnCoveredByVeil"},("B_ATK_PREFIX2",team),("B_CURRENT_MOVE",move));
            }
            return null;
        }
        switch(x.Volatile)
        {
            case "substitute":return x.Reason=="move"?Atk("sText_PkmnMadeSubstitute"):DoubleSilent;
            case "stockpile":return x.Reason=="move"?DoubleText("sText_PkmnStockpiled",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_BUFF1",x.After.ToString())):DoubleSilent;
            case "perish-song":
                if(x.Reason=="move")return DoublePrior(b,x,e=>e is SourceMtMoonVolatileEvent {Volatile:"perish-song",Reason:"move"})?DoubleSilent:DoubleText("sText_FaintInThree");
                return DoubleText("sText_PkmnPerishCountFell",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_BUFF1",x.Before.ToString()));
            case "torment":return x.Reason=="move"?Def("sText_PkmnSubjectedToTorment"):DoubleSilent;
            case "taunt":return x.Reason=="move"?Def("sText_PkmnFellForTaunt"):DoubleSilent;
            case "grudge":return x.Reason=="move"?Atk("sText_PkmnWantsGrudge"):DoubleSilent;
            case "magic-coat":return x.Reason=="move"?DoubleText("sText_PkmnShroudedItself",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_CURRENT_MOVE",MoveName(new("pret:MOVE_MAGIC_COAT")))):DoubleSilent;
            case "sure-hit":return x.Reason=="move"&&invocation is {}?DoubleText("sText_PkmnTookAim",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(invocation.Actor)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side))):DoubleSilent;
            case "called-charge":
                if(x.Reason!="move"||b.Events.Where(e=>e.Sequence>x.Sequence).TakeWhile(e=>e is not SourceAttackStringEvent).Any(e=>e is SourceMtMoonVolatileEvent {Volatile:"called-charge",Reason:"released"} release&&release.Side==x.Side))return DoubleSilent;
                string? symbol=DoubleLastMove(b,x,x.Side)?.Value switch {
                    "pret:MOVE_RAZOR_WIND"=>"sText_PkmnWhippedWhirlwind","pret:MOVE_SOLAR_BEAM"=>"sText_PkmnTookSunlight","pret:MOVE_SKULL_BASH"=>"sText_PkmnLoweredHead",
                    "pret:MOVE_SKY_ATTACK"=>"sText_PkmnIsGlowing","pret:MOVE_FLY"=>"sText_PkmnFlewHigh","pret:MOVE_DIG"=>"sText_PkmnDugHole",
                    "pret:MOVE_DIVE"=>"sText_PkmnHidUnderwater","pret:MOVE_BOUNCE"=>"sText_PkmnSprangUp",_=>null};
                return symbol is null?new([],true):Atk(symbol);
            case "called-bide":return x.Reason=="storing-energy"?Atk("sText_PkmnStoringEnergy"):x.Reason=="released"?Atk("sText_PkmnUnleashedEnergy"):DoubleSilent;
            case "called-uproar":return x.Reason switch {"move"=>Atk("sText_PkmnCausedUproar"),"countdown"=>Atk("sText_PkmnMakingUproar"),"uproar-ended"=>Atk("sText_PkmnCalmedDown"),_=>DoubleSilent};
            case "charge":return x.Reason=="move"?Atk("sText_PkmnChargingPower"):DoubleSilent;
            case "water-sport":return x.Reason=="move"?DoubleText("sText_FireWeakened"):DoubleSilent;
            case "mud-sport":return x.Reason=="move"?DoubleText("sText_ElectricityWeakened"):DoubleSilent;
            case "imprison":return x.Reason=="move"?Atk("sText_PkmnSealedOpponentMove"):DoubleSilent;
            case "haze":return DoubleText("sText_StatChangesGone");
            case "psych-up":return invocation is {}?DoubleText("sText_PkmnCopiedStatChanges",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(invocation.Target))):new([],true);
            case "identified":return invocation is {}?DoubleText("sText_PkmnIdentified",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(invocation.Actor)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side))):new([],true);
            case "called-recharge" or "called-rampage" or "called-rollout" or "fury-cutter" or "rampage" or "dig-underground" or "rage" or "minimized":return DoubleSilent;
            case "follow-me" or "helping-hand" or "protect" or "endure" or "spikes":return null;
            default:return null;
        }
    }
}
