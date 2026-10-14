using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>MON_DATA_POKERUS == 0 versus != 0, including cured infection. PartyPokemon and the
/// battle party have no Pokerus field. Callers must obtain this fact from authoritative persistent
/// state; assigning NeverInfected to every member is not a supported integration.</summary>
public enum SourceRewardPokerusHistory { Unknown, NeverInfected, InfectedOrCured }
public enum SourceRewardBattleKind { OrdinaryWild, OrdinaryTrainer }
/// <summary>Only facts absent from SourceBattleParty. Never a second Pokemon/HP/PP/item authority.</summary>
public sealed record SourceRewardMetadata(PartyMemberKey Key, string OriginalTrainerName,
    string MetLocation, string Ball, SourceRewardPokerusHistory PokerusHistory);
public sealed record SourceRewardContext(SourceRewardBattleKind BattleKind, uint PlayerTrainerId,
    string PlayerTrainerName, string CurrentRegion);
/// <summary>Snapshot of this exact fainted occupant and its opponent-flank participation mask.
/// The caller authenticates freshness against the battle; masks from different opponents must not merge.</summary>
public sealed record SourceRewardDefeat(OccupantToken Occupant, WildBattleCombatant BattleView,
    ImmutableArray<PartyMemberKey> Participants);
public sealed record SourceRewardMemberCalculation(PartyMemberKey Key, PokemonRewardResult Reward);
public sealed record SourceRewardCalculation(OccupantToken DefeatedOccupant,
    ImmutableArray<SourceRewardMemberCalculation> Members);

/// <summary>Pure calculation for one ordinary non-link opponent defeat. Does not admit a campaign,
/// mutate a frame, settle a receipt, or authorize a repeated award. The caller owns exact revision,
/// bounded opponent-party completion bits, atomic commit and ordered pending move decisions.</summary>
public static class SourceVictoryRewards
{
    public const string Version = "firered-source-reward-recipe-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;

    public static SourceRewardCalculation Calculate(SourceMechanicsCatalog mechanics, SourceBattleParty actualPlayerParty,
        ImmutableArray<SourceRewardMetadata> metadata, SourceRewardDefeat defeat, SourceRewardContext context, OwnedBattlePokemonCatalog? ownership = null)
    {
        ArgumentNullException.ThrowIfNull(mechanics);
        ArgumentNullException.ThrowIfNull(actualPlayerParty);
        ArgumentNullException.ThrowIfNull(defeat);
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(context.BattleKind) || !ValidName(context.PlayerTrainerName) || !ValidRegion(context.CurrentRegion))
            throw new ArgumentException("source_reward_context", nameof(context));
        var party = actualPlayerParty.Members;
        if (actualPlayerParty.Side != BattleSide.Player || party.IsDefaultOrEmpty || party.Length > 6 ||
            metadata.IsDefault || metadata.Length != party.Length)
            throw new ArgumentException("source_reward_party", nameof(actualPlayerParty));
        for (int i = 0; i < party.Length; i++)
        {
            if (metadata[i] is not { } facts || facts.Key != new PartyMemberKey(BattleSide.Player, i))
                throw new ArgumentException("source_reward_metadata_identity", nameof(metadata));
            ValidateMember(mechanics, party[i], facts, ownership);
        }
        var token = defeat.Occupant;
        var view = defeat.BattleView;
        if (token.Slot.Side != BattleSide.Opponent || token.Member.Side != BattleSide.Opponent || token.EntrySerial <= 0 ||
            view?.Pokemon is null || view.Pokemon.CurrentHp != 0 ||
            view.Transform is { } transform && (transform.OriginalPokemon is null || transform.CopiedPokemon is null))
            throw new ArgumentException("source_reward_defeat", nameof(defeat));
        if (WildBattleTransformationRules.Validate(mechanics.Catalog, view, enabled: true) is { } opponentError)
            throw new ArgumentException(opponentError, nameof(defeat));
        if (defeat.Participants.IsDefault || defeat.Participants.Length > party.Length ||
            defeat.Participants.Distinct().Count() != defeat.Participants.Length ||
            defeat.Participants.Any(key => key.Side != BattleSide.Player || key.Index >= party.Length))
            throw new ArgumentException("source_reward_participants", nameof(defeat));
        var participants = defeat.Participants.ToHashSet();
        int aliveParticipants = party.Where((m, i) => !m.IsEgg && m.Pokemon.CurrentHp > 0 && participants.Contains(metadata[i].Key)).Count();
        int aliveExpShareHolders = party.Count(m => !m.IsEgg && m.Pokemon.CurrentHp > 0 && HeldItem(m) == "ITEM_EXP_SHARE");
        var results = ImmutableArray.CreateBuilder<SourceRewardMemberCalculation>();
        for (int i = 0; i < party.Length; i++)
        {
            var member = party[i]; var facts = metadata[i]; string held = HeldItem(member);
            bool participated = participants.Contains(facts.Key), share = held == "ITEM_EXP_SHARE";
            if (member.IsEgg || member.Pokemon.CurrentHp == 0 || member.Pokemon.Level == 100 || !participated && !share) continue;
            // IsOtherTrainer compares all 32 OT-ID bits and OT-name bytes until the OT EOS.
            // Language, gender and the player's trailing name bytes are not compared by this source.
            bool traded = member.Pokemon.OriginalTrainerId != context.PlayerTrainerId ||
                !context.PlayerTrainerName.StartsWith(facts.OriginalTrainerName, StringComparison.Ordinal);
            int evMultiplier = (facts.PokerusHistory == SourceRewardPokerusHistory.InfectedOrCured ? 2 : 1) *
                (held == "ITEM_MACHO_BRACE" ? 2 : 1);
            var result = PokemonRewardRecipe.Calculate(mechanics.Catalog, member.Pokemon,
                mechanics.Catalog.Species[view.Pokemon.Species], view.Pokemon.Level, aliveParticipants, aliveExpShareHolders,
                new(participated, share, held == "ITEM_LUCKY_EGG", context.BattleKind == SourceRewardBattleKind.OrdinaryTrainer,
                    traded, evMultiplier, held == "ITEM_SOOTHE_BELL", facts.Ball == "ITEM_LUXURY_BALL",
                    facts.MetLocation == context.CurrentRegion), mechanics: mechanics);
            if (SourcePokemonValidator.ValidatePermanent(mechanics, result.Pokemon) is { } resultError)
                throw new InvalidOperationException(resultError);
            results.Add(new(facts.Key, result));
        }
        // A fully checked empty result is legitimate when nobody is eligible; not a zero-EXP acknowledgement.
        return new(token, results.ToImmutable());
    }

    private static void ValidateMember(SourceMechanicsCatalog mechanics, WildBattleCalledPartyMember? member, SourceRewardMetadata facts, OwnedBattlePokemonCatalog? ownership)
    {
        if (!Enum.IsDefined(facts.PokerusHistory) || facts.PokerusHistory == SourceRewardPokerusHistory.Unknown)
            throw new NotSupportedException("source_reward_pokerus_history_required");
        if (member?.Pokemon is null) throw new ArgumentException("source_reward_pokemon_missing", nameof(member));
        if (SourcePokemonValidator.ValidatePartyMember(mechanics.Catalog, member, ownership) is { } error)
            throw new ArgumentException(error, nameof(member));
        if (!ValidName(facts.OriginalTrainerName) ||
            !ValidRegion(facts.MetLocation) && facts.MetLocation != NpcTradePokemonOriginRules.MetLocation &&
                !(member.IsEgg && facts.MetLocation == SourceGiftEggRules.MetLocation) ||
            !NpcTradePokemonOriginRules.MatchesRewardMetadata(mechanics.Catalog, member.Pokemon,
                facts.OriginalTrainerName, facts.MetLocation, facts.Ball) ||
            facts.Ball is not ("ITEM_MASTER_BALL" or "ITEM_ULTRA_BALL" or "ITEM_GREAT_BALL" or "ITEM_POKE_BALL" or
                "ITEM_SAFARI_BALL" or "ITEM_NET_BALL" or "ITEM_DIVE_BALL" or "ITEM_NEST_BALL" or "ITEM_REPEAT_BALL" or
                "ITEM_TIMER_BALL" or "ITEM_LUXURY_BALL" or "ITEM_PREMIER_BALL"))
            throw new ArgumentException("source_reward_ownership_metadata", nameof(facts));
        if (!SourcePartyStatusRules.IsSupported(member.Status, allowSleep: true, allowFreeze: true, allowToxic: true))
            throw new NotSupportedException("source_reward_party_metadata_unsupported");
        foreach (string? item in new[] { member.HeldItem, member.KnockedOffItem, member.PersistentHeldItemOverride })
            if (item is not null && !SupportsHeldItem(mechanics.Catalog, item))
                throw new NotSupportedException("source_reward_held_item_unsupported");
        if (member.HeldItem is null || member.KnockedOffItem == "ITEM_NONE")
            throw new ArgumentException("source_reward_held_item_metadata", nameof(member));
    }

    // Cmd_getexp and MonGainEVs read the party item. Knock Off's battle-only suppression does not
    // suppress EXP Share, Lucky Egg, Macho Brace or Soothe Bell, whereas real consumption does.
    private static string HeldItem(WildBattleCalledPartyMember member) => WildBattleSourcePartyEffects.PersistentHeldItem(member);
    private static bool SupportsHeldItem(PokemonCatalog catalog, string item) => item is "ITEM_EXP_SHARE" or "ITEM_LUCKY_EGG" or "ITEM_MACHO_BRACE" or "ITEM_SOOTHE_BELL" || SourceBattleAdmission.SupportsHeldItem(catalog, item);
    private static bool ValidName(string? value) => !string.IsNullOrEmpty(value) && value.Length <= 7 &&
        value.Any(c => c != ' ') && value.All(PokemonStateValidator.NicknameAlphabet.Contains);
    private static bool ValidRegion(string? value) => value is not null && value.Length is > 7 and <= 128 &&
        value.StartsWith("MAPSEC_", StringComparison.Ordinal) && value[7..].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
