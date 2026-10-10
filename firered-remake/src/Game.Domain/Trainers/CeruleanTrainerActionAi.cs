using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned ordinary-singles ShouldSwitch. Runs before trainer medicine or move scoring,
/// even when no switch results. Opt-in source overlays include copied Natural Cure and Perish Song.</summary>
public static class CeruleanTrainerActionAi
{
    public static CeruleanTrainerSwitchDecision DecideSwitch(PokemonCatalog catalog, CeruleanTrainerSwitchInput input, RngState rng, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool towerRules = false, bool fuchsiaRules = false, SourceTrainerAiContext? source = null)
    {
        towerRules |= fuchsiaRules;
        rocketHideoutRules |= towerRules;
        celadonGymRules |= rocketHideoutRules;
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules; vermilionRules |= ssAnneRules; billRules |= vermilionRules;
        CeruleanTrainerSwitchDecision Fault(string code) => new(TransitionStatus.Fault,null,rng,[],new(code,"Invalid Cerulean trainer switch context."));
        if (catalog is null || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 98) != (celadonGymRules && !rocketHideoutRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 99) != (rocketHideoutRules && !towerRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 101 or 102) != (towerRules && !fuchsiaRules) || (catalog.Species.Count == 107) != fuchsiaRules) return Fault("celadon_gym_trainer_ai_catalog_scope");
        if (input is null || Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : 65) || input.Party.IsDefaultOrEmpty || input.Party.Length > 6 ||
            input.ActiveIndex < 0 || input.ActiveIndex >= input.Party.Length || input.PlayerPartyIndex is < 0 or > 5 ||
            input.Party.Any(m => m is null || m.Pokemon is null)) return Fault("cerulean_switch_party");
        if (source is not null && (!source.Matches(catalog,input.User,input.Target) || input.Trainer != source.State.Trainer ||
            input.ActiveIndex != source.State.OpponentIndex || !input.Party.SequenceEqual(source.Party))) return Fault("source_trainer_switch_context_mismatch");
        if (source is null)
        {
            if (CeruleanTrainerAi.ValidateCombatant(catalog,input.User,billRules,vermilionRules,ssAnneRules,vermilionGymRules, route11Rules, rockTunnelRules) is not null || CeruleanTrainerAi.ValidateCombatant(catalog,input.Target,billRules,vermilionRules,ssAnneRules,vermilionGymRules, route11Rules, rockTunnelRules) is not null ||
                input.User.Pokemon.CurrentHp == 0 || input.Target.Pokemon.CurrentHp == 0 || input.User.SpeedBadgeBoost || input.User.AttackBadgeBoost || input.User.HeldItem != "ITEM_NONE")
                return Fault("cerulean_switch_combatant");
            foreach (var m in input.Party)
                if (CeruleanTrainerAi.ValidateCombatant(catalog,m,billRules,vermilionRules,ssAnneRules,vermilionGymRules, route11Rules, rockTunnelRules) is not null || m.HeldItem != "ITEM_NONE" || m.AttackBadgeBoost || m.SpeedBadgeBoost)
                    return Fault("cerulean_switch_party_combatant");
            if (!CeruleanTrainerAiReplacement.SupportedParty(catalog,input.Party,input.Trainer,billRules,vermilionRules,ssAnneRules,vermilionGymRules, route11Rules, rockTunnelRules)) return Fault("cerulean_switch_party_scope");
            if (!SamePokemon(input.User.Pokemon,input.Party[input.ActiveIndex].Pokemon)) return Fault("cerulean_switch_active_identity");
        }
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("cerulean_switch_rng_algorithm");
        if (input.LastHitBy is { } lastSide && !Enum.IsDefined(lastSide)) return Fault("cerulean_switch_last_hit_by");
        var last = input.LastLandedMove is null || input.LastLandedMove.Value.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" or "pret:MOVE_STRUGGLE" ? null :
            catalog.Moves.GetValueOrDefault(input.LastLandedMove.Value);
        if (input.LastLandedMove is { } lastId && lastId.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" or "pret:MOVE_STRUGGLE") &&
            (last is null || !(source is not null ? MetronomeSourceMoveDefinition.Matches(last) : rockTunnelRules ? CeladonApproachBattleScope.ValidDefinitionForCatalog(catalog, last) : route11Rules ? Route11BattleScope.ValidDefinition(last) : vermilionGymRules ? VermilionGymBattleScope.ValidDefinition(last) : ssAnneRules ? SsAnneBattleScope.ValidDefinition(last) : vermilionRules ? VermilionBattleScope.ValidDefinition(last) : billRules ? BillBattleScope.ValidDefinition(last) : CeruleanBattleScope.ValidDefinition(last)))) return Fault("cerulean_switch_last_move");
        var current = rng; var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int Draw(string purpose,int bound)
        {
            var next = Gen3Rng.Next(current,"cerulean-trainer.switch."+purpose,bound); current=next.State;trace.Add(next.Trace);return next.Trace.Result;
        }
        CeruleanTrainerSwitchDecision Done(int? index=null) => new(TransitionStatus.Accepted,index,current,trace.ToImmutable(),null);
        bool Living(int index) => index != input.ActiveIndex && input.Party[index].Pokemon.CurrentHp > 0;
        bool HasSuperEffective(bool noRng)
        {
            for (int slot=0;slot<input.User.Pokemon.Moves.Length;slot++)
                if ((TypeFlags(catalog,catalog.Moves[input.User.Pokemon.Moves[slot].Move],input.Target) & SuperEffective) != 0 &&
                    (noRng || Draw($"has-super-effective.slot.{slot}",10) != 0)) return true;
            return false;
        }
        int? FindResistant(int flag,int modulus)
        {
            if (last is null || last.Power == 0 || input.LastHitBy is not WildBattleSide.Player) return null;
            bool sourceBattlerOverwritten = false;
            for (int i=0;i<input.Party.Length;i++)
            {
                if (!Living(i) || sourceBattlerOverwritten && i == input.PlayerPartyIndex) continue;
                var candidate=input.Party[i];
                if ((TypeFlags(catalog,last,candidate)&flag)==0) continue;
                // Source reuses battlerIn1 for last attacker; later candidate filters now also
                // compare to the player's current party index, a source indexing bug.
                sourceBattlerOverwritten=true;
                for(int slot=0;slot<candidate.Pokemon.Moves.Length;slot++)
                    if ((TypeFlags(catalog,catalog.Moves[candidate.Pokemon.Moves[slot].Move],input.Target)&SuperEffective)!=0 &&
                        Draw($"resist.{flag}.party.{i}.slot.{slot}",modulus)==0) return i;
            }
            return null;
        }
        try
        {
            var species=WildBattleEffective.Types(catalog,input.User);
            if (towerRules && input.User.MeanLookSource is not null || celadonGymRules && input.User.Rooted || input.User.BindTurnsRemaining>0 || WildBattleEffective.Ability(input.Target) is "ABILITY_SHADOW_TAG" or "ABILITY_ARENA_TRAP" ||
                (WildBattleEffective.Ability(input.Target)=="ABILITY_MAGNET_PULL" || WildBattleEffective.Ability(input.User)=="ABILITY_MAGNET_PULL") && (species.Type1=="TYPE_STEEL" || species.Type2=="TYPE_STEEL")) return Done();
            if (!Enumerable.Range(0,input.Party.Length).Any(Living)) return Done();
            if (source is not null && input.User.CalledVolatile?.PerishSongTimer == 0)
                return Done(SourceTrainerAi.Replacement(source).PartyIndex);
            if (source is not null && WildBattleEffective.Ability(input.Target) == "ABILITY_WONDER_GUARD" && !HasSuperEffective(true))
                for (int i=0;i<input.Party.Length;i++) {
                    if (!Living(i)) continue;
                    for (int slot=0;slot<input.Party[i].Pokemon.Moves.Length;slot++)
                        if ((TypeFlags(catalog,catalog.Moves[input.Party[i].Pokemon.Moves[slot].Move],input.Target)&SuperEffective)!=0 &&
                            Draw($"wonder-guard.party.{i}.slot.{slot}",3)<2) return Done(i);
                }
            // Source evaluates this gate even before checking MOVE_NONE.
            bool skipAbsorption = HasSuperEffective(true) && Draw("absorption-super-effective",3) != 0;
            if (route11Rules && !skipAbsorption && last is { Power: > 0 })
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
            if (source is not null && WildBattleStatus.IsSleeping(input.User.Status) &&
                WildBattleEffective.Ability(input.User)=="ABILITY_NATURAL_CURE" && input.User.Pokemon.CurrentHp>=input.User.Pokemon.Stats.MaxHp/2)
            {
                if (last is null && Draw("natural-cure-no-last-move",2)!=0)
                    return Done(SourceTrainerAi.Replacement(source).PartyIndex);
                // MOVE_NONE has power zero; the source performs this second draw after a failed first draw.
                if ((last is null || last.Power==0) && Draw("natural-cure-status-last-move",2)!=0)
                    return Done(SourceTrainerAi.Replacement(source).PartyIndex);
                int? resistant=FindResistant(DoesNotAffect,1) ?? FindResistant(NotVeryEffective,1);
                if (resistant is not null) return Done(resistant);
                if (Draw("natural-cure-fallback",2)!=0) return Done(SourceTrainerAi.Replacement(source).PartyIndex);
            }
            if (HasSuperEffective(false)) return Done();
            int raised = new[]{input.User.AttackStage,input.User.DefenseStage,input.User.SpeedStage,
                input.User.AccuracyStage,input.User.EvasionStage,input.User.SpecialAttackStageDelta+6,input.User.SpecialDefenseStageDelta+6}
                .Sum(s=>Math.Max(0,s-6));
            if(raised>3) return Done();
            return Done(FindResistant(DoesNotAffect,2) ?? FindResistant(NotVeryEffective,3));
        }
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
    internal static bool SamePokemon(PokemonInstance a,PokemonInstance b) => a with{Moves=b.Moves}==b && a.Moves.SequenceEqual(b.Moves);
}
