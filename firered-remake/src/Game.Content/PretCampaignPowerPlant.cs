using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretPowerPlantRegion PowerPlantRegion { get; }
    public bool PowerPlantEnabled => PowerPlantRegion is not null;
    public PowerPlantOwnedPokemonCatalog PowerPlantOwnership { get; }
    public PowerPlantOpponentPokemonCatalog PowerPlantOpponents { get; }
    public PowerPlantMachineCatalog PowerPlantMachines { get; }
    public PowerPlantShopCatalog PowerPlantShop { get; }

    private static void AppendPowerPlantSources(PretPowerPlantRegion region,
        List<PretSourceScriptFile> sources, List<string> texts, Dictionary<string,int> constants, List<string> hashes)
    {
        var previous=sources.ToDictionary(s=>s.Path,StringComparer.Ordinal);
        foreach(var file in region.Scripts)
        {
            if(previous.TryGetValue(file.Path,out var existing))
            {
                if(existing.SourceRef.Sha256!=file.SourceRef.Sha256||existing.Text!=file.Text)
                    throw new InvalidDataException("power_plant_source_conflict:"+file.Path);
            }
            else { previous.Add(file.Path,file); sources.Add(file); }
            if(!texts.Contains(file.Text,StringComparer.Ordinal))texts.Add(file.Text);
        }
        foreach(var text in region.TextSources.Values)
            if(!texts.Contains(text,StringComparer.Ordinal))texts.Add(text);
        MergeRegionalServiceConstants(constants,region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
    }
}
