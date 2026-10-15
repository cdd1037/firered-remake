using Game.Application;
using Godot;

public partial class CampaignView
{
    private SaveOperationResult AttemptPlayerSave()
    {
        SaveOperationResult result;
        try
        {
            if (_quitRequested || _busy || _tracks.Any(t => t.Time < t.Duration))
                return new(SaveOperation.Save, SaveOperationStatus.Blocked, DiagnosticDetail: "presentation_active");
            string? blocked = _session.ValidatePlayerSave(_session.State);
            if (blocked is not null)
                return new(SaveOperation.Save, SaveOperationStatus.Blocked, DiagnosticDetail: blocked);
            if (RequestEntrySaveOverwrite())
                return new(SaveOperation.Save, SaveOperationStatus.OverwriteRequired);
            result = SaveCampaignWithTrainerTowerClock(PlayerSavePath);
        }
        catch (Exception error) when (SaveOperationResult.IsExpectedFailure(error))
        {
            // Observation and clock preparation happen before the storage transaction.
            return SaveOperationResult.Failure(SaveOperation.Save, error);
        }
        if (result.Succeeded) EntrySaveSucceeded();
        return result;
    }

    private SaveOperationResult SaveCampaignWithTrainerTowerClock(string path)
    {
        if (!CommitPlayTime(true))
            return new(SaveOperation.Save, SaveOperationStatus.Blocked, DiagnosticDetail: "play_time_not_committed");
        CommitTrainerTowerTime(true);
        ulong started = Time.GetTicksUsec();
        try
        {
            return PlayerSaveOperations.Save(EntrySaveStorage(), path, _session.State, _session.Revision,
                _session.RulesVersion, _catalog.ContentVersion, _session.ValidatePlayerSave);
        }
        finally { _trainerTowerFrames.ExcludeSaveWrite((Time.GetTicksUsec() - started) / 1_000_000d); }
    }

    // Continue and F9 share the same file/backup validation and error classification.
    private SaveLoadResult<CampaignState> ReadPlayerSave() => PlayerSaveOperations.Load(() =>
        _entrySource is null
            ? new SaveService(new LocalSaveStorage()).Load<CampaignState>(PlayerSavePath,
                _session.RulesVersion, _catalog.ContentVersion, _session.ValidatePlayerSave)
            : LoadEntryProtectedSave());

    private SaveOperationResult AttemptPlayerLoad()
    {
        if (_quitRequested || _busy || _tracks.Any(t => t.Time < t.Duration) ||
            _session.ValidatePlayerSave(_session.State) is not null)
            return new(SaveOperation.Load, SaveOperationStatus.Blocked, DiagnosticDetail: "current_event_or_presentation_active");
        var attempt = ReadPlayerSave();
        if (!attempt.Result.Succeeded) return attempt.Result;
        var saved = attempt.Saved!;
        _session = CampaignSession.RestoreCurrent(_catalog, saved.State, saved.Generation);
        ResetPlayTimeHost(); _trainerTowerFrames.Reset(); _entryOverwritePending = false;
        _sequence = saved.Generation; _lastFanfareSequence = 0; _fanfareRemaining = 0;
        _blockedAutomaticWait = null; _lastCommandFault = false; _waitId = null; _waitKind = null;
        ResetSourceAudio(); LoadMap(saved.State.World.Player.MapId); Synchronize(null, saved.State);
        return attempt.Result;
    }

    private void ShowPlayerSaveResult(SaveOperationResult result)
    {
        if (result.Status == SaveOperationStatus.OverwriteRequired) return;
        string message = CampaignSaveFeedback.Message(result, _session.State.PlayerName);
        _status = message; _held = null; _repeat = 0;
        if (FieldMenuOpen) _fieldMenuNotice = message;
        else if (_session.State.Battle is not null) _battleNotice = message;
        else if (PresentedWildBattle is not null) _wildNotice = message;
        else if (DoubleBattleVisible) _doubleNotice = message;
        else if (_session.State.ItemMenu is not null) _itemNotice = message;
        else if (_session.State.Shop is not null) _shopNotice = message;
        else if (_session.State.Pc is not null) _pcNotice = message;
        else _regionalServiceNotice = message;
        string kind = result.Operation == SaveOperation.Save
            ? result.Succeeded ? "saved" : "saveRejected"
            : result.Succeeded ? "loaded" : "loadRejected";
        Write(kind, _diagnostics.Payload(
            new { result.Status, result.Files, result.FromBackup, error = result.DiagnosticDetail, _session.Revision },
            () => new { result.Status, result.Files, result.FromBackup, error = result.DiagnosticDetail,
                _session.Revision, hash = CanonicalJson.Hash(_session.State) }));
        QueueRedraw();
    }
}
