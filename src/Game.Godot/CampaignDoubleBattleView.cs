using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Godot;

/// <summary>Four canonical positions, original doubles healthboxes and source event pages.
/// This view never resolves a turn or substitutes a pair of singles for the actual battle.
/// Six dedicated source move scripts share the singles animation owner and four-slot coordinates.</summary>
public partial class CampaignView
{
    private readonly Dictionary<string, Texture2D> _doubleTextures = new(StringComparer.Ordinal);
    private static readonly Vector2[] DoubleHealthCenters = [new(159,75),new(44,19),new(171,100),new(32,44)];
    private static readonly Vector2[] DoubleSpriteOffsets = [new(-40,0),new(24,0),new(18,8),new(-24,-8)];
    private string? _doubleKey, _doubleNotice;
    private ImmutableArray<string> _doublePages = [];
    private int _doublePage, _doubleCursor, _doubleChosenMove;
    private string _doubleMenu = "actions";
    private double _doubleElapsed;
    private int? _doubleSecondCry;
    private bool _doubleOpening;
    private string? _doubleOpenedBattleId;
    private readonly Dictionary<int,(WildRewardLevelEvent Level,bool Totals)> _doubleRewardStats=[];
    private bool DoubleBattleVisible => _session.State.DoubleBattle is { Stage: not CampaignDoubleBattleStage.Intro };

    private bool DoubleIntroInput(InputEventKey key)
    {
        if(_session.State.DoubleBattle?.Stage!=CampaignDoubleBattleStage.Intro||key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape)return false;
        _held=null;
        if(key.Pressed&&!key.Echo&&key.Keycode==Key.Enter&&FieldTextComplete&&
            _waitElapsed>=_waitDuration&&_tracks.All(t=>t.Time>=t.Duration))Apply(new("ack"));
        return true;
    }

    private void LoadDoubleBattlePresentation()
    {
        if (_doubleTextures.Count != 0) return;
        var folder = Path.Combine(_root, "content/generated/ui/double-battle-ui");
        var bytes = File.ReadAllBytes(Path.Combine(folder, "double-battle-ui.json"));
        using var json = JsonDocument.Parse(bytes);
        foreach (var asset in json.RootElement.GetProperty("assets").EnumerateArray())
        {
            string file = asset.GetProperty("path").GetString()!;
            var id = asset.GetProperty("id").GetString()!;
            var shared = id is "hp-label" or "hp-green" or "hp-yellow" or "hp-red";
            var assetFolder = shared ? Path.Combine(Path.GetDirectoryName(folder)!, "lab-battle-ui") : folder;
            var expected = Path.GetRelativePath(_root, Path.Combine(assetFolder, id + ".png")).Replace('\\', '/');
            if (Path.GetFileName(id) != id || file != expected) throw new InvalidDataException("Doubles texture path differs.");
            var data = File.ReadAllBytes(Path.Combine(_root, file));
            using var image = new Image();
            if (image.LoadPngFromBuffer(data) != Error.Ok) throw new InvalidDataException("Doubles PNG differs.");
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != asset.GetProperty("width").GetInt32() || image.GetHeight() != asset.GetProperty("height").GetInt32())
                throw new InvalidDataException("Doubles source pixels differ.");
            _doubleTextures.Add(asset.GetProperty("id").GetString()!, ImageTexture.CreateFromImage(image));
        }
    }

    private string DoubleName(int slot)
    {
        var body = DoubleDisplayedBody(slot);
        var b=_session.State.DoubleBattle!;
        int member=b.CurrentEvent is SourceCalledPartySwitchedEvent change&&change.Side.Value==slot?(DoubleSwitchSending(change)?change.IncomingIndex:change.OutgoingIndex):b.DisplayedMembers[slot].Index;
        return (slot & 1) == 0 ? _catalog.PokemonDisplayName(_session.State.Party[member]) :
            PresentedBattleSpeciesName(WildBattleTransformationRules.PersistentPokemon(body).SourceSpeciesId);
    }
    private bool DoubleSwitchSending(SourceCalledPartySwitchedEvent change)=>change.Kind is WildBattleCalledSwitchKind.FaintedReplacement or WildBattleCalledSwitchKind.BatonPass or WildBattleCalledSwitchKind.Forced||_doublePage>0;
    private WildBattleCombatant DoubleDisplayedBody(int slot)
    {
        var b=_session.State.DoubleBattle!;
        if(b.CurrentEvent is SourceCalledPartySwitchedEvent change&&change.Side.Value==slot&&DoubleSwitchSending(change))return change.Replacement?.Body??throw new InvalidDataException("Doubles switch event is missing its entry snapshot.");
        if(b.CurrentEvent is SourcePlayerSwitchedEvent x&&x.Slot.Value==slot)return _doublePage==0?x.Previous.Body:x.Replacement.Body;
        if(b.CurrentEvent is SourcePlayerReplacedEvent y&&y.Slot.Value==slot)return y.Replacement.Body;
        return b.DisplayedSlots[slot];
    }
    private string DoubleBodyName(WildBattleCombatant body)
    {var p=WildBattleTransformationRules.PersistentPokemon(body);return _catalog.PokemonDisplayName(_session.State.Party.First(m=>m.Pokemon.Personality==p.Personality&&m.Pokemon.OriginalTrainerId==p.OriginalTrainerId));}
    private string DoublePrefixed(BattlerSlot slot) => (slot.Side == BattleSide.Opponent ? _battleSelectionText.FoePrefix : "") + DoubleName(slot.Value);
    private ImmutableArray<string> DoubleSource(string symbol, params (string Key,string Value)[] extra)
    {
        var values = new Dictionary<string,string>(StringComparer.Ordinal) {
            ["B_PLAYER_NAME"]=_session.State.PlayerName,["PLAYER"]=_session.State.PlayerName,
            ["B_TRAINER1_CLASS"]=DoubleTrainerClass,["B_TRAINER1_NAME"]=DoubleTrainerName,
            ["B_PLAYER_MON1_NAME"]=DoubleName(0),["B_PLAYER_MON2_NAME"]=DoubleName(2),
            ["B_OPPONENT_MON1_NAME"]=DoubleName(1),["B_OPPONENT_MON2_NAME"]=DoubleName(3)
        };
        foreach(var (key,value) in extra) values[key]=value;
        return PretBattleText.Pages(_battleText,symbol,values);
    }
    private void SynchronizeDoubleBattle()
    {
        if (_session.State.DoubleBattle is not {} b || b.Stage == CampaignDoubleBattleStage.Intro)
        { _doubleKey=null;_doubleNotice=null;_doublePages=[];_doubleMenu="actions";_doubleOpening=false;if(_session.State.DoubleBattle is null)_doubleOpenedBattleId=null;return; }
        LoadDoubleBattlePresentation();
        string key=$"{b.Id}:{b.Stage}:{b.Frame.EventSequence}:{b.EventIndex}:{b.FinishPageIndex}:{b.PlayerSelections.Length}:{b.SelectingSlot}:{b.ReplacementSlot}";
        if (_doubleKey==key) return;
        _doubleOpening=b.Id!=_doubleOpenedBattleId&&b.Stage==CampaignDoubleBattleStage.Combat&&b.Frame.Turn==0&&b.EventIndex==0&&b.PlayerSelections.IsEmpty;
        if(_doubleOpening)_doubleOpenedBattleId=b.Id;
        _doubleRewardStats.Clear();
        _doubleKey=key;_doublePage=0;_doubleElapsed=0;_doubleNotice=null;_doublePages=[];_doubleCursor=0;
        _doubleMenu=b.LearningPending?"learn-intro":b.ReplacementSlot is not null?"party":"actions";
        if (b.Stage==CampaignDoubleBattleStage.Finished)
        { if(b.FinishPageIndex<b.FinishPages.Length)_doublePages=SourceTrainerPresentationModel.ScrollResultPages(TrainerTowerBattleSpeech(b.FinishPageIndex) ?? b.FinishPages[b.FinishPageIndex]).ToImmutableArray();return; }
        if(b.LearningPending)
        {
            CaptureMoveSelection("double:" + b.Id, DoubleLearningPokemon(b), b.Frame.Rewards!.Pending!.Moves[0].Move);
            var pending=b.Frame.Rewards!.Pending!;string name=_catalog.PokemonDisplayName(_session.State.Party[pending.Recipient.Index]);
            _doublePages=DoubleSource("sText_TryToLearnMove1",("B_BUFF1",name),("B_BUFF2",MoveName(pending.Moves[0].Move)))
                .AddRange(DoubleSource("sText_TryToLearnMove2",("B_BUFF1",name)))
                .AddRange(DoubleSource("sText_TryToLearnMove3",("B_BUFF2",MoveName(pending.Moves[0].Move))));
            return;
        }
        if(_doubleOpening)_doublePages=DoubleSource("sText_Trainer1WantsToBattle").AddRange(DoubleSource("sText_Trainer1SentOutTwoPkmn")).AddRange(DoubleSource("sText_GoTwoPkmn"));
        else if(b.CurrentEvent is {} e)_doublePages=DoubleEventPages(b,e);
    }
    private ImmutableArray<string> DoubleEventPages(CampaignDoubleBattle b,SourceBattleEvent e)
    {
        var pages=ImmutableArray.CreateBuilder<string>();
        void Source(string symbol,params (string Key,string Value)[] values)=>pages.AddRange(DoubleSource(symbol,values));
        void Atk(string symbol,BattlerSlot actor)=>Source(symbol,("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(actor)));
        void Def(string symbol,BattlerSlot target)=>Source(symbol,("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(target)));
        if (DoubleExtendedEventDisplay(b,e) is {} display)
        {
            if (!display.Unsupported)
            {
                foreach (var line in display.Texts) Source(line.Symbol,line.Values);
                return pages.ToImmutable();
            }
            pages.Add(SourceTrainerPresentationModel.UnsupportedNotice);
            Write("unsupportedDoublePresentation",new{type=e.GetType().Name,e.Sequence});
            return pages.ToImmutable();
        }
        switch(e)
        {
            case SourceStartedEvent:
                Source("sText_Trainer1WantsToBattle");Source("sText_Trainer1SentOutTwoPkmn");Source("sText_GoTwoPkmn");break;
            case SourceAttackStringEvent attack:
                if(!b.Events.Skip(b.EventIndex+1).TakeWhile(x=>x is not SourceAttackStringEvent).OfType<SourceMoveUsedEvent>().Any(x=>x.Actor==attack.Actor&&x.Move==attack.CurrentMove))
                    Source("sText_AttackerUsedX",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(attack.Actor)),("B_BUFF2",MoveName(attack.CurrentMove)+"!"));break;
            case SourceMoveUsedEvent used:Source("sText_AttackerUsedX",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(used.Actor)),("B_BUFF2",MoveName(used.Move)+"!"));break;
            case SourceMoveMissedEvent missed:Atk("sText_AttackMissed",missed.Actor);break;
            case SourceMoveFailedEvent {Reason:"protected"} x:Def("sText_PkmnProtectedItself",x.Target);break;
            case SourceMoveFailedEvent:Source("sText_ButItFailed");break;
            case SourceCriticalHitEvent:Source("sText_CriticalHit");break;
            case SourceEffectivenessEvent x when x.Effectiveness==WildBattleEffectiveness.SuperEffective:Source("sText_SuperEffective");break;
            case SourceEffectivenessEvent x when x.Effectiveness==WildBattleEffectiveness.NotVeryEffective:Source("sText_NotVeryEffective");break;
            case SourceEffectivenessEvent x when x.Effectiveness==WildBattleEffectiveness.NoEffect:Def("sText_ItDoesntAffect",x.Target);break;
            case SourceFaintedEvent faint:Atk("sText_AttackerFainted",faint.Side);break;
            case SourceStatChangedEvent stat when DoubleIntimidateActive(b,stat.Actor,stat.Sequence):
                Source("sText_PkmnCutsAttackWith",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(stat.Actor)),("B_SCR_ACTIVE_ABILITY",DoubleAbility("ABILITY_INTIMIDATE")),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(stat.Target)));break;
            case SourceStatChangedEvent stat:
                Source(stat.After>stat.Before?"sText_AttackersStatRose":"sText_DefendersStatFell",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(stat.Target)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(stat.Target)),
                    ("B_BUFF1",WildStatName(stat.Stat)),("B_BUFF2",stat.After>stat.Before?(stat.After-stat.Before>1?"rose sharply!":"rose!"):(stat.Before-stat.After>1?"harshly fell!":"fell!")));break;
            case SourceStatAtMinimumEvent stat:Source("sText_StatsWontDecrease",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(stat.Target)),("B_BUFF1",WildStatName(stat.Stat)));break;
            case SourceStatAtMaximumEvent stat:Source("sText_StatsWontIncrease",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(stat.Target)),("B_BUFF1",WildStatName(stat.Stat)));break;
            case SourceMultiHitEvent hit:Source("sText_HitXTimes",("B_BUFF1",hit.ActualHits.ToString()));break;
            case SourceRecoilEvent x:Atk("sText_PkmnHitWithRecoil",x.Actor);break;
            case SourceConfusionSelfHitEvent:Source("sText_ItHurtConfusion");break;
            case SourceConfusionChangedEvent x when x.Reason=="snapped-out":Atk("sText_PkmnHealedConfusion",x.Side);break;
            case SourceConfusionChangedEvent x when x.Reason=="countdown":Atk("sText_PkmnIsConfused",x.Side);break;
            case SourceConfusionChangedEvent x when x.After>0:Source("sText_PkmnWasConfused",("B_EFF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));break;
            case SourceStatusChangedEvent x when x.Reason=="woke-up":Atk("sText_PkmnWokeUp",x.Side);break;
            case SourceStatusChangedEvent x when x.Before==0&&x.After!=0&&DoubleContactHolder(b,x) is {} holder:
                Source(WildBattleStatus.IsSleeping(x.After)?"sText_PkmnMadeSleep":x.After switch {
                    WildBattleStatus.Poison=>"sText_PkmnPoisonedBy",WildBattleStatus.Burn=>"sText_PkmnBurnedBy",
                    WildBattleStatus.Paralysis=>"sText_PkmnWasParalyzedBy",_=>throw new InvalidDataException("Contact status differs.")},
                    ("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(holder.Side)),("B_SCR_ACTIVE_ABILITY",DoubleAbility(holder.Ability)),("B_EFF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));break;
            case SourceStatusChangedEvent x when x.Before==0&&x.After!=0:
                Source(WildBattleStatus.IsSleeping(x.After)?"sText_PkmnFellAsleep":ForestPresentationStatus(x.After) switch {
                    WildBattleStatus.Poison=>WildBattleSourceFieldEffects.IsValidToxic(x.After)?"sText_PkmnBadlyPoisoned":"sText_PkmnWasPoisoned",
                    WildBattleStatus.Burn=>"sText_PkmnWasBurned",WildBattleStatus.Paralysis=>"sText_PkmnWasParalyzed",WildBattleStatus.Freeze=>"sText_PkmnWasFrozen",_=>throw new InvalidDataException("Doubles status message differs.")},("B_EFF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));break;
            case SourceStatusDamageEvent x:Atk(ForestPresentationStatus(x.Status)==WildBattleStatus.Burn?"sText_PkmnHurtByBurn":"sText_PkmnHurtByPoison",x.Side);break;
            case SourceActionBlockedEvent x:Atk(WildBattleStatus.IsSleeping(x.Status)?"sText_PkmnFastAsleep":x.Status==WildBattleStatus.Freeze?"sText_PkmnIsFrozen":"sText_PkmnIsParalyzed",x.Side);break;
            case SourceFlinchEvent {Applied:false} x:Atk("sText_PkmnFlinched",x.Side);break;
            case SourceInfatuationChangedEvent x when x.After is {} lover:
                if(x.Reason=="cute-charm")Source("sText_PkmnsXInfatuatedY",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(lover)),("B_DEF_ABILITY",DoubleAbility("ABILITY_CUTE_CHARM")),("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));
                else Def("sText_PkmnFellInLove",x.Side);break;
            case SourceInfatuationBlockedEvent x:Atk("sText_PkmnImmobilizedByLove",x.Side);break;
            case SourceAbilityActivatedEvent {Ability:"ABILITY_CUTE_CHARM" or "ABILITY_INTIMIDATE" or "ABILITY_STATIC" or "ABILITY_POISON_POINT" or "ABILITY_FLAME_BODY" or "ABILITY_EFFECT_SPORE" or "ABILITY_ROUGH_SKIN"}:break;
            case SourceAbilityDamageEvent {Ability:"ABILITY_ROUGH_SKIN"} x:
                Source("sText_PkmnHurtsWith",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Holder)),("B_DEF_ABILITY",DoubleAbility("ABILITY_ROUGH_SKIN")),("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Target)));break;
            case SourceAbilityPreventedEvent x when x.Move.Value=="pret:MOVE_NONE"&&DoubleIntimidateActive(b,x.Actor,x.Sequence):
                Source("sText_PreventedFromWorking",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Target)),("B_DEF_ABILITY",DoubleAbility(x.Ability)),("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Actor)),("B_BUFF1",DoubleAbility("ABILITY_INTIMIDATE")));break;
            case SourceHeldItemSurvivalEvent item:
                Source("sText_PkmnHungOnWithX",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(item.Target)),
                    ("B_LAST_ITEM",DoubleItem(item.Item)));break;
            case SourceFocusEnergyEvent x:Atk("gBattleText_GetPumped",x.Side);break;
            case SourceDisableChangedEvent x when x.Reason=="move"&&x.Move is {} move:Source("sText_PkmnMoveWasDisabled",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_BUFF1",MoveName(move)));break;
            case SourceDisableChangedEvent x when x.After==0:Atk("sText_PkmnMoveDisabledNoMore",x.Side);break;
            case SourceEncoreChangedEvent x when x.Reason=="move":Def("sText_PkmnGotEncore",x.Side);break;
            case SourceEncoreChangedEvent x when x.After==0:Atk("sText_PkmnEncoreEnded",x.Side);break;
            case SourceMtMoonVolatileEvent {Volatile:"follow-me",After:>0} x:Atk("sText_PkmnCenterAttention",x.Side);break;
            case SourceMtMoonVolatileEvent {Volatile:"helping-hand",After:>0} x:Source("sText_PkmnReadyToHelp",("B_ATK_NAME_WITH_PREFIX",DoublePrefixed(x.Side.Partner)),("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));break;
            case SourceFocusPunchPreparedEvent x:Atk("sText_PkmnTighteningFocus",x.Actor);break;
            case SourceFocusPunchLostEvent x:Atk("sText_PkmnLostFocus",x.Actor);break;
            case SourceRecoveredEvent x:Def("sText_PkmnRegainedHealth",x.Side);break;
            case SourcePotionRecoveredEvent x:Source("gText_PkmnHPRestoredByVar2",("STR_VAR_1",DoubleName(x.Side.Value)),("STR_VAR_2",x.HpRestored.ToString()));break;
            case SourceMedicineAppliedEvent x:pages.AddRange(DoubleMedicinePages(x));break;
            case SourceDrainEvent x:Def("sText_PkmnEnergyDrained",x.Target);break;
            case SourceCalledSpikesChangedEvent:break; // Mutation precedes the source attack/PP commands.
            case SourceMtMoonVolatileEvent {Volatile:"spikes",Reason:"move"}:Source("sText_SpikesScattered");break;
            case SourceMtMoonVolatileEvent {Volatile:"spikes",Reason:"entry"} x:Source("sText_PkmnHurtBySpikes",("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));break;
            case SourceMtMoonVolatileEvent {Volatile:"protect",Reason:"move"} x:Atk("sText_PkmnProtectedItself2",x.Side);break;
            case SourceMtMoonVolatileEvent {Volatile:"endure",Reason:"move"} x:Atk("sText_PkmnBracedItself",x.Side);break;
            case SourceMtMoonVolatileEvent {Volatile:"endure",Reason:"endured"} x:Def("sText_PkmnEnduredHit",x.Side);break;
            case SourceCalledPartySwitchedEvent x:
                if(x.Kind==WildBattleCalledSwitchKind.Forced){Source("sText_PkmnWasDraggedOut",("B_DEF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)));break;}
                if(x.Side.Side==BattleSide.Player)
                {
                    if(x.Kind==WildBattleCalledSwitchKind.Voluntary)Source("sText_PkmnThatsEnough",("B_BUFF1",_catalog.PokemonDisplayName(_session.State.Party[x.OutgoingIndex])));
                    Source("sText_GoPkmn2",("B_BUFF1",_catalog.PokemonDisplayName(_session.State.Party[x.IncomingIndex])));
                }
                else Source("sText_Trainer1SentOutPkmn2",("B_BUFF1",PresentedBattleSpeciesName(b.Frame.Party(BattleSide.Opponent).Members[x.IncomingIndex].Pokemon.SourceSpeciesId)));
                break;
            case SourcePlayerSwitchedEvent x:
                Source("sText_PkmnThatsEnough",("B_BUFF1",DoubleBodyName(x.Previous.Body)));
                Source("sText_GoPkmn2",("B_BUFF1",DoubleBodyName(x.Replacement.Body)));break;
            case SourcePlayerReplacedEvent x:Source("sText_GoPkmn2",("B_BUFF1",DoubleBodyName(x.Replacement.Body)));break;
            case SourceRewardAppliedEvent reward:
                string name=_catalog.PokemonDisplayName(_session.State.Party[reward.Recipient.Index]);
                Source("sText_PkmnGainedEXP",("B_BUFF1",name),("B_BUFF2",""),("B_BUFF3",reward.Reward.ExperienceGained.ToString()));
                foreach(var result in reward.Reward.Events)
                {
                    if(result is WildRewardLevelEvent level)
                    {
                        Source("sText_PkmnGrewToLv",("B_BUFF1",name),("B_BUFF2",level.After.ToString()));
                        _doubleRewardStats.Add(pages.Count,(level,false));pages.Add("");
                        _doubleRewardStats.Add(pages.Count,(level,true));pages.Add("");
                    }
                    else if(result is WildRewardMoveLearnedEvent learned)Source("sText_PkmnLearnedMove",("B_BUFF1",name),("B_BUFF2",MoveName(learned.Move)));
                }
                break;
            case SourceRewardLearningEvent learned when learned.Outcome is SourceRewardLearningOutcome.Added or SourceRewardLearningOutcome.Replaced:
                Source("sText_PkmnLearnedMove",("B_BUFF1",_catalog.PokemonDisplayName(_session.State.Party[learned.Recipient.Index])),("B_BUFF2",MoveName(learned.Candidate.Move)));break;
            case SourceRewardLearningEvent learned when learned.Outcome==SourceRewardLearningOutcome.Declined:
                Source("sText_DidNotLearnMove",("B_BUFF1",_catalog.PokemonDisplayName(_session.State.Party[learned.Recipient.Index])),("B_BUFF2",MoveName(learned.Candidate.Move)));break;
            // Typed bookkeeping events have no printed page; HP/faint/rewards still have their own boundaries.
            case SourcePpCommandEvent or SourceOrderEvent or SourceTurnReadyEvent or SourceCalledMoveTransitionEvent or SourceMoveTargetResolvedEvent or SourceCallerSelectionEvent or
                SourceHpChangedEvent or SourceEndedEvent or SourceReplacementRequiredEvent or SourceCalledPartySwitchRequiredEvent or
                SourceOpponentRewardCompletedEvent or SourceRewardLearningEvent or SourceDefenseCurledEvent or SourceRolloutChangedEvent or
                SourceStatusChangedEvent or SourceConfusionChangedEvent or SourceFlinchEvent or SourceDisableChangedEvent or SourceEncoreChangedEvent or
                SourceProtectAttemptEvent or SourceDamageAdjustedEvent or SourceEffectivenessEvent or SourceCalledPartyChangedEvent:
                break;
            default:
                // Unsupported presentation stays visible and requires an explicit ACK; never silently erase an event.
                pages.Add("This battle event is not\nsupported visually yet.");
                Write("unsupportedDoublePresentation",new{type=e.GetType().Name,e.Sequence});break;
        }
        return pages.ToImmutable();
    }
    private static SourceAbilityActivatedEvent? DoubleContactHolder(CampaignDoubleBattle battle,SourceStatusChangedEvent status)
    {
        string? expected=status.Reason switch {"static"=>"ABILITY_STATIC","poison-point"=>"ABILITY_POISON_POINT","flame-body"=>"ABILITY_FLAME_BODY","effect-spore"=>"ABILITY_EFFECT_SPORE",_=>null};
        if(expected is null)return null;
        return battle.Events.LastOrDefault(e=>e.Sequence<status.Sequence&&e is SourceAbilityActivatedEvent or SourceMoveUsedEvent) is SourceAbilityActivatedEvent a&&a.Ability==expected?a:null;
    }
    private static bool DoubleIntimidateActive(CampaignDoubleBattle battle,BattlerSlot actor,long sequence)
    {
        var previous=battle.Events.LastOrDefault(e=>e.Sequence<sequence&&e is SourceAbilityActivatedEvent or SourceMoveUsedEvent);
        return previous is SourceAbilityActivatedEvent {Ability:"ABILITY_INTIMIDATE"} ability&&ability.Side==actor;
    }

    private void ProcessDoubleBattle(double delta)
    {
        _doubleElapsed+=delta;
        var b=_session.State.DoubleBattle!;
        if(_busy||_doubleNotice is not null||_doublePages.Length!=0||b.LearningPending||b.ReplacementSlot is not null||b.SelectingSlot is not null)return;
        if(b.Stage==CampaignDoubleBattleStage.Combat&&_doubleElapsed>=(DoubleEventChangesHp(b.CurrentEvent) ? .4 : .06))Apply(new("ack"));
    }
    private void DoubleSubmit(CampaignCommand command)
    {
        bool accepted = Apply(command);
        if(!accepted)_doubleNotice=_status=="preset_double_item_already_used"?"The item was already used.\nChoose this POKéMON's action.":command.Kind=="double_switch"?"That POKéMON can't\nswitch in now.":"That choice can't be\nused now.";
    }
    private void DoubleAcknowledge()
    {
        if(SourceAnimationBlocking)return;
        if(_doubleElapsed<(DoubleEventChangesHp(_session.State.DoubleBattle?.CurrentEvent) ? .4 : .12))return;
        if(_doubleOpening&&_doublePage>0&&
            (_doubleElapsed<1||_doubleSecondCry is not null||_sourceAudio?.Snapshot().Cry?.Playing==true))return;
        if(_doublePage+1<_doublePages.Length){_doublePage++;_doubleElapsed=0;return;}
        if(_doubleMenu=="learn-intro"){_doubleMenu="learn-choice";_doubleCursor=0;return;}
        if(_doubleMenu=="struggle"&&_session.State.DoubleBattle!.SelectingSlot is {} actor)
        {DoubleSubmit(new("double_move",PartyIndex:actor,MoveSlot:WildBattleRules.StruggleSlot,Choice:actor^1));return;}
        AcknowledgeDoubleAttackText();
    }
    private int[] DoubleTargets(int actor,int moveSlot)
    {
        var b=_session.State.DoubleBattle!;
        if(moveSlot==WildBattleRules.StruggleSlot)return Enumerable.Range(0,4).Where(i=>(i&1)!=0&&b.Frame.CanAct(new(i))).ToArray();
        var move=_catalog.PresetDoublePokemon!.Moves[b.Frame.Get(new(actor)).Body!.Pokemon.Moves[moveSlot].Move];
        if(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED")return [actor];
        if(move.Effect=="EFFECT_HELPING_HAND")return b.Frame.CanAct(new(actor^2))?[actor^2]:[];
        return Enumerable.Range(0,4).Where(i=>i!=actor&&b.Frame.CanAct(new(i))&&
            (move.Target=="MOVE_TARGET_SELECTED"||(i&1)!=0)).ToArray();
    }
    private void ChooseDoubleMove(int actor,int moveSlot)
    {
        _doubleChosenMove=moveSlot;var targets=DoubleTargets(actor,moveSlot);
        if(targets.Length==0){_doubleNotice="There is no available\ntarget.";return;}
        var move=moveSlot==WildBattleRules.StruggleSlot?null:_catalog.PresetDoublePokemon!.Moves[_session.State.DoubleBattle!.Frame.Get(new(actor)).Body!.Pokemon.Moves[moveSlot].Move];
        if(targets.Length==1||move?.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" or "MOVE_TARGET_RANDOM" or "MOVE_TARGET_BOTH" or "MOVE_TARGET_FOES_AND_ALLY" or "MOVE_TARGET_OPPONENTS_FIELD")
        {DoubleSubmit(new("double_move",PartyIndex:actor,MoveSlot:moveSlot,Choice:targets[0]));return;}
        _doubleMenu="targets";_doubleCursor=Array.FindIndex(targets,i=>(i&1)!=0);if(_doubleCursor<0)_doubleCursor=0;
    }
    private bool DoubleBattleInput(InputEventKey key)
    {
        if(!DoubleBattleVisible||!key.Pressed)return false;
        if(key.Keycode is Key.Escape or Key.F5 or Key.F9 or Key.F12)return false;
        if(_doubleNotice is not null){if(key.Keycode is Key.Enter or Key.Space or Key.Backspace or Key.B)_doubleNotice=null;return true;}
        var b=_session.State.DoubleBattle!;
        if(BattlePartyInspectionInput(key))return true;
        if(b.CurrentEvent is not null||b.Stage==CampaignDoubleBattleStage.Finished||_doubleMenu is "learn-intro" or "struggle")
        {if(key.Keycode is Key.Enter or Key.Space or Key.Backspace)DoubleAcknowledge();return true;}
        bool back=key.Keycode is Key.Backspace or Key.B;
        bool confirm=key.Keycode is Key.Enter or Key.Space;
        int direction=key.Keycode is Key.Up or Key.Left?-1:key.Keycode is Key.Down or Key.Right?1:0;
        if(b.LearningPending)
        {
            int count=b.Frame.Party(BattleSide.Player).Members[b.Frame.Rewards!.Pending!.Recipient.Index].Pokemon.Moves.Length;
            if(_doubleMenu=="learn-choice")
            {if(direction!=0)_doubleCursor=1-_doubleCursor;if(back||confirm&&_doubleCursor==1){DoubleSubmit(new("double_learn",MoveSlot:-1));return true;}if(confirm){_doubleMenu="learn-moves";_doubleCursor=0;}return true;}
            var pending = b.Frame.Rewards!.Pending!;
            var recipient = DoubleLearningPokemon(b);
            if (!MoveSelectionCurrent("double:" + b.Id, recipient, pending.Moves[0].Move))
            { _doubleCursor = 0; _doubleNotice = SummaryChangedNotice; return true; }
            if(back){_doubleMenu="learn-choice";_doubleCursor=0;return true;}
            if(direction!=0)_doubleCursor=(_doubleCursor+direction+count+1)%(count+1);
            if(confirm){if(_doubleCursor==count){_doubleMenu="learn-choice";_doubleCursor=0;}else DoubleSubmit(new("double_learn",MoveSlot:_doubleCursor));}return true;
        }
        int actor=b.ReplacementSlot??b.SelectingSlot??-1;
        if(actor<0)return true;
        if(BattleMoveOrderInput(key, _doubleMenu, ref _doubleCursor, out var swapNotice))
        { _doubleNotice = swapNotice; return true; }
        if(DoubleMedicineInput(key,actor))return true;
        if(_doubleMenu=="party")
        {
            if(back){if(b.ReplacementSlot is null){_doubleMenu="actions";_doubleCursor=2;}return true;}
            int count=_session.State.Party.Length;NativePartyMove(key.Keycode,ref _doubleCursor,count,cancel:b.ReplacementSlot is null,layout:NativePartyLayout.Double);
            if(confirm){if(_doubleCursor==count){if(b.ReplacementSlot is null){_doubleMenu="actions";_doubleCursor=2;}}else OpenBattlePartyActions(_doubleCursor,true);}return true;
        }
        if(_doubleMenu=="targets")
        {
            var targets=DoubleTargets(actor,_doubleChosenMove);
            if(back){_doubleMenu="moves";_doubleCursor=Math.Max(0,_doubleChosenMove);return true;}
            if(direction!=0)_doubleCursor=(_doubleCursor+direction+targets.Length)%targets.Length;
            if(confirm)DoubleSubmit(new("double_move",PartyIndex:actor,MoveSlot:_doubleChosenMove,Choice:targets[_doubleCursor]));return true;
        }
        if(back)
        {
            if(_doubleMenu=="moves"){_doubleMenu="actions";_doubleCursor=0;}
            else if(!b.PlayerSelections.IsEmpty)DoubleSubmit(new("double_cancel"));
            return true;
        }
        int moveCount=b.Frame.Get(new(actor)).Body!.Pokemon.Moves.Length;
        int countGrid=_doubleMenu=="actions"?4:moveCount;
        if(key.Keycode is Key.Left or Key.Right)_doubleCursor^=1;
        if(key.Keycode is Key.Up or Key.Down)_doubleCursor^=2;
        _doubleCursor=Math.Min(_doubleCursor,countGrid-1);
        if(!confirm)return true;
        if(_doubleMenu=="moves"){ChooseDoubleMove(actor,_doubleCursor);return true;}
        switch(_doubleCursor)
        {
            case 0:
                if(SourceFourActionTurns.RequiresStruggle(_catalog.PresetDoublePokemon!,b.Frame,new(actor)))
                {_doubleMenu="struggle";_doublePages=DoubleSource("sText_PkmnHasNoMovesLeft",("B_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(new(actor))));_doublePage=0;_doubleElapsed=0;}
                else {_doubleMenu="moves";_doubleCursor=0;}
                break;
            case 1:OpenDoubleBag();break;
            case 2:_doubleMenu="party";_doubleCursor=0;break;
            case 3:_doubleNotice="No! There's no running\nfrom a TRAINER battle!";break;
        }
        return true;
    }
    private void DoubleLines(string text)
    {int y=122;foreach(var line in text.Split('\n')){SourceText(_battleWhite,new(10,y),line);y+=16;}}
    private static bool DoubleBattlerFaintShown(CampaignDoubleBattle battle,PokemonInstance mon,int slot)=>
        mon.CurrentHp==0&&!(battle.CurrentEvent is SourceHpChangedEvent {HpBefore:>0} hit&&hit.Target.Value==slot);
    private void DrawDoubleBattle()
    {
        var b=_session.State.DoubleBattle!;
        if(DrawBattlePartyInspection())return;
        if(DrawDoubleMedicineOverlay(b))return;
        _battleSkin.DrawBackground(this);DrawTexture(CurrentWildBattleTerrain,Vector2.Zero);
        if(SourceAnimationPresented)DrawTexture(_sourceMoveField!,Vector2.Zero);
        bool entry=_doubleOpening||b.CurrentEvent is SourceStartedEvent;
        bool portrait=entry&&_doublePage==0||b.Stage==CampaignDoubleBattleStage.Finished&&b.Frame.Outcome==WildBattleOutcome.Won&&(b.FinishPageIndex>0||b.Frame.TrainerTowerEncounter is not null);
        if(portrait)DrawTexture(DoubleTrainerPortrait,VisibleTrainerPortraitPosition);
        if(entry&&_doublePage==0)DrawTexture(_forestTrainerPortraits[_session.State.PlayerGender==0?"red-back":"leaf-back"],new(48,48));
        var healthForeground=new List<Action>();
        foreach(int i in new[]{3,1,0,2})
        {
            var body=DoubleDisplayedBody(i);var mon=body.Pokemon;
            if(entry&&(_doublePage==0||_doublePage==1&&(i&1)==0)||portrait&&(i&1)!=0)continue;
            if(DoubleBattlerFaintShown(b,mon,i))continue;
            var asset=DoubleBattleArt(body,i);
            if(!SourceAnimationPresented)
            {
                if(body.CalledVolatile?.SubstituteHp>0&&!SourceSubstituteRevealed(true,i))DrawDoubleSubstitute(i);
                else DrawTexture(asset.Texture,asset.Position+DoubleSpriteOffsets[i]);
            }
            healthForeground.Add(()=>DrawDoubleHealth(i,body));
        }
        foreach(var drawHealth in healthForeground)drawHealth();
        bool choosing=b.SelectingSlot is not null&&_doubleNotice is null&&_doublePages.IsEmpty;
        if(choosing)_uiCorrections.ClearBattleMenu(this);else _battleSkin.DrawBattleMessage(this);
        if(_doubleMenu is "party" or "learn-moves"){DrawDoublePartyOrMoves(b);return;}
        if(_doubleNotice is not null){DoubleLines(_doubleNotice);return;}
        if(_sourceResultContinuation is not null&&!_sourceResultCompleted)return;
        if(_doublePages.Length>0)
        {
            if(_doubleRewardStats.TryGetValue(_doublePage,out var stats)){DrawDoubleRewardStats(stats.Level,stats.Totals);return;}
            DoubleLines(_doublePages[_doublePage]);DrawTextCursor(_battleWhite,LabBattleUiSkin.BattleMessageText,_doublePages[_doublePage].Split('\n'),16,_doubleElapsed,true);
            if(_doubleMenu=="learn-choice")
            { _battleSkin.DrawStandardFrame(this,new(184,64,56,48));SourceText(_battleMenuFont,new(200,72),"YES");SourceText(_battleMenuFont,new(200,88),"NO");SourceText(_battleMenuFont,new(188,72+16*_doubleCursor),">"); }
            return;
        }
        if(b.SelectingSlot is not {} actor)return;
        if(_doubleMenu=="actions")
        {
            _battleSkin.DrawActionMenu(this);DoubleLines(_battleSelectionText.ActionPrompt(DoubleName(actor),_session.State.PlayerName));
            var labels=_battleSelectionText.ActionLabels;
            for(int i=0;i<4;i++)SourceText(_battleMenuFont,LabBattleUiSkin.ActionText(i),labels[i]);
            _uiCorrections.DrawSelectionCursor(this,false,_doubleCursor);
        }
        else if(_doubleMenu=="targets")
        {
            _battleSkin.DrawBattleMessage(this);var targets=DoubleTargets(actor,_doubleChosenMove);int target=targets[_doubleCursor];
            DoubleLines("Choose a target:\n"+(target==actor?"SELF ":(target&1)==0?"ALLY ":"FOE ")+DoubleName(target));
            var center=DoubleHealthCenters[target];DrawRect(new(center+new Vector2(-32,-16),new(104,24)),Color.Color8(248,64,64),false,1);
        }
        else
        {
            _battleSkin.DrawMoveMenu(this);var moves=b.Frame.Get(new(actor)).Body!.Pokemon.Moves;
            for(int i=0;i<4;i++)SourceText(_battleMoveFont,LabBattleUiSkin.MoveText(i),i<moves.Length?MoveName(moves[i].Move):"-");
            _uiCorrections.DrawSelectionCursor(this,true,_doubleCursor);
            if (DrawBattleMoveOrder(_doubleCursor)) return;
            var move=moves[_doubleCursor];int pp=SourceUiCorrections.PpPaletteState(move.CurrentPp,move.MaximumPp);
            SourceText(_battlePpSmallFont[pp],new(168,122),_battleSelectionText.PpLabel);SourceText(_battlePpFont[pp],new(202,122),$"{move.CurrentPp,2}/{move.MaximumPp,2}");
            SourceText(_battleMoveFont,new(168,138),_battleSelectionText.TypeLabel+_catalog.DisplayNames.TypeName(_catalog.PresetDoublePokemon!.Moves[move.Move].Type));
        }
    }
    private void DrawDoubleHealth(int slot,WildBattleCombatant body)
    {
        var center=DoubleHealthCenters[slot];bool player=(slot&1)==0;var mon=body.Pokemon;
        DrawTexture(_doubleTextures[player?"healthbox-player":"healthbox-opponent"],center+new Vector2(-32,-16));
        int hp=DoubleEventHp(slot,body);
        var medicine=_session.State.DoubleBattle!.CurrentEvent as SourceMedicineAppliedEvent;
        bool medicineTarget=medicine is not null&&_session.State.DoubleBattle.DisplayedMembers[slot]==new PartyMemberKey(medicine.TargetSide??BattleSide.Player,medicine.PartyIndex);
        var barOrigin=center+new Vector2(player?0:-8,0);
        uint status=ForestPresentationStatus(medicineTarget?DoubleMedicineStatus(medicine!):DoubleEventStatus(slot,body));
        if(status==0)DrawTexture(_doubleTextures["hp-label"],barOrigin);
        else
        {
            DrawTexture(_forestStatusTextures["battle-opponent-"+ForestStatusId(status)],center+new Vector2(player?-16:-24,0));
            DrawTexture(_forestStatusTextures["battle-opponent-hp-border"],barOrigin+new Vector2(8,0));
        }
        int pixels=hp==0?0:Math.Max(1,hp*48/mon.Stats.MaxHp);string bar=pixels>24?"hp-green":pixels>9?"hp-yellow":"hp-red";
        for(int tile=0;tile<6;tile++)DrawTextureRectRegion(_doubleTextures[bar],new(barOrigin+new Vector2(16+8*tile,0),new(8,8)),new(Math.Clamp(pixels-tile*8,0,8)*8,0,8,8));
        var nameOrigin=center+new Vector2(player?-16:-24,-13);string name=DoubleName(slot);SourceText(_healthFont,nameOrigin,name);
        if(mon.Gender!=PokemonGender.Genderless)
        {
            float width=_healthFont.Font.GetStringSize(_healthFont.PrepareText(name),fontSize:_healthFont.Size).X;
            SourceText(mon.Gender==PokemonGender.Female?_healthFemale:_healthMale,nameOrigin+new Vector2(width,0),mon.Gender==PokemonGender.Female?"♀":"♂");
        }
        SourceText(_healthFont,center+new Vector2((player?40:32)+5*(3-mon.Level.ToString().Length),-13),_healthFont.LevelPrefix+mon.Level);
    }
    private void DrawDoubleRewardStats(WildRewardLevelEvent level,bool totals)
    {
        var reward=(SourceRewardAppliedEvent)_session.State.DoubleBattle!.CurrentEvent!;
        DoubleLines(string.Join('\n',DoubleSource("sText_PkmnGrewToLv",("B_BUFF1",_catalog.PokemonDisplayName(_session.State.Party[reward.Recipient.Index])),("B_BUFF2",level.After.ToString()))));
        _battleSkin.DrawStandardFrame(this,new(144,56,96,104));
        var rows=new[]{("gText_LevelUp_MaxHP",level.StatsBefore.MaxHp,level.StatsAfter.MaxHp),("gText_LevelUp_Attack",level.StatsBefore.Attack,level.StatsAfter.Attack),
            ("gText_LevelUp_Defense",level.StatsBefore.Defense,level.StatsAfter.Defense),("gText_LevelUp_SpAtk",level.StatsBefore.SpecialAttack,level.StatsAfter.SpecialAttack),
            ("gText_LevelUp_SpDef",level.StatsBefore.SpecialDefense,level.StatsAfter.SpecialDefense),("gText_LevelUp_Speed",level.StatsBefore.Speed,level.StatsAfter.Speed)};
        for(int i=0;i<rows.Length;i++)
        {
            var (symbol,before,after)=rows[i];DrawStatLabel(new(152,64+15*i),_battleText[symbol]);
            if(totals)SourceText(_battleMenuFont,new(208+6*(4-after.ToString().Length),64+15*i),after.ToString());
            else {int difference=after-before;DrawStatLabel(new(208,64+15*i),_battleText[difference>=0?"gText_LevelUp_Plus":"gText_LevelUp_Minus"]);SourceText(_battleMenuFont,new(208+(Math.Abs(difference)<10?12:6),64+15*i)," "+Math.Abs(difference));}
        }
    }
    private PartyPokemon DoubleLearningPokemon(CampaignDoubleBattle b)
    {
        int index = b.Frame.Rewards!.Pending!.Recipient.Index;
        var member = b.Frame.Party(BattleSide.Player).Members[index];
        return PartyPresentationAssets.Displayed(_session.State.Party[index], member.Pokemon, member.Status);
    }
    private void DrawDoublePartyOrMoves(CampaignDoubleBattle b)
    {
        _battleSkin.DrawStandardFrame(this,new(0,0,240,112));
        if(_doubleMenu=="learn-moves")
        {
            DrawMoveSelectionSummary("double:" + b.Id, DoubleLearningPokemon(b), b.Frame.Rewards!.Pending!.Moves[0].Move, _doubleCursor);
            if (_doubleNotice is not null) ItemMessageBox(_doubleNotice);
            return;
        }
        DrawNativeParty(_session.State.Party,_doubleCursor,"Choose a POKéMON.",
            cancel:b.ReplacementSlot is null,layout:NativePartyLayout.Double,
            displayed:i=>PartyPresentationAssets.Displayed(_session.State.Party[i],b.Frame.Party(BattleSide.Player).Members[i].Pokemon,
                b.Frame.Party(BattleSide.Player).Members[i].Status));
        if(_doubleNotice is not null)DrawNativePartyMessage(_doubleNotice);
    }
    private bool NavigateDoublePointer(Vector2 point,int direction)
    {
        var b=_session.State.DoubleBattle!;if(_doubleNotice is not null)return false;
        if(_battlePartySelection is not null)return NavigateBattlePartyInspection(point,direction);
        if(_doubleMenu=="bag-actions")return PointerRows(point,direction,ref _doubleCursor,3,168,106,72);
        if(_doubleMenu=="bag")return PointerList(point,direction,ref _doubleCursor,DoubleBagStacks.Length);
        if(_doubleMenu=="medicine-target")return PointerNativeParty(point,direction,ref _doubleCursor,_session.State.Party.Length,cancel:true,layout:NativePartyLayout.Double);
        if(_doubleMenu=="medicine-move")return PointerRows(point,direction,ref _doubleCursor,_session.State.Party[_doubleMedicinePartyIndex].Pokemon.Moves.Length+1,8,10,224,16);
        if(_doubleMenu=="party")return PointerNativeParty(point,direction,ref _doubleCursor,_session.State.Party.Length,cancel:b.ReplacementSlot is null,layout:NativePartyLayout.Double);
        if(_doubleMenu=="learn-moves")return NavigateMoveSelectionSummary(point,direction,ref _doubleCursor,DoubleLearningPokemon(b).Pokemon.Moves.Length);
        if(_doubleMenu=="learn-choice")return PointerYesNo(point,direction,ref _doubleCursor);
        if(b.SelectingSlot is not {} actor)return false;
        if(_doubleMenu=="targets")
        {
            var targets=DoubleTargets(actor,_doubleChosenMove);
            for(int i=0;i<targets.Length;i++)if(new Rect2(DoubleHealthCenters[targets[i]]+new Vector2(-32,-16),new(104,24)).HasPoint(point)){if(direction!=0)return MouseNavigationKey(direction);_doubleCursor=i;return true;}
            return false;
        }
        return PointerGrid(point,direction,ref _doubleCursor,_doubleMenu=="moves",_doubleMenu=="actions"?4:b.Frame.Get(new(actor)).Body!.Pokemon.Moves.Length);
    }
    private bool MouseDouble(Vector2 point)
    {
        var b=_session.State.DoubleBattle!;
        if(_doubleNotice is not null)return MouseMessage.HasPoint(point)&&MouseKey();
        if(_battlePartySelection is not null)return MouseBattlePartyInspection(point);
        if(_doubleMenu=="bag-actions")return MouseSelect(point,ref _doubleCursor,3,168,106,72);
        if(_doubleMenu=="bag")return (!_doubleBerries&&MouseBagArrows(point,_doubleBagPocket))||MouseList(point,ref _doubleCursor,DoubleBagStacks.Length);
        if(_doubleMenu=="medicine-target")return MouseNativeParty(point,ref _doubleCursor,_session.State.Party.Length,cancel:true,layout:NativePartyLayout.Double);
        if(_doubleMenu=="medicine-move")return MouseSelect(point,ref _doubleCursor,_session.State.Party[_doubleMedicinePartyIndex].Pokemon.Moves.Length+1,8,10,224,16);
        if(_doubleMenu=="party")return MouseNativeParty(point,ref _doubleCursor,_session.State.Party.Length,cancel:b.ReplacementSlot is null,layout:NativePartyLayout.Double);
        if(_doubleMenu=="learn-moves")return MouseMoveSelectionSummary(point,ref _doubleCursor,DoubleLearningPokemon(b).Pokemon.Moves.Length);
        if(_doubleMenu=="learn-choice")return MouseSelect(point,ref _doubleCursor,2,184,72,56);
        if(_doublePages.Length>0)return MouseMessage.HasPoint(point)&&MouseKey();
        if(b.SelectingSlot is not {} actor)return false;
        if(_doubleMenu=="targets")return NavigateDoublePointer(point,0)&&MouseKey();
        return MouseGrid(point,ref _doubleCursor,_doubleMenu=="moves",_doubleMenu=="actions"?4:b.Frame.Get(new(actor)).Body!.Pokemon.Moves.Length);
    }
    private void ProcessDoubleBattleAudio()
    {
        if(_session.State.DoubleBattle is not {} b||!DoubleBattleVisible){_doubleSecondCry=null;return;}
        if(_doubleSecondCry is {} second&&_sourceAudio?.Snapshot().Cry?.Playing!=true)
        {_doubleSecondCry=null;SourceCry(b.DisplayedSlots[second].Pokemon);}
        string key=$"{b.Id}:double:{b.EventIndex}:{b.Frame.EventSequence}:{_doublePage}";
        if(key==_audioPresentedEvent)return;_audioPresentedEvent=key;
        if(_doubleOpening||b.CurrentEvent is SourceStartedEvent)
        {
            // Source pairs cry in sequence; the second begins after the first completes.
            if(_doublePage==1){SourceCry(b.DisplayedSlots[1].Pokemon);_doubleSecondCry=3;}
            if(_doublePage==2){SourceCry(b.DisplayedSlots[0].Pokemon);_doubleSecondCry=2;}
        }
        if(b.CurrentEvent is SourceCalledPartySwitchedEvent sw&&DoubleSwitchSending(sw))SourceCry((sw.Replacement?.Body??throw new InvalidDataException("Doubles switch snapshot missing.")).Pokemon);
        if(b.CurrentEvent is SourcePlayerSwitchedEvent x&&_doublePage==1)SourceCry(x.Replacement.Body.Pokemon);
        if(b.CurrentEvent is SourcePlayerReplacedEvent y&&_doublePage==0)SourceCry(y.Replacement.Body.Pokemon);
        if(b.CurrentEvent is SourceMedicineAppliedEvent&&_doublePage==0)SourceSound("SE_USE_ITEM");
    }
}
