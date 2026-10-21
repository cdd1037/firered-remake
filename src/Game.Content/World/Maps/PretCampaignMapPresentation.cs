using System.Collections.Frozen;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Final map art bindings, independent of historical region package ownership.</summary>
public sealed record PretMapPresentationBinding(string OverworldCatalogPath, bool UsesLocalBorder);

public static class PretCampaignMapPresentation
{
    public static FrozenDictionary<MapId, PretMapPresentationBinding> Create(PretCampaignCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.SourceMaps.ToFrozenDictionary(pair => pair.Key, pair =>
        {
            var generated = PretRuntimeMapValidation.GeneratedRoot(pair.Value.MapPath);
            return new PretMapPresentationBinding(Path.Combine(generated, "sprites/overworld/catalog.json"), pair.Value.UsesLocalBorder);
        });
    }
}
