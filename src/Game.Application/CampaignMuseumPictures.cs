using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // These specials choose fossil artwork, not a live Pokémon or a Pokédex sighting.
    // The existing picture/dialog lifecycle owns input, field locking and save blocking.
    private bool TryApplyMuseumPictureEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode != "special" || effect.Args.Length != 1 ||
            effect.Args[0] is not ("OpenMuseumFossilPic" or "CloseMuseumFossilPic")) return false;

        if (effect.Args[0] == "CloseMuseumFossilPic")
        {
            if (state.MonPicture?.AssetId is not ("aerodactyl-fossil" or "kabutops-fossil"))
                throw new InvalidDataException("fossil_picture_not_open");
            next = state with { MonPicture = null };
            return true;
        }

        int species = state.Script.Variables.GetValueOrDefault("VAR_0x8004");
        int x = state.Script.Variables.GetValueOrDefault("VAR_0x8005");
        int y = state.Script.Variables.GetValueOrDefault("VAR_0x8006");
        if (!_content.TryConstant("SPECIES_AERODACTYL", out int aerodactyl) ||
            !_content.TryConstant("SPECIES_KABUTOPS", out int kabutops))
            throw new InvalidDataException("fossil_picture_species_constants_missing");
        string asset = species == aerodactyl ? "aerodactyl-fossil" : species == kabutops ? "kabutops-fossil" :
            throw new InvalidDataException("fossil_picture_species_unsupported");
        if (state.MonPicture is not null || x is < 0 or > 20 || y is < 0 or > 10)
            throw new InvalidDataException("fossil_picture_context_invalid");
        next = state with { MonPicture = new(species, x, y) { AssetId = asset } };
        return true;
    }
}
