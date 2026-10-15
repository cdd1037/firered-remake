using System.Globalization;
using Game.Application;
using Godot;

public partial class CampaignView
{
    private readonly CampaignPlayTimeTicks _playTimeTicks = new();
    private bool PlayTimeRunning => _session is not null && !_quitRequested && !PlayerEntryActive &&
        LeagueCompletion?.Stage != CampaignPokemonLeagueCompletionStage.ResetRequested;

    public override void _PhysicsProcess(double delta)
    {
        // delta is intentionally unused. This adapter counts one observed callback,
        // not wall time, suspension, save I/O, movement or presentation progress.
        _playTimeTicks.Observe(Engine.GetProcessFrames(), PlayTimeRunning);
    }

    private void ResetPlayTimeHost() => _playTimeTicks.Reset(Engine.GetProcessFrames());

    private bool PlayTimeDisplayNeedsFlush => PlayTimeRunning &&
        (_fieldMenuMode == "trainer-card" || LeagueCompletion?.Stage == CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame) &&
        _session.State.PlayTime.Seconds * 60 + _session.State.PlayTime.VBlanks + _playTimeTicks.PendingTicks >= 3600;

    private bool CommitPlayTime(bool flush)
    {
        if (!PlayTimeRunning) return true;
        if (_busy) return false;
        int ticks;
        while ((ticks = _playTimeTicks.PeekBatch(flush)) > 0)
        {
            long before = _session.Revision;
            var result = _session.Execute("ui:" + (_sequence++), before, new("play_time_tick", Choice: ticks));
            if (result.Status != CommandStatus.Accepted)
            {
                // Keep the pending batch intact; a clock fault cannot masquerade as
                // a completed gameplay command or silently erase observed ticks.
                _lastCommandFault = true;
                _status = "Play time could not be committed: " + result.Error;
                Write("playTimeRejected", new { ticks, result.Error, result.Revision });
                return false;
            }
            _playTimeTicks.Acknowledge(ticks);
            PreservePlayTimeRevisionGuards(before, result.Revision);
            // Do not Synchronize: this transaction changes only authoritative time.
            if (_diagnostics.Verbose) Write("playTimeClock", new { ticks, result.Revision, result.State.PlayTime });
        }
        return true;
    }

    private void PreservePlayTimeRevisionGuards(long before, long after)
    {
        static void Keep(ref long guard, long oldRevision, long newRevision) =>
            guard = CampaignPlayTimeTicks.PreserveMatchingRevision(guard, oldRevision, newRevision);
        if (_sourceMoveSession == _session) Keep(ref _sourceMoveRevision, before, after);
        Keep(ref _summaryMoveRevision, before, after);
        Keep(ref _storageCreatureRevision, before, after);
        Keep(ref _storageReleaseRevision, before, after);
        Keep(ref _storageBoxOptionsRevision, before, after);
        Keep(ref _storageCustomizationRevision, before, after);
        Keep(ref _ppBoostSelectionRevision, before, after);
        Keep(ref _vitaminSelectionRevision, before, after);
        Keep(ref _evolutionSelectionRevision, before, after);
    }

    private static string FormatPlayTime(CampaignPlayTime time) =>
        time.Hours.ToString(CultureInfo.InvariantCulture) + ":" + time.Minutes.ToString("D2", CultureInfo.InvariantCulture) +
        (time.SinceCheckpoint ? "+" : "");
    private const string CheckpointTimeNote = "+ Time since checkpoint.";
}
