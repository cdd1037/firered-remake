using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Read-only projection of a committed source invocation, consumed after its attack text.
/// It adds no Domain event, command, RNG draw, saved field or authoritative acknowledgement.</summary>
public sealed record CampaignMoveAnimationDispatch(string BattleId, long Sequence, long AttackSequence,
    long PpSequence, long ResultSequence, int SourceMoveId, BattlerSlot Actor, BattlerSlot Target,
    bool HitsSubstitute) { public byte? MoveTurn {get;init;} public bool AcknowledgesCurrentReceipt=>MoveTurn is null; }
public sealed record CampaignMoveAnimationDecision(CampaignMoveAnimationDispatch? Dispatch, string Reason);

public static partial class CampaignMoveAnimationPresentation
{
    // Shared by single/double projections; zero means no implemented source renderer.
    internal static (WildBattleStat Stat,int Delta)? ReductionAnimation(int move) => move switch
    {
        39 => (WildBattleStat.Defense,-1),
        103 => (WildBattleStat.Defense,-2),
        148 => (WildBattleStat.Accuracy,-1),
        178 => (WildBattleStat.Speed,-2),
        204 => (WildBattleStat.Attack,-2),
        319 => (WildBattleStat.SpecialDefense,-2),
        _ => null
    };
    private static bool IsRecoveryAnimation(int move) => move is 105 or 235 or 303;

    internal static int SupportedSourceMoveId(string move)=>move switch
    {
        "pret:MOVE_DOUBLE_SLAP" => 3,
        "pret:MOVE_COMET_PUNCH" => 4,
        "pret:MOVE_TRIPLE_KICK" => 167,
        "pret:MOVE_GUST" => 16,
        "pret:MOVE_WING_ATTACK" => 17,
        "pret:MOVE_STEEL_WING" => 211,
        "pret:MOVE_ROCK_THROW" => 88,
        "pret:MOVE_ROCK_SLIDE" => 157,
        "pret:MOVE_SPORE" => 147,
        "pret:MOVE_COTTON_SPORE" => 178,
        "pret:MOVE_SYNTHESIS" => 235,
        "pret:MOVE_DREAM_EATER" => 138,
        "pret:MOVE_SWAGGER" => 207,
        "pret:MOVE_TICKLE" => 321,
        "pret:MOVE_BARRIER" => 112,
        "pret:MOVE_LIGHT_SCREEN" => 113,
        "pret:MOVE_REFLECT" => 115,
        "pret:MOVE_MIRROR_COAT" => 243,
        "pret:MOVE_MAGIC_COAT" => 277,
        "pret:MOVE_CONFUSION" => 93,
        "pret:MOVE_PSYCHIC" => 94,
        "pret:MOVE_DEFENSE_CURL" => 111,
        "pret:MOVE_PSYBEAM" => 60,
        "pret:MOVE_HYPNOSIS" => 95,
        "pret:MOVE_PSYWAVE" => 149,
        "pret:MOVE_SLACK_OFF" => 303,
        "pret:MOVE_BULK_UP" => 339,
        "pret:MOVE_POUND" => 1,
        "pret:MOVE_KARATE_CHOP" => 2,
        "pret:MOVE_FIRE_PUNCH" => 7,
        "pret:MOVE_ICE_PUNCH" => 8,
        "pret:MOVE_THUNDER_PUNCH" => 9,
        "pret:MOVE_SCRATCH" => 10,
        "pret:MOVE_VICE_GRIP" => 11,
        "pret:MOVE_CUT" => 15,
        "pret:MOVE_BIND" => 20,
        "pret:MOVE_SLAM" => 21,
        "pret:MOVE_VINE_WHIP" => 22,
        "pret:MOVE_STOMP" => 23,
        "pret:MOVE_JUMP_KICK" => 26,
        "pret:MOVE_ROLLING_KICK" => 27,
        "pret:MOVE_HEADBUTT" => 29,
        "pret:MOVE_TACKLE" => 33,
        "pret:MOVE_BODY_SLAM" => 34,
        "pret:MOVE_WRAP" => 35,
        "pret:MOVE_TAKE_DOWN" => 36,
        "pret:MOVE_DOUBLE_EDGE" => 38,
        "pret:MOVE_TAIL_WHIP" => 39,
        "pret:MOVE_BITE" => 44,
        "pret:MOVE_SUPERSONIC" => 48,
        "pret:MOVE_ACID" => 51,
        "pret:MOVE_EMBER" => 52,
        "pret:MOVE_FLAMETHROWER" => 53,
        "pret:MOVE_WATER_GUN" => 55,
        "pret:MOVE_HYDRO_PUMP" => 56,
        "pret:MOVE_ICE_BEAM" => 58,
        "pret:MOVE_BUBBLE_BEAM" => 61,
        "pret:MOVE_PECK" => 64,
        "pret:MOVE_SUBMISSION" => 66,
        "pret:MOVE_LOW_KICK" => 67,
        "pret:MOVE_COUNTER" => 68,
        "pret:MOVE_STRENGTH" => 70,
        "pret:MOVE_ABSORB" => 71,
        "pret:MOVE_MEGA_DRAIN" => 72,
        "pret:MOVE_GROWTH" => 74,
        "pret:MOVE_RAZOR_LEAF" => 75,
        "pret:MOVE_POISON_POWDER" => 77,
        "pret:MOVE_STUN_SPORE" => 78,
        "pret:MOVE_SLEEP_POWDER" => 79,
        "pret:MOVE_DRAGON_RAGE" => 82,
        "pret:MOVE_FIRE_SPIN" => 83,
        "pret:MOVE_THUNDER_SHOCK" => 84,
        "pret:MOVE_THUNDERBOLT" => 85,
        "pret:MOVE_THUNDER_WAVE" => 86,
        "pret:MOVE_TOXIC" => 92,
        "pret:MOVE_AGILITY" => 97,
        "pret:MOVE_QUICK_ATTACK" => 98,
        "pret:MOVE_SCREECH" => 103,
        "pret:MOVE_RECOVER" => 105,
        "pret:MOVE_HARDEN" => 106,
        "pret:MOVE_MINIMIZE" => 107,
        "pret:MOVE_WITHDRAW" => 110,
        "pret:MOVE_FOCUS_ENERGY" => 116,
        "pret:MOVE_SELF_DESTRUCT" => 120,
        "pret:MOVE_EGG_BOMB" => 121,
        "pret:MOVE_FIRE_BLAST" => 126,
        "pret:MOVE_CLAMP" => 128,
        "pret:MOVE_SWIFT" => 129,
        "pret:MOVE_HI_JUMP_KICK" => 136,
        "pret:MOVE_LEECH_LIFE" => 141,
        "pret:MOVE_BUBBLE" => 145,
        "pret:MOVE_FLASH" => 148,
        "pret:MOVE_SPLASH" => 150,
        "pret:MOVE_CRABHAMMER" => 152,
        "pret:MOVE_EXPLOSION" => 153,
        "pret:MOVE_SLASH" => 163,
        "pret:MOVE_THIEF" => 168,
        "pret:MOVE_FLAME_WHEEL" => 172,
        "pret:MOVE_POWDER_SNOW" => 181,
        "pret:MOVE_OCTAZOOKA" => 190,
        "pret:MOVE_ZAP_CANNON" => 192,
        "pret:MOVE_OUTRAGE" => 200,
        "pret:MOVE_GIGA_DRAIN" => 202,
        "pret:MOVE_ENDURE" => 203,
        "pret:MOVE_CHARM" => 204,
        "pret:MOVE_FALSE_SWIPE" => 206,
        "pret:MOVE_DYNAMIC_PUNCH" => 223,
        "pret:MOVE_DRAGON_BREATH" => 225,
        "pret:MOVE_IRON_TAIL" => 231,
        "pret:MOVE_VITAL_THROW" => 233,
        "pret:MOVE_CRUNCH" => 242,
        "pret:MOVE_WHIRLPOOL" => 250,
        "pret:MOVE_SNATCH" => 289,
        "pret:MOVE_MUD_SPORT" => 300,
        "pret:MOVE_METAL_SOUND" => 319,
        "pret:MOVE_SIGNAL_BEAM" => 324,
        "pret:MOVE_SAND_TOMB" => 328,
        "pret:MOVE_AERIAL_ACE" => 332,
        "pret:MOVE_IRON_DEFENSE" => 334,
        "pret:MOVE_MUD_SHOT" => 341,
        "pret:MOVE_POISON_TAIL" => 342,
        "pret:MOVE_COVET" => 343,
        _=>0
    };

    public static CampaignMoveAnimationDecision AtAttackText(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene)
    {
        if (!sourceRules) return new(null, "historical-rules");
        if (scene != SourceBattleScene.On) return new(null, "animations-disabled");
        if (battle.Phase != CampaignBattlePhase.Events || battle.EventIndex < 0 || battle.EventIndex >= battle.Events.Length)
            return new(null, "not-attack-text");
        // Accepted basic hit/slice moves print through SpendInvocationPp. The paired AttackString is silent;
        // MoveUsed is the actual visible page. Callers such as Metronome have a different current move.
        if (battle.Events[battle.EventIndex] is not WildBattleMoveUsedEvent used) return new(null, "not-attack-text");
        int move = SupportedSourceMoveId(used.Move.Value);
        if (move == 0) return new(null, "script-outside-slice");
        if(move is 3 or 4 or 167)return new(null,"source-per-hit-animation");
        if (battle.EventIndex == 0 || battle.Events[battle.EventIndex - 1] is not WildBattleAttackStringEvent attack ||
            attack.Actor != used.Actor || attack.CurrentMove != used.Move || attack.Turn != used.Turn)
            return new(null, "unmatched-source-attack-string");
        int end = battle.EventIndex + 1;
        while (end < battle.Events.Length && battle.Events[end].Turn == used.Turn &&
            battle.Events[end] is not (WildBattleAttackStringEvent or WildBattleMoveUsedEvent or WildBattleOrderEvent or WildBattleTurnReadyEvent)) end++;
        var action = battle.Events.Skip(battle.EventIndex + 1).Take(end - battle.EventIndex - 1).ToArray();
        var pp = action.OfType<WildBattlePpCommandEvent>().ToArray();
        if (pp.Length != 1 || pp[0].Actor != used.Actor || pp[0].Target != used.Target || pp[0].CurrentMove != used.Move)
            return new(null, "unmatched-source-pp-command");
        if (move is 120 or 153)
        {
            // BattleScript_EffectExplosion animates once immediately after setatkhptozero,
            // before every target pass, even when every pass later misses/is immune.
            var committed=action.OfType<WildBattleSelfDestructEvent>().ToArray();
            if(pp[0].ChosenMove!=used.Move||pp[0].PpSlot!=used.Slot||pp[0].Sequence<=used.Sequence||
                pp[0].Before!=used.PpBefore||pp[0].After!=used.PpAfter||committed.Length!=1||
                committed[0].Actor!=used.Actor||committed[0].Move!=used.Move||committed[0].HpBefore<=0||committed[0].Sequence<=pp[0].Sequence)
                return new(null,"unmatched-source-self-destruct");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,committed[0].Sequence,move,
                new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(move is 207 or 321 or 339)
        {
            var result=CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle,sourceRules);
            if(result is null)return new(null,action.Any(e=>e is WildBattleMoveMissedEvent miss&&miss.Actor==used.Actor&&miss.Move==used.Move)?"source-no-animation-result":"unmatched-source-mixed-effect-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Value,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if (action.Any(e => e is WildBattleMoveMissedEvent m && m.Actor == used.Actor && m.Move == used.Move ||
            e is WildBattleMoveFailedEvent f && f.Actor == used.Actor && f.Move == used.Move && !(move==150&&f.Target==used.Actor&&f.Reason=="nothing-happened") ||
            e is WildBattleAbilityPreventedEvent a && a.Actor == used.Actor && a.Move == used.Move ||
            e is WildBattleEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } t && t.Actor == used.Actor && t.Move == used.Move))
            return new(null, "source-no-animation-result");
        if(move is 113 or 115 or 277)
        {
            long result=0;
            if(move==277)
            {
                if(battle.EventIndex>=2&&battle.Events[battle.EventIndex-2] is WildBattleMtMoonVolatileEvent coat&&coat.Side==used.Actor&&coat.Turn==used.Turn&&coat.Volatile=="magic-coat"&&coat.Before is 0 or 1&&coat.After==1&&coat.Reason=="move"&&coat.Sequence<attack.Sequence)result=coat.Sequence;
            }
            else
            {
                var changed=action.OfType<WildBattleMtMoonVolatileEvent>().Where(e=>e.Side==used.Actor&&e.Volatile==(move==113?"light-screen":"reflect")&&e.Before==0&&e.After==5&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
                if(changed.Length==1)result=changed[0].Sequence;
            }
            if(used.Target!=used.Actor||result==0)return new(null,"unmatched-source-wall-state");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(move is 150 or 300)
        {
            // Splash's source animation precedes its intentional nothing-happened
            // message. Field sports animate only after the committed state change.
            var results=action.Where(e=>move==150
                ? e is WildBattleMoveFailedEvent splash&&splash.Actor==used.Actor&&splash.Target==used.Actor&&splash.Move==used.Move&&splash.Reason=="nothing-happened"&&splash.Sequence>pp[0].Sequence
                : e is WildBattleMtMoonVolatileEvent sport&&sport.Side==used.Actor&&sport.Volatile=="mud-sport"&&sport.Before==0&&sport.After==1&&sport.Reason=="move"&&sport.Sequence>pp[0].Sequence).ToArray();
            if(used.Target!=used.Actor||results.Length!=1)return new(null,"unmatched-source-self-action-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(move is 77 or 78 or 79 or 86 or 92 or 95 or 147)
        {
            bool Matches(uint status)=>move switch{77=>status==WildBattleStatus.Poison,92=>status==WildBattleSourceFieldEffects.ToxicPoison,78 or 86=>status==WildBattleStatus.Paralysis,_=>WildBattleStatus.IsSleeping(status)};
            var changed=action.OfType<WildBattleStatusChangedEvent>().Where(e=>e.Side==used.Target&&e.Before==0&&Matches(e.After)&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
            if(changed.Length!=1)return new(null,"unmatched-source-primary-status-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed[0].Sequence,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(move is 116 or 203)
        {
            var results=action.Where(e=>move==116
                ? e is WildBattleFocusEnergyEvent focus&&focus.Side==used.Actor&&focus.Sequence>pp[0].Sequence
                : e is WildBattleProtectAttemptEvent protect&&protect.Side==used.Actor&&protect.Move==used.Move&&protect.Succeeded&&protect.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-focus-or-endure-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(move==289)
        {
            var ready=action.OfType<WildBattleSnatchReadyEvent>().Where(e=>e.Actor==used.Actor&&e.Sequence>pp[0].Sequence).ToArray();
            if(ready.Length!=1)return new(null,"unmatched-source-snatch-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,ready[0].Sequence,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(move is 74 or 106 or 334 or 110 or 107 or 97 or 111 or 112)
        {
            var resultIndices=Enumerable.Range(battle.EventIndex+1,end-battle.EventIndex-1).Where(i=>battle.Events[i] is WildBattleStatChangedEvent).ToArray();
            if(resultIndices.Length!=1||CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=resultIndices[0]},true) is not {} result||result.SourceMoveId!=move)
                return new(null,"unmatched-source-self-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Sequence,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(move==48)
        {
            var results=action.OfType<WildBattleConfusionChangedEvent>().Where(e=>e.Side==used.Target&&e.Before==0&&e.After is >=2 and <=5&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-confusion-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(IsRecoveryAnimation(move))
        {
            var results=action.OfType<WildBattleRecoveredEvent>().Where(e=>e.Side==used.Actor&&e.Move==used.Move&&e.HpAfter>e.HpBefore&&e.Recovered==e.HpAfter-e.HpBefore&&e.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-recovery-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(ReductionAnimation(move) is {} reduction)
        {
            var results=action.OfType<WildBattleStatChangedEvent>().Where(e=>e.Actor==used.Actor&&e.Target==used.Target&&e.Stat==reduction.Stat&&e.Before is >=0 and <=12&&e.After<e.Before&&e.After==Math.Clamp(e.Before+reduction.Delta,0,12)).ToArray();
            if(results.Length!=1||results[0].Sequence<=pp[0].Sequence)return new(null,"unmatched-source-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,
                new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        var hits = action.Where(e => e is WildBattleHpChangedEvent h && h.Actor == used.Actor && h.Target == used.Target && h.Move == used.Move ||
            e is WildBattleSubstituteDamagedEvent s && s.Actor == used.Actor && s.Target == used.Target && s.Move == used.Move).ToArray();
        if (hits.Length != 1 || hits[0].Sequence <= pp[0].Sequence) return new(null, "unmatched-source-hit-result");
        // Damage and HP deltas are deliberately not eligibility inputs. Endure and substitute can
        // leave actual Pokémon HP unchanged while the original attack animation still executes.
        return new(new(battle.Id, used.Sequence, attack.Sequence, pp[0].Sequence, hits[0].Sequence, move,
            new((int)used.Actor), new((int)used.Target), hits[0] is WildBattleSubstituteDamagedEvent), "committed-source-animation");
    }
    public static CampaignMoveAnimationDecision AtAttackText(CampaignDoubleBattle battle,
        SourceBattleScene scene)
    {
        if (scene != SourceBattleScene.On) return new(null, "animations-disabled");
        if (battle.Stage != CampaignDoubleBattleStage.Combat || battle.EventIndex < 0 || battle.EventIndex >= battle.Events.Length)
            return new(null, "not-attack-text");
        // Accepted basic hit/slice moves print through SpendInvocationPp. The paired AttackString is silent;
        // MoveUsed is the actual visible page. Callers such as Metronome have a different current move.
        if (battle.Events[battle.EventIndex] is not SourceMoveUsedEvent used) return new(null, "not-attack-text");
        int move = SupportedSourceMoveId(used.Move.Value);
        if (move == 0) return new(null, "script-outside-slice");
        if(move is 3 or 4 or 167)return new(null,"source-per-hit-animation");
        if (battle.EventIndex == 0 || battle.Events[battle.EventIndex - 1] is not SourceAttackStringEvent attack ||
            attack.Actor != used.Actor || attack.CurrentMove != used.Move || attack.Turn != used.Turn)
            return new(null, "unmatched-source-attack-string");
        int end = battle.EventIndex + 1;
        while (end < battle.Events.Length && battle.Events[end].Turn == used.Turn &&
            battle.Events[end] is not (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent)) end++;
        var action = battle.Events.Skip(battle.EventIndex + 1).Take(end - battle.EventIndex - 1).ToArray();
        var pp = action.OfType<SourcePpCommandEvent>().ToArray();
        if(move is 51 or 75 or 129 or 145 or 157 or 181)
        {
            // Source MOVEEND_NEXT_TARGET revisits the hit script without another attack string.
            // One visible attack page owns both PP-command visits and one visual invocation.
            if(pp.Length is <1 or >2||pp[0].Target!=used.Target||pp.Select(p=>p.Target).Distinct().Count()!=pp.Length||
                pp.Any(p=>p.Actor!=used.Actor||p.CurrentMove!=used.Move||p.ChosenMove!=attack.ChosenMove||p.PpSlot!=used.Slot||p.Sequence<=used.Sequence||
                    p.Target.Side==used.Actor.Side||p.Target!=used.Target&&p.Target!=used.Target.Partner)||
                pp[0].Before!=used.PpBefore||pp[0].After!=used.PpAfter||pp.Skip(1).Any(p=>p.Before!=pp[0].After||p.After!=p.Before))
                return new(null,"unmatched-source-spread-pp");
            var spreadHits=action.Where(e=>e is SourceHpChangedEvent h&&h.Actor==used.Actor&&h.Move==used.Move||e is SourceSubstituteDamagedEvent d&&d.Actor==used.Actor&&d.Move==used.Move)
                .Select(e=>e switch{SourceHpChangedEvent h=>(h.Target,h.Sequence,Substitute:false),SourceSubstituteDamagedEvent d=>(d.Target,d.Sequence,Substitute:true),_=>throw new InvalidOperationException()}).ToArray();
            if(spreadHits.Length==0)return new(null,"source-no-animation-result");
            if(spreadHits.Length>2||spreadHits.Select(h=>h.Target).Distinct().Count()!=spreadHits.Length||spreadHits.Any(h=>!pp.Any(p=>p.Target==h.Target&&p.Sequence<h.Sequence)))
                return new(null,"unmatched-source-spread-hit");
            var hit=spreadHits[0];var hitPp=pp.Single(p=>p.Target==hit.Target);
            if(action.Any(e=>e is SourceMoveMissedEvent m&&m.Actor==used.Actor&&m.Target==hit.Target&&m.Move==used.Move||
                e is SourceMoveFailedEvent f&&f.Actor==used.Actor&&f.Target==hit.Target&&f.Move==used.Move||
                e is SourceAbilityPreventedEvent a&&a.Actor==used.Actor&&a.Target==hit.Target&&a.Move==used.Move))
                return new(null,"source-no-animation-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,hitPp.Sequence,hit.Sequence,move,used.Actor,hit.Target,hit.Substitute),"committed-source-animation");
        }
        if(move==39)
        {
            // The original BOTH-target stat script revisits ppreduce without spending
            // again. One visible attack page can own two target passes. Select the
            // first successful committed result, including when the first target is capped.
            if(pp.Length is <1 or >2||pp[0].Target!=used.Target||pp.Select(p=>p.Target).Distinct().Count()!=pp.Length||
                pp.Any(p=>p.Actor!=used.Actor||p.CurrentMove!=used.Move||p.ChosenMove!=attack.ChosenMove||p.PpSlot!=used.Slot||p.Sequence<=used.Sequence||
                    (p.Target.Value&1)==(used.Actor.Value&1)||p.Target!=used.Target&&p.Target!=new BattlerSlot(used.Target.Value^2))||
                pp[0].Before!=used.PpBefore||pp[0].After!=used.PpAfter||pp.Skip(1).Any(p=>p.Before!=pp[0].After||p.After!=p.Before))
                return new(null,"unmatched-source-both-stat-pp");
            var results=action.OfType<SourceStatChangedEvent>().Where(e=>e.Actor==used.Actor&&e.Stat==WildBattleStat.Defense&&e.After<e.Before).ToArray();
            if(results.Length is <1 or >2||results.Select(r=>r.Target).Distinct().Count()!=results.Length||results.Any(r=>!pp.Any(p=>p.Target==r.Target&&p.Sequence<r.Sequence)))
                return new(null,"unmatched-source-both-stat-result");
            var result=results[0];var ownPp=pp.Single(p=>p.Target==result.Target);
            if(action.Any(e=>e is SourceMoveMissedEvent m&&m.Actor==used.Actor&&m.Target==result.Target&&m.Move==used.Move||
                e is SourceMoveFailedEvent f&&f.Actor==used.Actor&&f.Target==result.Target&&f.Move==used.Move||
                e is SourceAbilityPreventedEvent a&&a.Actor==used.Actor&&a.Target==result.Target&&a.Move==used.Move))
                return new(null,"source-no-animation-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,ownPp.Sequence,result.Sequence,move,used.Actor,result.Target,false),"committed-source-animation");
        }
        if (pp.Length != 1 || pp[0].Actor != used.Actor || pp[0].Target != used.Target || pp[0].CurrentMove != used.Move)
            return new(null, "unmatched-source-pp-command");
        if (move is 120 or 153)
        {
            // BattleScript_EffectExplosion animates once immediately after setatkhptozero,
            // before every target pass, even when every pass later misses/is immune.
            var committed=action.OfType<SourceSelfDestructEvent>().ToArray();
            if(pp[0].ChosenMove!=used.Move||pp[0].PpSlot!=used.Slot||pp[0].Sequence<=used.Sequence||
                pp[0].Before!=used.PpBefore||pp[0].After!=used.PpAfter||committed.Length!=1||
                committed[0].Actor!=used.Actor||committed[0].Move!=used.Move||committed[0].HpBefore<=0||committed[0].Sequence<=pp[0].Sequence)
                return new(null,"unmatched-source-self-destruct");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,committed[0].Sequence,move,
                used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 207 or 321 or 339)
        {
            var result=CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle);
            if(result is null)return new(null,action.Any(e=>e is SourceMoveMissedEvent miss&&miss.Actor==used.Actor&&miss.Move==used.Move)?"source-no-animation-result":"unmatched-source-mixed-effect-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Value,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if (action.Any(e => e is SourceMoveMissedEvent m && m.Actor == used.Actor && m.Move == used.Move ||
            e is SourceMoveFailedEvent f && f.Actor == used.Actor && f.Move == used.Move && !(move==150&&f.Target==used.Actor&&f.Reason=="nothing-happened") ||
            e is SourceAbilityPreventedEvent a && a.Actor == used.Actor && a.Move == used.Move ||
            e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } t && t.Actor == used.Actor && t.Move == used.Move))
            return new(null, "source-no-animation-result");
        if(move is 113 or 115 or 277)
        {
            long result=0;
            if(move==277)
            {
                if(battle.EventIndex>=2&&battle.Events[battle.EventIndex-2] is SourceMtMoonVolatileEvent coat&&coat.Side==used.Actor&&coat.Turn==used.Turn&&coat.Volatile=="magic-coat"&&coat.Before is 0 or 1&&coat.After==1&&coat.Reason=="move"&&coat.Sequence<attack.Sequence)result=coat.Sequence;
            }
            else
            {
                var changed=action.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Side==used.Actor&&e.Volatile==(move==113?"light-screen":"reflect")&&e.Before==0&&e.After==5&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
                if(changed.Length==1)result=changed[0].Sequence;
            }
            if(used.Target!=used.Actor||result==0)return new(null,"unmatched-source-wall-state");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 150 or 300)
        {
            // Splash's source animation precedes its intentional nothing-happened
            // message. Field sports animate only after the committed state change.
            var results=action.Where(e=>move==150
                ? e is SourceMoveFailedEvent splash&&splash.Actor==used.Actor&&splash.Target==used.Actor&&splash.Move==used.Move&&splash.Reason=="nothing-happened"&&splash.Sequence>pp[0].Sequence
                : e is SourceMtMoonVolatileEvent sport&&sport.Side==used.Actor&&sport.Volatile=="mud-sport"&&sport.Before==0&&sport.After==1&&sport.Reason=="move"&&sport.Sequence>pp[0].Sequence).ToArray();
            if(used.Target!=used.Actor||results.Length!=1)return new(null,"unmatched-source-self-action-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 77 or 78 or 79 or 86 or 92 or 95 or 147)
        {
            bool Matches(uint status)=>move switch{77=>status==WildBattleStatus.Poison,92=>status==WildBattleSourceFieldEffects.ToxicPoison,78 or 86=>status==WildBattleStatus.Paralysis,_=>WildBattleStatus.IsSleeping(status)};
            var changed=action.OfType<SourceStatusChangedEvent>().Where(e=>e.Side==used.Target&&e.Before==0&&Matches(e.After)&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
            if(changed.Length!=1)return new(null,"unmatched-source-primary-status-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 116 or 203)
        {
            var results=action.Where(e=>move==116
                ? e is SourceFocusEnergyEvent focus&&focus.Side==used.Actor&&focus.Sequence>pp[0].Sequence
                : e is SourceProtectAttemptEvent protect&&protect.Side==used.Actor&&protect.Move==used.Move&&protect.Succeeded&&protect.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-focus-or-endure-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,new(used.Actor.Value),new(used.Target.Value),false),"committed-source-animation");
        }
        if(move==289)
        {
            var ready=action.OfType<SourceSnatchReadyEvent>().Where(e=>e.Actor==used.Actor&&e.Sequence>pp[0].Sequence).ToArray();
            if(ready.Length!=1)return new(null,"unmatched-source-snatch-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,ready[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 74 or 106 or 334 or 110 or 107 or 97 or 111 or 112)
        {
            var resultIndices=Enumerable.Range(battle.EventIndex+1,end-battle.EventIndex-1).Where(i=>battle.Events[i] is SourceStatChangedEvent).ToArray();
            if(resultIndices.Length!=1||CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=resultIndices[0]}) is not {} result||result.SourceMoveId!=move)
                return new(null,"unmatched-source-self-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move==48)
        {
            var results=action.OfType<SourceConfusionChangedEvent>().Where(e=>e.Side==used.Target&&e.Before==0&&e.After is >=2 and <=5&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-confusion-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(IsRecoveryAnimation(move))
        {
            var results=action.OfType<SourceRecoveredEvent>().Where(e=>e.Side==used.Actor&&e.Move==used.Move&&e.HpAfter>e.HpBefore&&e.Recovered==e.HpAfter-e.HpBefore&&e.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-recovery-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(ReductionAnimation(move) is {} reduction)
        {
            var results=action.OfType<SourceStatChangedEvent>().Where(e=>e.Actor==used.Actor&&e.Target==used.Target&&e.Stat==reduction.Stat&&e.Before is >=0 and <=12&&e.After<e.Before&&e.After==Math.Clamp(e.Before+reduction.Delta,0,12)).ToArray();
            if(results.Length!=1||results[0].Sequence<=pp[0].Sequence)return new(null,"unmatched-source-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,
                used.Actor,used.Target,false),"committed-source-animation");
        }
        var hits = action.Where(e => e is SourceHpChangedEvent h && h.Actor == used.Actor && h.Target == used.Target && h.Move == used.Move ||
            e is SourceSubstituteDamagedEvent s && s.Actor == used.Actor && s.Target == used.Target && s.Move == used.Move).ToArray();
        if (hits.Length != 1 || hits[0].Sequence <= pp[0].Sequence) return new(null, "unmatched-source-hit-result");
        // Damage and HP deltas are deliberately not eligibility inputs. Endure and substitute can
        // leave actual Pokémon HP unchanged while the original attack animation still executes.
        return new(new(battle.Id, used.Sequence, attack.Sequence, pp[0].Sequence, hits[0].Sequence, move,
            used.Actor, used.Target, hits[0] is SourceSubstituteDamagedEvent), "committed-source-animation");
    }
}
