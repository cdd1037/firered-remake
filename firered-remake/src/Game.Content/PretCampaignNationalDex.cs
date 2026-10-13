using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string NationalDexContentProfile = "firered-national-dex-postgame-v1";
    public PretNationalDexSource? NationalDexSource { get; }
    public bool NationalDexEnabled => NationalDexSource is not null;

    public CampaignNationalDexProjection EvaluateNationalDex(IEnumerable<int> seenSpecies, IEnumerable<int> caughtSpecies)
    {
        if (!NationalDexEnabled) throw new InvalidDataException("national_dex_not_enabled");
        var result = OakRating!.EvaluateSpeciesIds(seenSpecies, caughtSpecies, PokedexMenu!,
            new(HasPokedex: true, NationalFlag: true, NationalMagic: 0xB9, NationalDexVariable: 0x6258));
        return new(result.National!.Seen, result.National.Caught, result.NationalComplete!.Value);
    }

    private static void AppendNationalDexSources(PretNationalDexSource source, List<PretSourceScriptFile> sources,
        List<string> texts, Dictionary<string, int> constants, List<string> hashes)
    {
        var previous = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in source.Scripts)
        {
            if (previous.TryGetValue(file.Path, out var old))
            {
                if (old.SourceRef.Sha256 != file.SourceRef.Sha256 || old.Text != file.Text)
                    throw new InvalidDataException("national_dex_source_conflict: " + file.Path);
            }
            else { previous.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in source.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, source.Constants);
        hashes.AddRange(source.AdditionalSourceHashes);
        hashes.Add("national-dex-campaign:" + NationalDexContentProfile);
    }
}
