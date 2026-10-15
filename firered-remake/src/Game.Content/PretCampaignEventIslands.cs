using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretEventIslandRegion EventIslandRegion { get; }
    public bool EventIslandsEnabled => EventIslandRegion is not null;
    public EventIslandFieldData EventIslandField => EventIslandRegion.Field;
    public EventIslandOwnedPokemonCatalog EventIslandOwnership { get; }
    public EventIslandOpponentPokemonCatalog EventIslandOpponents { get; }
    public EventIslandMachineCatalog EventIslandMachines { get; }
    public EventIslandShopCatalog EventIslandShop { get; }

    private static void AppendEventIslandSources(PretEventIslandRegion region,List<PretSourceScriptFile> sources,List<string> texts,Dictionary<string, int> constants)
    {
        var prior = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in region.Scripts)
        {
            if (prior.TryGetValue(file.Path, out var old))
            {
                if (old.Text != file.Text)
                    throw new InvalidDataException("event_island_source_conflict:" + file.Path);
            }
            else { prior.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
    }
}
