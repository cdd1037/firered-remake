using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record HallOfFamePcSourceInput(string Path, int ByteLength, string Sha256);
public sealed record HallOfFamePcPresentation(string SourceCommit, string SourceTree, string Profile, int MaximumTeams,
    int MaximumHallNumber, int MiscellaneousHelpContext, ImmutableArray<HallOfFamePcPosition> FullTeamPositions,
    ImmutableArray<HallOfFamePcPosition> HalfTeamPositions, ImmutableDictionary<string, string> Text,
    ImmutableArray<HallOfFamePcSourceInput> Inputs);

/// <summary>Small authenticated PC-view contract. Existing League artwork stays owned by its existing package.</summary>
public static class PretHallOfFamePcPresentation
{
    public const string PresentationSha256 = "f3cbc6198aa5325028cd7ae750f34fa17a695419f3a88f964072ec89c56b0310";
    public static HallOfFamePcPresentation Load(string root) => FromAuthenticatedBytes(
        File.ReadAllBytes(Path.Combine(root, "content/generated/hall-pc/presentation.json")));

    public static HallOfFamePcPresentation FromAuthenticatedBytes(byte[] bytes)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != PresentationSha256)
            throw new InvalidDataException("hall_pc_presentation_bytes");
        var result = JsonSerializer.Deserialize<HallOfFamePcPresentation>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("hall_pc_presentation_missing");
        if (result.SourceCommit != HallOfFamePcRules.SourceCommit || result.SourceTree != "7515cd4067ea55f8cdbc73624478afd2736c58b9" ||
            result.Profile != "firered-hall-pc-readonly-v1" || result.MaximumTeams != HallOfFamePcRules.MaximumTeams || result.MaximumHallNumber != HallOfFamePcRules.MaximumHallNumber || result.MiscellaneousHelpContext != 31 ||
            !result.FullTeamPositions.SequenceEqual(HallOfFamePcRules.FullTeamPositions) || !result.HalfTeamPositions.SequenceEqual(HallOfFamePcRules.HalfTeamPositions) ||
            result.Text.Count != 8 || result.Inputs.Length != 6) throw new InvalidDataException("hall_pc_presentation_contract");
        return result;
    }
}
