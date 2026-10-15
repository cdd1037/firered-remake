using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string NationalDexContentProfile = "firered-national-dex-postgame-v1";
    public bool NationalDexEnabled { get; }

    public CampaignNationalDexProjection EvaluateNationalDex(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)
    {
        if (!NationalDexEnabled) throw new InvalidDataException("national_dex_not_enabled");
        var result = OakRating!.EvaluateSpeciesIds(seenSpecies, caughtSpecies, PokedexMenu!,
            new(HasPokedex: true, NationalFlag: true, NationalMagic: 0xB9, NationalDexVariable: 0x6258));
        return new(result.National!.Seen, result.National.Caught, result.NationalComplete!.Value);
    }


}
