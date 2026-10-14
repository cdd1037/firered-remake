using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideStorageRelease(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.Pc is not {} pc || pc.View is not (CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move) ||
            state.Storage is not {} storage) return No("storage_release_context");
        if (ValidateStorageMoveOwnership(state) is {} invalid) return No(invalid);

        if (pc.ReleaseTarget is {} pending)
        {
            if (command.Kind == "pc_back" || command.Kind == "storage_release_prompt" && command.Choice is 1 or 127)
                return new(CommandStatus.Accepted, state with { Pc = pc with { ReleaseTarget = null } });
            if (command.Kind != "storage_release_prompt") return No("storage_release_prompt_active");
            if (command.Choice != 0) return No("storage_release_choice_required");
            if (command.StorageLocation != pending.Location || (pending.Location is null
                ? command.HeldInstanceId != pending.InstanceId || command.TargetInstanceId is not null
                : command.TargetInstanceId != pending.InstanceId || command.HeldInstanceId is not null))
                return No("storage_release_target_identity");
            var released = pending.Location is {} selected
                ? PokemonStorageReleaseRules.ReleaseSelected(_content.PokemonContent, state.Party, storage, pc.MoveCursor, selected, pending.InstanceId)
                : PokemonStorageReleaseRules.ReleaseHeld(_content.PokemonContent, state.Party, storage, pc.MoveCursor, pending.InstanceId);
            if (!released.Succeeded) return No(released.Diagnostic!.Code);
            var next = state with { Party = released.Party, Storage = released.Storage,
                Pc = pc with { MoveCursor = released.Cursor, ReleaseTarget = null },
                ReleasedPokemon = (state.ReleasedPokemon ?? []).Add(released.ReleasedPokemon!) };
            if (ValidateStorageMoveOwnership(next) is {} error) return No(error);
            return new(CommandStatus.Accepted, next);
        }

        if (command.Kind != "storage_release") return No("storage_release_prompt_missing");
        PokemonStorageReleaseResult check;
        CampaignStorageReleaseTarget target;
        if (pc.MoveCursor is not null)
        {
            if (pc.View != CampaignPcView.Move || command.StorageLocation is not null || command.TargetInstanceId is not null)
                return No("storage_release_target_identity");
            if (command.HeldInstanceId is not {} held) return No("storage_move_held_identity");
            check = PokemonStorageReleaseRules.CheckHeld(_content.PokemonContent, state.Party, storage, pc.MoveCursor, held);
            target = new(null, held);
        }
        else
        {
            if (command.StorageLocation is not {} location) return No("storage_move_location_missing");
            if (pc.View == CampaignPcView.Deposit && !location.IsParty || pc.View == CampaignPcView.Withdraw && location.IsParty)
                return No("storage_release_context");
            if (command.TargetInstanceId is not {} expected || command.HeldInstanceId is not null)
                return No("storage_move_target_identity");
            check = PokemonStorageReleaseRules.CheckSelected(_content.PokemonContent, state.Party, storage, pc.MoveCursor, location, expected);
            target = new(location, expected);
        }
        return check.Succeeded ? new(CommandStatus.Accepted, state with { Pc = pc with { ReleaseTarget = target } })
            : No(check.Diagnostic!.Code);
    }

    // Release is retirement, never a new acquisition. Keep exactly the final object, including
    // its departing held item. The existing capture/gift/trade/evolution/Hall validators continue
    // to resolve the identity through HistoricalPokemon after this structural validation.
    private string? ValidateReleasedPokemon(CampaignState state)
    {
        if (state.ReleasedPokemon is not {} released) return null;
        if (released.IsDefault || released.IsEmpty) return "save_release_history_invalid";
        var occupied = OwnedPokemon(state).Select(p => p.InstanceId).ToHashSet();
        foreach (var trade in state.NpcTrades ?? [])
            if (trade?.Outgoing is {} outgoing) occupied.Add(outgoing.InstanceId);
        foreach (var pokemon in released)
        {
            if (PokemonStateValidator.ValidateParty(_content.PokemonContent, pokemon) is not null ||
                pokemon.InstanceId >= state.NextPokemonId || pokemon.IsEgg || pokemon.Mail != SourceMailState.NoMail ||
                SourceMailRules.IsMailItem(pokemon.HeldItem)) return "save_release_pokemon_invalid";
            if (!occupied.Add(pokemon.InstanceId)) return "save_release_ownership_duplicate";
            if (!state.CaughtSpecies.Contains(pokemon.Pokemon.SourceSpeciesId) ||
                !HasSupportedCampaignOriginalTrainer(state, pokemon) || pokemon.Language != 2 || pokemon.MetGame != 4 ||
                ((!_content.CaptureEnabled || pokemon.InstanceId == 1) &&
                    (pokemon.MetLocation != "MAPSEC_PALLET_TOWN" || pokemon.MetLevel != 5)) ||
                pokemon.Ball != "ITEM_POKE_BALL" && !(pokemon.InstanceId != 1 &&
                    (AdditionalCaptureBallSupported(pokemon.Ball) || pokemon.Ball == "ITEM_SAFARI_BALL" && _content.SafariEnabled)) ||
                !OwnedHeldItemSupported(state, pokemon)) return "save_release_origin_invalid";
        }
        return null;
    }
}
