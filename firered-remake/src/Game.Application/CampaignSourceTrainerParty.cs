using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Adapter between stable owned slots and the complete source battle party.</summary>
public sealed partial class CampaignSession
{
    private static bool SourceTrainerBatonPassPending(ForestTrainerCombatState combat)=>
        combat.SourceMoveRulesVersion==ForestTrainerCombatRules.SourceTrainerContractVersion&&combat.SourceTurn is not null&&
        combat.Battle.CalledParty?.PendingSwitch is{Side:WildBattleSide.Player,Kind:WildBattleCalledSwitchKind.BatonPass};

    public bool SourceTrainerPartyChoicePending=>State.ForestTrainerBattle is {} trainer&&SourceTrainerBatonPassPending(trainer.Combat);

    private Decision<CampaignState> DecideSourceTrainerPartyChoice(CampaignState state,CampaignCommand command)
    {
        var trainer=state.ForestTrainerBattle!;var presentation=trainer.Presentation;
        if(!SourceTrainerBatonPassPending(trainer.Combat))throw new InvalidDataException("source_trainer_party_choice_context");
        if(command.Kind!="battle_switch")return new(CommandStatus.Rejected,state,"source_trainer_party_choice_required");
        int index=command.PartyIndex;
        if(!trainer.Combat.Battle.CalledParty!.PendingSwitch!.LegalPartyIndices.Contains(index)||
            index<0||index>=state.Party.Length||state.Party[index].IsEgg||state.Party[index].Pokemon.CurrentHp==0)
            return new(CommandStatus.Rejected,state,"source_trainer_party_choice_invalid");
        var result=ForestTrainerCombatRules.ChooseSourceParty(OrdinaryExecutionPokemon(trainer.Combat.Battle),trainer.Combat,index);
        if(!result.Succeeded)return new(CommandStatus.Rejected,state,result.Fault);
        var incoming=state.Party[index];
        // A living Baton Pass user has requested a mandatory selection; this is neither
        // a fainted replacement nor a new ordinary switch turn. The Domain resumes its queue.
        return new(CommandStatus.Accepted,CommitForestTrainerTurn(state with{ForestTrainerBattle=trainer with
        {Presentation=presentation with{ActivePartyIndex=index,OwnedBefore=incoming,Replacement=null,LastSwitch=null,
            ParticipatingMembers=presentation.ParticipatingMembers!.Add(incoming.InstanceId),
            OpponentHpAtLastSwitch=trainer.Combat.Battle.Opponent.Pokemon.CurrentHp}}},result));
    }

    private ImmutableArray<WildBattleCombatant> SourceTrainerEntryParty(CampaignState state)
    {
        if(state.Party.IsDefaultOrEmpty||state.Party.Length>6||state.Party.Any(p=>p.IsEgg||
            p.Pokemon.OriginalTrainerId!=state.TrainerId||p.OriginalTrainerName!=state.PlayerName||p.OriginalTrainerGender!=state.PlayerGender))
            throw new InvalidDataException("source_trainer_owned_entry_identity");
        return state.Party.Select(p=>new WildBattleCombatant(p.Pokemon,HeldItem:p.HeldItem,Status:p.Status)
        {
            AttackBadgeBoost=_content.PewterEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),
            SpeedBadgeBoost=_content.VermilionGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),
            DefenseBadgeBoost=_content.FuchsiaGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET")
        }).ToImmutableArray();
    }

    private ForestTrainerCombatResult PrepareFuchsiaSourceTrainerBattle(CampaignState state,TrainerId trainer)
    {
        var party=SourceTrainerEntryParty(state);
        int active=Enumerable.Range(0,party.Length).First(i=>party[i].Pokemon.CurrentHp>0);
        var style=EffectiveBattleStyle(state);var terrain=CampaignBattleTerrain(state);
        var owned=SafariOrdinaryOwnership;var catalog=owned?.Predecessor??BattlePokemon;
        var result=IsFuchsiaGymTrainer(trainer) ? ForestTrainerCombatRules.Start(_content.FuchsiaGymTrainerParties!,catalog,trainer,party,active,state.World.Rng,style,owned)
            : IsFuchsiaRouteTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.FuchsiaRouteTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsPokemonTowerTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.PokemonTowerTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsRocketHideoutTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.RocketHideoutTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsCeladonGymTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.CeladonGymTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsCeladonApproachTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.CeladonApproachTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsRockTunnelTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.RockTunnelTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsRoute11Trainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.Route11Trainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsVermilionGymTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.VermilionGymTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsSsAnneTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.SsAnneTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsVermilionTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.VermilionTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsBillTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.BillTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsCeruleanTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.CeruleanTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsMtMoonTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.MtMoonTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsRoute3Trainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.Route3Trainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsRoute22Trainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.Route22Trainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : IsPewterTrainer(trainer) ? ForestTrainerCombatRules.StartSource(_content.PewterTrainers!,catalog,trainer,party,active,state.World.Rng,style,terrain,owned)
            : ForestTrainerCombatRules.StartSource(ForestTrainers,catalog,trainer,party,active,state.World.Rng,style,terrain,owned);
        if(!result.Succeeded)throw new InvalidDataException(result.Fault);
        if(result.State!.SourceMoveRulesVersion!=ForestTrainerCombatRules.SourceTrainerContractVersion)
            throw new InvalidDataException("source_trainer_entry_protocol");
        if(owned is not null)result=result with{State=result.State with{Battle=result.State.Battle with{Obedience=CampaignObedienceContext(state)}}};
        return result;
    }

    private static bool SourceOwnedIdentity(PokemonInstance current,PokemonInstance owned)=>
        current.Species==owned.Species&&current.SourceSpeciesId==owned.SourceSpeciesId&&
        current.OriginalTrainerId==owned.OriginalTrainerId&&current.Personality==owned.Personality&&
        current.Nature==owned.Nature&&current.Ivs==owned.Ivs&&current.Gender==owned.Gender&&
        current.AbilitySlot==owned.AbilitySlot&&current.AbilityId==owned.AbilityId;

    private static bool SameSourceOwnedPokemon(PokemonInstance a,PokemonInstance b)=>
        a with{Moves=b.Moves}==b&&!a.Moves.IsDefault&&!b.Moves.IsDefault&&a.Moves.SequenceEqual(b.Moves);

    private CampaignForestTrainerBattle ValidateSourceTrainerOwnedParty(CampaignState state,CampaignForestTrainerBattle trainer)
    {
        var combat=trainer.Combat;
        if(combat.SourceMoveRulesVersion!=ForestTrainerCombatRules.SourceTrainerContractVersion||
            combat.Battle.CalledParty is not {} party||party.Player.Length!=state.Party.Length||
            combat.SsAnnePlayerParty is not {} members||members.Length!=state.Party.Length||
            party.PlayerActiveIndex!=trainer.Presentation.ActivePartyIndex||combat.CeruleanPlayerPartyIndex!=party.PlayerActiveIndex)
            throw new InvalidDataException("source_trainer_owned_party_context");
        for(int i=0;i<state.Party.Length;i++)
        {
            var owned=state.Party[i];var member=party.Player[i];
            if(owned.IsEgg||member.IsEgg||members[i] is null||!SourceOwnedIdentity(member.Pokemon,owned.Pokemon)||
                !SameSourceOwnedPokemon(member.Pokemon,owned.Pokemon)||member.Status!=owned.Status||
                WildBattleSourcePartyEffects.PersistentHeldItem(member)!=owned.HeldItem)
                throw new InvalidDataException("source_trainer_owned_party_mismatch");
        }
        return trainer;
    }

    private static CampaignState ProjectSourceTrainerOwnedParty(CampaignState state,ForestTrainerCombatState combat)
    {
        if(combat.SourceMoveRulesVersion!=ForestTrainerCombatRules.SourceTrainerContractVersion||
            combat.Battle.CalledParty is not {} party||party.Player.Length!=state.Party.Length)
            throw new InvalidDataException("source_trainer_owned_result_context");
        var owned=state.Party.ToBuilder();
        for(int i=0;i<owned.Count;i++)
        {
            var member=party.Player[i];
            if(owned[i].IsEgg||member.IsEgg||!SourceOwnedIdentity(member.Pokemon,owned[i].Pokemon))
                throw new InvalidDataException("source_trainer_owned_result_identity");
            // CalledParty records contain persistent originals, including Transform originals.
            // Identity, nickname, met data and every other ownership property remain untouched.
            owned[i]=owned[i] with{Pokemon=member.Pokemon,Status=member.Status,
                HeldItem=WildBattleSourcePartyEffects.PersistentHeldItem(member)};
        }
        return state with{Party=owned.ToImmutable()};
    }

    private static ForestTrainerCombatState SyncSourceTrainerOwnedPokemon(ForestTrainerCombatState combat,int index,PartyPokemon owned)
    {
        if(combat.SourceMoveRulesVersion!=ForestTrainerCombatRules.SourceTrainerContractVersion||
            combat.Battle.CalledParty is not {} party||combat.SsAnnePlayerParty is not {} members||
            index<0||index>=party.Player.Length||members.Length!=party.Player.Length||members[index] is not {} mon)
            throw new InvalidDataException("source_trainer_owned_reward_context");
        var member=party.Player[index];
        if(owned.IsEgg||!SourceOwnedIdentity(member.Pokemon,owned.Pokemon)||member.Status!=owned.Status||
            WildBattleSourcePartyEffects.PersistentHeldItem(member)!=owned.HeldItem)
            throw new InvalidDataException("source_trainer_owned_reward_identity");
        mon=WildBattleTransformationRules.ApplyPersistentPokemon(mon,owned.Pokemon);
        party=party with{Player=party.Player.SetItem(index,member with{Pokemon=owned.Pokemon})};
        var battle=combat.Battle with{CalledParty=party};
        if(index==party.PlayerActiveIndex)battle=battle with{Player=mon};
        return combat with{Battle=battle,SsAnnePlayerParty=members.SetItem(index,mon)};
    }

    private static ForestTrainerCombatState SyncSourceTrainerPickup(ForestTrainerCombatState combat,ImmutableArray<PartyPokemon> owned)
    {
        if(combat.SourceMoveRulesVersion!=ForestTrainerCombatRules.SourceTrainerContractVersion||combat.Battle.Outcome!=WildBattleOutcome.Won||
            combat.Battle.CalledParty is not {} party||combat.SsAnnePlayerParty is not {} members||
            party.Player.Length!=owned.Length||members.Length!=owned.Length)
            throw new InvalidDataException("source_trainer_pickup_context");
        var battle=combat.Battle;
        for(int i=0;i<owned.Length;i++)
        {
            var member=party.Player[i];var mon=members[i]??throw new InvalidDataException("source_trainer_pickup_member");
            if(!SameSourceOwnedPokemon(member.Pokemon,owned[i].Pokemon)||member.Status!=owned[i].Status)
                throw new InvalidDataException("source_trainer_pickup_identity");
            string previous=WildBattleSourcePartyEffects.PersistentHeldItem(member);
            if(previous==owned[i].HeldItem)continue;
            // The caller has just applied the authenticated Pickup settlement. It can only
            // fill an empty persistent held-item slot after a won battle.
            if(previous!="ITEM_NONE"||owned[i].HeldItem=="ITEM_NONE")
                throw new InvalidDataException("source_trainer_pickup_item");
            member=member with{HeldItem=owned[i].HeldItem,KnockedOffItem=null,PersistentHeldItemOverride=null};
            mon=mon with{HeldItem=owned[i].HeldItem,KnockedOffItem=null};
            party=party with{Player=party.Player.SetItem(i,member)};
            members=members.SetItem(i,mon);
            if(i==party.PlayerActiveIndex)battle=battle with{Player=mon};
        }
        return combat with{Battle=battle with{CalledParty=party},SsAnnePlayerParty=members};
    }
}
