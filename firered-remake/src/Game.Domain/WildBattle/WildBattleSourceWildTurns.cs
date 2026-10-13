using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.Randomness;

namespace Game.Domain.WildBattle;

/// <summary>Read-only encounter provenance projection for the presentation boundary.</summary>
public sealed record WildBattleSourceWildState
{
    public string RulesVersion { get; init; } = WildBattleRules.SourceWildRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CelioRoamerBattleOrigin? CelioRoamer { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public VictoryRoadWildOrigin? VictoryRoad { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SixIslandWildOrigin? SixIsland { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public FiveIslandWildOrigin? FiveIsland { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public FourIslandWildOrigin? FourIsland { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SeviiRubyWildOrigin? SeviiRuby { get; init; }
}
