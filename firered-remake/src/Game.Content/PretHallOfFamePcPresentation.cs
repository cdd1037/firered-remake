using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record HallOfFamePcPresentation(int MaximumTeams, int MaximumHallNumber,
    int MiscellaneousHelpContext, ImmutableArray<HallOfFamePcPosition> FullTeamPositions,
    ImmutableArray<HallOfFamePcPosition> HalfTeamPositions, ImmutableDictionary<string, string> Text);

/// <summary>Small authenticated PC-view contract. Existing League artwork stays owned by its existing package.</summary>
public static class PretHallOfFamePcPresentation
{
    public static HallOfFamePcPresentation Load(string root) => FromAuthenticatedBytes(
        File.ReadAllBytes(Path.Combine(root, "content/generated/ui/hall-pc.json")));

    public static HallOfFamePcPresentation FromAuthenticatedBytes(byte[] bytes)
    {
        var result = JsonSerializer.Deserialize<HallOfFamePcPresentation>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("hall_pc_presentation_missing");
        if (result.MaximumTeams != HallOfFamePcRules.MaximumTeams || result.MaximumHallNumber != HallOfFamePcRules.MaximumHallNumber || result.MiscellaneousHelpContext != 31 ||
            !result.FullTeamPositions.SequenceEqual(HallOfFamePcRules.FullTeamPositions) || !result.HalfTeamPositions.SequenceEqual(HallOfFamePcRules.HalfTeamPositions) ||
            result.Text.Count != 8 ) throw new InvalidDataException("hall_pc_presentation_contract");
        return result;
    }
}
