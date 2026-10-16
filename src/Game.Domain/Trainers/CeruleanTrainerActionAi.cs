using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Shared pinned ShouldSwitch, with historical singles and validated actual-slot contexts.
/// Runs before trainer medicine or move scoring,
/// even when no switch results. Opt-in source overlays include copied Natural Cure and Perish Song.</summary>
public static class CeruleanTrainerActionAi
{
    public static CeruleanTrainerSwitchDecision DecideSwitch(CeruleanTrainerSwitchInput input,
        RngState rng, SourceTrainerAiSlotContext slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var catalog = slots.Catalog;
        CeruleanTrainerSwitchDecision Fault(string code) => new(TransitionStatus.Fault,null,rng,[],new(code,"Invalid trainer switch context."));
        if (input is null || input.Party.IsDefaultOrEmpty || input.Party.Length > 6 ||
            input.ActiveIndex < 0 || input.ActiveIndex >= input.Party.Length || input.PlayerPartyIndex is < 0 or > 5 ||
            input.Party.Any(m => m is null || m.Pokemon is null)) return Fault("cerulean_switch_party");
        if (!slots.Matches(input.User, input.Target) || !slots.ActualParty.SequenceEqual(input.Party))
            return Fault("source_four_trainer_switch_context");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("cerulean_switch_rng_algorithm");
        if (input.LastHitBy is { } lastSide && !Enum.IsDefined(lastSide)) return Fault("cerulean_switch_last_hit_by");
        var last = input.LastLandedMove is null || input.LastLandedMove.Value.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" || (!slots.Doubles && input.LastLandedMove.Value.Value == "pret:MOVE_STRUGGLE") ? null :
            catalog.Moves.GetValueOrDefault(input.LastLandedMove.Value);
        if (input.LastLandedMove is { } lastId && lastId.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" or "pret:MOVE_STRUGGLE") &&
            (last is null || !SourceMoveDefinitionRules.IsValid(last))) return Fault("cerulean_switch_last_move");
        var current = rng; var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int Draw(string purpose,int bound)
        {
            var next = Gen3Rng.Next(current,"cerulean-trainer.switch."+purpose,bound); current=next.State;trace.Add(next.Trace);return next.Trace.Result;
        }
        CeruleanTrainerSwitchDecision Done(int? index=null) => new(TransitionStatus.Accepted,index,current,trace.ToImmutable(),null);
        bool Living(int index) => slots.SwitchEligible(index);
        int? Replacement()
        {
            var context = slots;
            var result = SourceFourTrainerAi.ReplacementCore(context.WithTarget(context.Target, current), forced: false);
            if (!result.Succeeded) throw new ArgumentException(result.Fault);
            current = result.Rng; trace.AddRange(result.Trace); return result.PartyIndex;
        }
        bool HasSuperEffective(bool noRng)
        {
            var targets = new[] { new BattlerSlot(0), new BattlerSlot(2) }
                .Where(s => !slots.Frame.IsSourceAbsent(s)).Select(s => slots.Frame.Get(s).Body!).ToArray();
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                for (int slot=0;slot<input.User.Pokemon.Moves.Length;slot++)
                    if ((TypeFlags(catalog,catalog.Moves[input.User.Pokemon.Moves[slot].Move],targets[targetIndex]) & SuperEffective) != 0 &&
                        (noRng || Draw(!slots.Doubles ? $"has-super-effective.slot.{slot}" : $"has-super-effective.target.{targetIndex}.slot.{slot}",10) != 0)) return true;
            return false;
        }
        int? FindResistant(int flag,int modulus)
        {
            if (last is null || last.Power == 0 || slots.Frame.Get(slots.Actor).Registers.LastHitBy is null) return null;
            BattlerSlot? first = slots.Actor;
            for (int i=0;i<input.Party.Length;i++)
            {
                if (!slots.SwitchEligible(i, first)) continue;
                var candidate=input.Party[i];
                if ((TypeFlags(catalog,last,candidate)&flag)==0) continue;
                // Source reuses battlerIn1 for last attacker; later candidate filters now also
                // compare to the player's current party index, a source indexing bug.
                var target = input.Target;
                first = slots.Frame.Get(slots.Actor).Registers.LastHitBy; target = slots.Frame.Get(first!.Value).Body!;
                for(int slot=0;slot<candidate.Pokemon.Moves.Length;slot++)
                    if ((TypeFlags(catalog,catalog.Moves[candidate.Pokemon.Moves[slot].Move],target)&SuperEffective)!=0 &&
                        Draw($"resist.{flag}.party.{i}.slot.{slot}",modulus)==0) return i;
            }
            return null;
        }
        try
        {
            var species=WildBattleEffective.Types(catalog,input.User);
            bool OtherSide(string ability) => slots.Frame.Slots.Any(c => c.Slot.Side != slots.Actor.Side && !slots.Frame.IsSourceAbsent(c.Slot) && WildBattleEffective.Ability(c.Body!) == ability);
            bool MagnetPull() => slots.Frame.Slots.Any(c => !slots.Frame.IsSourceAbsent(c.Slot) && WildBattleEffective.Ability(c.Body!) == "ABILITY_MAGNET_PULL");
            if (slots.Frame.Get(slots.Actor).Registers.MeanLookSource is not null ||
                input.User.Rooted || input.User.BindTurnsRemaining>0 || OtherSide("ABILITY_SHADOW_TAG") || OtherSide("ABILITY_ARENA_TRAP") ||
                MagnetPull() && (species.Type1=="TYPE_STEEL" || species.Type2=="TYPE_STEEL")) return Done();
            if (!Enumerable.Range(0,input.Party.Length).Any(Living)) return Done();
            if (input.User.CalledVolatile?.PerishSongTimer == 0)
                return Done(Replacement());
            if (!slots.Doubles && WildBattleEffective.Ability(input.Target) == "ABILITY_WONDER_GUARD" && !HasSuperEffective(true))
                for (int i=0;i<input.Party.Length;i++) {
                    if (!Living(i)) continue;
                    for (int slot=0;slot<input.Party[i].Pokemon.Moves.Length;slot++)
                        if ((TypeFlags(catalog,catalog.Moves[input.Party[i].Pokemon.Moves[slot].Move],input.Target)&SuperEffective)!=0 &&
                            Draw($"wonder-guard.party.{i}.slot.{slot}",3)<2) return Done(i);
                }
            // Source evaluates this gate even before checking MOVE_NONE.
            bool skipAbsorption = HasSuperEffective(true) && Draw("absorption-super-effective",3) != 0;
            if (!skipAbsorption && last is { Power: > 0 })
            {
                string? absorbingAbility = last.Type switch {
                    "TYPE_FIRE" => "ABILITY_FLASH_FIRE", "TYPE_WATER" => "ABILITY_WATER_ABSORB",
                    "TYPE_ELECTRIC" => "ABILITY_VOLT_ABSORB", _ => null };
                if (absorbingAbility is not null && WildBattleEffective.Ability(input.User) != absorbingAbility)
                    for (int i=0; i<input.Party.Length; i++)
                        if (Living(i) && WildBattleEffective.Ability(input.Party[i]) == absorbingAbility && Draw($"absorption.party.{i}",2) != 0)
                            return Done(i);
            }
            // Source overlays make Natural Cure reachable after Skill Swap/Role Play.
            if (WildBattleStatus.IsSleeping(input.User.Status) &&
                WildBattleEffective.Ability(input.User)=="ABILITY_NATURAL_CURE" && input.User.Pokemon.CurrentHp>=input.User.Pokemon.Stats.MaxHp/2)
            {
                if (last is null && Draw("natural-cure-no-last-move",2)!=0)
                    return Done(Replacement());
                // MOVE_NONE has power zero; the source performs this second draw after a failed first draw.
                if ((last is null || last.Power==0) && Draw("natural-cure-status-last-move",2)!=0)
                    return Done(Replacement());
                int? resistant=FindResistant(DoesNotAffect,1) ?? FindResistant(NotVeryEffective,1);
                if (resistant is not null) return Done(resistant);
                if (Draw("natural-cure-fallback",2)!=0) return Done(Replacement());
            }
            if (HasSuperEffective(false)) return Done();
            int raised = new[]{input.User.AttackStage,input.User.DefenseStage,input.User.SpeedStage,
                input.User.AccuracyStage,input.User.EvasionStage,input.User.SpecialAttackStageDelta+6,input.User.SpecialDefenseStageDelta+6}
                .Sum(s=>Math.Max(0,s-6));
            if(raised>3) return Done();
            return Done(FindResistant(DoesNotAffect,2) ?? FindResistant(NotVeryEffective,3));
        }
        catch(ArgumentException) { return Fault("source_four_trainer_switch_replacement"); }
        catch(OverflowException) { return Fault("cerulean_switch_rng_exhausted"); }
    }
    internal const int DoesNotAffect=8,NotVeryEffective=4,SuperEffective=2;
    internal static int TypeFlags(PokemonCatalog catalog,PokemonMoveDefinition move,WildBattleCombatant target)
    {
        if(move.Id==WildBattleRules.StruggleMove) return 0;
        if(move.Type=="TYPE_GROUND" && WildBattleEffective.Ability(target)=="ABILITY_LEVITATE") return DoesNotAffect;
        // AI_TypeCalc takes a species argument and intentionally uses base types, even after Conversion.
        var d=catalog.Species[target.Pokemon.Species];int value=10;
        ElementType Type(string s)=>Enum.Parse<ElementType>(s[5..],true);
        foreach(var e in TypeRules.OrderedEffects(Type(move.Type),Type(d.Type1),Type(d.Type2))) value=value*e.MultiplierTenths/10;
        if(value==0) return DoesNotAffect;
        int flags=move.Power==0?0:value>10?SuperEffective:value<10?NotVeryEffective:0;
        if (WildBattleEffective.Ability(target)=="ABILITY_WONDER_GUARD" && move.Power>0 && value<=10) flags|=DoesNotAffect;
        return flags;
    }
}
