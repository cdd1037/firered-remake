using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private PokemonCatalog CanonicalTrainerPokemon => SafariOrdinaryOwnership?.Mechanics.Catalog ??
        throw new InvalidDataException("canonical_trainer_owned_catalog");
    private static bool SourceTrainerBatonPassPending(SourceBattleFrame frame) =>
        frame.FourTurn?.Phase == SourceFourActionPhase.ActionSwitchPending &&
        frame.PendingSwitches.Any(p => p.Slot.Value == 0 && p.Kind == WildBattleCalledSwitchKind.BatonPass);
    public bool SourceTrainerPartyChoicePending => State.ForestTrainerBattle is { } trainer && SourceTrainerBatonPassPending(trainer.Frame);
    private static SourceTurnContinuation TrainerContinuation(SourceBattleFrame frame) =>
        new(frame.Revision, frame.EventSequence, frame.NextAction, frame.FourTurn?.Phase ?? throw new InvalidDataException("canonical_trainer_continuation"));

    private Decision<CampaignState> DecideSourceTrainerPartyChoice(CampaignState state, CampaignCommand command)
    {
        var trainer = state.ForestTrainerBattle!; var frame = trainer.Frame;
        if (command.Kind != "battle_switch") return new(CommandStatus.Rejected, state, "source_trainer_party_choice_required");
        return CommitCanonicalTrainerResult(state, SourceFourActionTurns.ChooseActionSwitch(CanonicalTrainerPokemon, frame,
            TrainerContinuation(frame), new(0), new(BattleSide.Player, command.PartyIndex)));
    }

    private ImmutableArray<WildBattleCombatant> SourceTrainerEntryParty(CampaignState state)
    {
        if (state.Party.IsDefaultOrEmpty || state.Party.Length > 6 || state.Party.Any(p => !HasSupportedCampaignOriginalTrainer(state, p)))
            throw new InvalidDataException("source_trainer_owned_entry_identity");
        return state.Party.Select(p => new WildBattleCombatant(p.Pokemon, HeldItem: p.HeldItem, Status: p.Status)
        {
            IsEgg = p.IsEgg,
            AttackBadgeBoost = state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),
            SpeedBadgeBoost = state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),
            DefenseBadgeBoost = state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"),
            SpecialBadgeBoost = state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET")
        }).ToImmutableArray();
    }

    private SourceTrainerEncounterEntryResult PrepareCanonicalTrainerBattle(CampaignState state, TrainerId trainer)
    {
        var party = SourceTrainerEntryParty(state);
        int active = Enumerable.Range(0, party.Length).First(i => !party[i].IsEgg && party[i].Pokemon.CurrentHp > 0);
        var binding = OrdinaryBinding(trainer);
        var setup = SourceTrainerEncounterEntry.Prepare(state.World.Rng, binding.TracePrefix);
        if (!setup.Succeeded) throw new InvalidDataException(setup.Fault);
        var made = binding.Factory(trainer, setup.Rng);
        var rewards = new SourceFourRewardState {
            Context = new(SourceRewardBattleKind.OrdinaryTrainer, state.TrainerId, state.PlayerName,
                _content.MapDefinition(state.World.Player.MapId).RegionMapSection),
            Metadata = state.Party.Select((p, i) => new SourceRewardMetadata(new(BattleSide.Player, i),
                p.OriginalTrainerName, p.MetLocation, p.Ball, SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()
        };
        var result = SourceTrainerEncounterEntry.Begin(CanonicalTrainerPokemon, setup, made, party, active,
            EffectiveBattleStyle(state), CampaignBattleTerrain(state), SafariOrdinaryOwnership, rewards);
        if (!result.Succeeded) throw new InvalidDataException(result.Fault);
        return result with { State = result.State! with { Obedience = CampaignObedienceContext(state) } };
    }

    private static bool SourceOwnedIdentity(PokemonInstance current, PokemonInstance owned) =>
        current.Species == owned.Species && current.SourceSpeciesId == owned.SourceSpeciesId &&
        current.OriginalTrainerId == owned.OriginalTrainerId && current.Personality == owned.Personality &&
        current.Nature == owned.Nature && current.Ivs == owned.Ivs && current.Gender == owned.Gender &&
        current.AbilitySlot == owned.AbilitySlot && current.AbilityId == owned.AbilityId &&
        current.EggStatCalculationIvs == owned.EggStatCalculationIvs && current.NpcTradeOrigin == owned.NpcTradeOrigin && current.ContestConditions == owned.ContestConditions;

    private static bool SameSourceOwnedPokemon(PokemonInstance a, PokemonInstance b) =>
        a with { Moves = b.Moves } == b && !a.Moves.IsDefault && !b.Moves.IsDefault && a.Moves.SequenceEqual(b.Moves);

    private static CampaignState ProjectSourceTrainerOwnedParty(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation; var frame = trainer.Frame;
        var actual = frame.Party(BattleSide.Player);
        if (actual.Members.Length != state.Party.Length) throw new InvalidDataException("source_trainer_owned_count");
        var owned = state.Party.ToBuilder(); var members = actual.Members;
        var fainted = b.FaintedMembers ?? ImmutableSortedSet<long>.Empty;
        for (int i = 0; i < owned.Count; i++)
        {
            var member = members[i];
            if (member.IsEgg != owned[i].IsEgg || !SourceOwnedIdentity(member.Pokemon, owned[i].Pokemon))
                throw new InvalidDataException("source_trainer_owned_result_identity");
            var pokemon = member.Pokemon;
            if (pokemon.CurrentHp == 0 && !fainted.Contains(owned[i].InstanceId))
            {
                pokemon = pokemon with { Friendship = Math.Max(0, pokemon.Friendship - 1) };
                fainted = fainted.Add(owned[i].InstanceId); member = member with { Pokemon = pokemon }; members = members.SetItem(i, member);
                frame = frame with { Slots = frame.Slots.Select(c => c.Member == new PartyMemberKey(BattleSide.Player, i) ? c with {
                    Body = WildBattleTransformationRules.ApplyPersistentPokemon(c.Body!, pokemon) } : c).ToImmutableArray() };
            }
            owned[i] = owned[i] with { Pokemon = pokemon, Status = member.Status, HeldItem = WildBattleSourcePartyEffects.PersistentHeldItem(member) };
        }
        frame = frame with { Parties = frame.Parties.SetItem(0, actual with { Members = members }) };
        int active = frame.Get(new(0)).Member!.Value.Index;
        b = b with { Frame = frame, ActivePartyIndex = active, OwnedBefore = owned[active], FaintedMembers = fainted };
        return state with { Party = owned.ToImmutable(), ForestTrainerBattle = trainer with { Presentation = b } };
    }

    private static SourceBattleFrame SyncSourceTrainerPickup(SourceBattleFrame frame, ImmutableArray<PartyPokemon> owned)
    {
        if (frame.Outcome != WildBattleOutcome.Won || frame.Party(BattleSide.Player).Members.Length != owned.Length)
            throw new InvalidDataException("source_trainer_pickup_context");
        var party = frame.Party(BattleSide.Player);
        for (int i = 0; i < owned.Length; i++)
        {
            var member = party.Members[i];
            if (!SameSourceOwnedPokemon(member.Pokemon, owned[i].Pokemon) || member.Status != owned[i].Status)
                throw new InvalidDataException("source_trainer_pickup_identity");
            string previous = WildBattleSourcePartyEffects.PersistentHeldItem(member);
            if (previous == owned[i].HeldItem) continue;
            if (previous != "ITEM_NONE" || owned[i].HeldItem == "ITEM_NONE") throw new InvalidDataException("source_trainer_pickup_item");
            member = member with { HeldItem = owned[i].HeldItem, KnockedOffItem = null, PersistentHeldItemOverride = null };
            party = party with { Members = party.Members.SetItem(i, member) };
            frame = frame with { Slots = frame.Slots.Select(c => c.Member == new PartyMemberKey(BattleSide.Player, i) ? c with {
                Body = c.Body! with { HeldItem = owned[i].HeldItem, KnockedOffItem = null } } : c).ToImmutableArray() };
        }
        return frame with { Parties = frame.Parties.SetItem(0, party) };
    }
}
