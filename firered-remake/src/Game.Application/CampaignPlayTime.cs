using System.Text.Json.Serialization;
using Game.Domain.Time;

namespace Game.Application;

/// <summary>Observed original logical ticks. SinceCheckpoint discloses an unknown
/// earlier duration; it never turns that missing history into a zero-time claim.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CampaignPlayTime(
    [property: JsonRequired] ushort Hours,
    [property: JsonRequired] byte Minutes,
    [property: JsonRequired] byte Seconds,
    [property: JsonRequired] byte VBlanks,
    [property: JsonRequired] bool SinceCheckpoint)
{
    public static CampaignPlayTime NewGame { get; } = new(0, 0, 0, 0, false);
    internal SourcePlayTime SourceTime => new(Hours, Minutes, Seconds, VBlanks);
}

public sealed partial class CampaignSession
{
    private const string FirstHallOfFameTimeStat = "GAME_STAT_FIRST_HOF_PLAY_TIME";

    private static string? ValidatePlayTime(CampaignState state) =>
        state.PlayTime is null || !SourcePlayTimeRules.IsCanonicalSaveTime(state.PlayTime.SourceTime)
            ? "save_play_time_invalid" : null;

    private static Decision<CampaignState> DecidePlayTime(CampaignState state, CampaignCommand command)
    {
        if (command.Choice is not (>= 1 and <= 60) ||
            command != new CampaignCommand("play_time_tick", Choice: command.Choice))
            return new(CommandStatus.Rejected, state, "play_time_tick_invalid");
        if (ValidatePlayTime(state) is not null)
            return new(CommandStatus.Rejected, state, "play_time_invalid");
        var counter = SourcePlayTimeRules.Start(new(state.PlayTime.SourceTime, SourcePlayTimeCounterMode.Stopped));
        for (int i = 0; i < command.Choice; i++) counter = SourcePlayTimeRules.Update(counter);
        var time = counter.Time;
        return new(CommandStatus.Accepted, state with
        {
            PlayTime = state.PlayTime with { Hours = time.Hours, Minutes = time.Minutes, Seconds = time.Seconds, VBlanks = time.VBlanks }
        });
    }

    // SetGameStat accepts the source u32 packed clock. Unlike ordinary incremented
    // counters, hours above 255 legitimately occupy the high byte.
    private static bool ValidFirstHallOfFameTime(int packed) => packed >= 0 &&
        SourcePlayTimeRules.IsCanonicalSaveTime(new((ushort)((uint)packed >> 16), (byte)(packed >> 8), (byte)packed, 0));

    private static int? RecordFirstHallOfFamePlayTime(CampaignState state)
    {
        // An explicitly unknown earlier Hall entry stays unknown. A known source
        // zero is different: its original zero guard permits a later write.
        if (state.HallOfFame is { FirstHallOfFamePlayTime: null }) return null;
        return checked((int)SourcePlayTimeRules.RecordFirstHallOfFame(
            (uint)(state.GameStats?.GetValueOrDefault(FirstHallOfFameTimeStat) ?? 0), state.PlayTime.SourceTime));
    }

    private static bool ValidHallOfFamePlayTime(CampaignState state, CampaignHallOfFameArchive archive)
    {
        if (state.GameStats is not {} stats || !stats.TryGetValue(FirstHallOfFameTimeStat, out int packed) ||
            !ValidFirstHallOfFameTime(packed)) return false;
        return archive.FirstHallOfFamePlayTime is {} first
            ? first == packed && ValidFirstHallOfFameTime(first)
            : state.PlayTime.SinceCheckpoint && packed == 0;
    }
}
