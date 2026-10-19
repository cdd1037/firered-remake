using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

// Script text is admitted by the content contract. Display labels stay outside these rules.
public sealed record NameRaterSourceContent(string ChoosePokemonText, string ChoosePokemonCancelText);

public enum NameRaterEligibility { Eligible, Egg, ForeignTrainerId, ForeignTrainerName }
public sealed record NameRaterTarget(int PartyIndex, long InstanceId, string? OriginalCustomNickname);
// Changed describes stored nullable naming intent, not display-text equality.
public sealed record NameRaterRenameResult(ImmutableArray<PartyPokemon> Party, string? Error,
    string? OriginalCustomNickname = null, string? FinalCustomNickname = null, bool Changed = false)
{
    public bool Accepted => Error is null;
}

/// <summary>
/// Lavender source service. The caller owns the script, source text, waits and command transaction.
/// This service accepts party members only and never reads or mutates storage, identity, or RNG.
/// </summary>
public static class NameRaterRules
{
    public const int PartySize = 6;
    // party_menu.c SLOT_CANCEL is PARTY_SIZE + 1; map script branches on >= PARTY_SIZE.
    public const int CancelSlot = 7;
    public const int EggSpecies = 412;

    public static NameRaterEligibility Eligibility(PartyPokemon mon, uint playerTrainerId, string playerName)
    {
        if (mon.IsEgg) return NameRaterEligibility.Egg;
        if (mon.Pokemon.OriginalTrainerId != playerTrainerId) return NameRaterEligibility.ForeignTrainerId;
        return string.Equals(mon.OriginalTrainerName, playerName, StringComparison.Ordinal)
            ? NameRaterEligibility.Eligible : NameRaterEligibility.ForeignTrainerName;
        // OriginalTrainerGender, language, met location, and a history of trading are not checked
        // by this source script. Do not import a different game's ownership predicate here.
    }

    public static int SpeciesOrEgg(PartyPokemon mon) => mon.IsEgg ? EggSpecies : mon.Pokemon.SourceSpeciesId;
    public static bool TrainerIdDiffers(PartyPokemon mon, uint playerTrainerId) => mon.Pokemon.OriginalTrainerId != playerTrainerId;
    public static bool TrainerNameDiffers(PartyPokemon mon, string playerName) => !string.Equals(mon.OriginalTrainerName, playerName, StringComparison.Ordinal);

    // Source ShouldUseChooseMonText changes the prompt, not the selectable party rows.
    public static string ChooserPromptSymbol(ImmutableArray<PartyPokemon> party) =>
        party.Count(mon => mon.Pokemon.SourceSpeciesId != 0 && (mon.Pokemon.CurrentHp != 0 || mon.IsEgg)) > 1
            ? "gText_ChoosePokemon" : "gText_ChoosePokemonCancel";

    public static string? ValidateSelection(ImmutableArray<PartyPokemon> party, int partyIndex, long? expectedInstanceId)
    {
        if (party.IsDefault || party.Length is < 1 or > PartySize) return "name_rater_party_shape";
        if (party.Any(p => p is null || p.InstanceId <= 0) || party.Select(p => p.InstanceId).Distinct().Count() != party.Length)
            return "name_rater_party_identity";
        if (partyIndex < 0 || partyIndex >= party.Length) return "name_rater_party_index";
        if (expectedInstanceId is null || party[partyIndex].InstanceId != expectedInstanceId) return "name_rater_target_mismatch";
        return null;
    }

    public static string? Begin(ImmutableArray<PartyPokemon> party, int partyIndex, long? expectedInstanceId,
        uint playerTrainerId, string playerName, out NameRaterTarget? target)
    {
        target = null;
        var error = ValidateSelection(party, partyIndex, expectedInstanceId);
        if (error is not null) return error;
        var mon = party[partyIndex];
        error = Eligibility(mon, playerTrainerId, playerName) switch
        {
            NameRaterEligibility.Egg => "name_rater_egg",
            NameRaterEligibility.ForeignTrainerId => "name_rater_foreign_id",
            NameRaterEligibility.ForeignTrainerName => "name_rater_foreign_name",
            _ => null
        };
        if (error is not null) return error;
        if (!PokemonNicknameRules.IsValidCustomNickname(mon.CustomNickname)) return "name_rater_original_name_unsupported";
        target = new(partyIndex, mon.InstanceId, mon.CustomNickname);
        return null;
    }

    public static NameRaterRenameResult Rename(ImmutableArray<PartyPokemon> party, NameRaterTarget? target,
        uint playerTrainerId, string playerName, string? input, string alphabet)
    {
        NameRaterRenameResult Reject(string error) => new(party, error);
        if (target is null) return Reject("name_rater_not_pending");
        // The supported alphabet is already authenticated in CampaignPokemonContent. Do not
        // silently extend this boundary when an adapter supplies a different character list.
        if (alphabet is null || alphabet.Length != PokemonStateValidator.NicknameAlphabet.Length ||
            !alphabet.ToHashSet().SetEquals(PokemonStateValidator.NicknameAlphabet)) return Reject("name_rater_alphabet_unsupported");
        if (input is null) return Reject("nickname_text_required");
        var error = PokemonNicknameRules.Validate(input, alphabet);
        if (error is not null) return Reject(error);
        error = Begin(party, target.PartyIndex, target.InstanceId, playerTrainerId, playerName, out var current);
        if (error is not null) return Reject(error);
        if (!string.Equals(target.OriginalCustomNickname, current!.OriginalCustomNickname, StringComparison.Ordinal)) return Reject("name_rater_original_name_changed");
        var name = PokemonNicknameRules.Apply(target.OriginalCustomNickname, input, alphabet);
        bool changed = !string.Equals(name, target.OriginalCustomNickname, StringComparison.Ordinal);
        // Blank input preserves nullable intent; explicitly typing a default species name
        // creates a custom name. Application owns the separate source display-buffer comparison.
        var updated = changed ? party.SetItem(target.PartyIndex, party[target.PartyIndex] with { CustomNickname = name }) : party;
        return new(updated, null, target.OriginalCustomNickname, name, changed);
    }
}
