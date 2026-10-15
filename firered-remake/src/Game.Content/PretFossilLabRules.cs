using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record FossilLabShopDocument(string SourceCommit, string Profile, int PreviousItemCount, int ItemCount,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<EarlyShopStock> ShopsAdded);

/// <summary>Authenticated three-fossil catalogs and level-five creation evidence over the exact Dojo resources.</summary>
public static class PretFossilLabRules
{

    private static T Read<T>(string root,string file, SharedMachineCatalog? shared = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/fossil-lab-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        })) ?? throw new InvalidDataException("fossil_lab_source_missing");
    }

    public static JsonElement LoadRules(string root, SharedMachineCatalog? shared = null) => Read<JsonElement>(root,"rules.json", shared);

    public static FossilLabShopDocument LoadShopDocument(string root, SharedMachineCatalog? shared = null)
    {
        var document = Read<FossilLabShopDocument>(root,"shop.json", shared);
        if (            document.Additions.IsDefault || document.Additions.Length != 1 || document.Additions[0].Id != ItemIds.OldAmberId ||
            document.ShopsAdded.IsDefault || !document.ShopsAdded.IsEmpty)
            throw new InvalidDataException("fossil_lab_shop_scope");
        return document;
    }

}
