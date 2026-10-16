using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string NationalDexContentProfile = "firered-national-dex-postgame-v1";

    public CampaignNationalDexProjection EvaluateNationalDex(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)
    {
        var result = OakRating!.EvaluateSpeciesIds(seenSpecies, caughtSpecies, PokedexMenu!,
            new(HasPokedex: true, NationalFlag: true, NationalMagic: 0xB9, NationalDexVariable: 0x6258));
        return new(result.National!.Seen, result.National.Caught, result.NationalComplete!.Value);
    }

}
