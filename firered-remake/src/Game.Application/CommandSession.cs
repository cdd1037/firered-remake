namespace Game.Application;
public enum CommandStatus { Accepted, Rejected, Fault }
public sealed record CommandEnvelope<T>(string CommandId, long ExpectedRevision, string RulesVersion, string ContentVersion, T Payload);
public sealed record CommandOutcome<T>(CommandStatus Status, long Revision, T State, string StateHash, string? Error);
public sealed record Decision<T>(CommandStatus Status, T State, string? Error = null);
/// <summary>
/// Single session, serialized commits guarded by the expected state revision.
/// Command IDs are trace labels, not persistent or session-local deduplication keys.
/// A rule fault or rejected command never publishes partial state.
/// </summary>
public sealed class CommandSession<TState, TCommand>
{
    private readonly object _gate = new();
    private readonly string _rulesVersion;
    private readonly string _contentVersion;
    private TState _state;
    private long _revision;
    public CommandSession(TState initial, string rulesVersion, string contentVersion, long revision = 0)
    { if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision)); _state = initial; _rulesVersion = rulesVersion; _contentVersion = contentVersion; _revision = revision; }
    public TState State { get { lock (_gate) return _state; } }
    public long Revision { get { lock (_gate) return _revision; } }
    public CommandOutcome<TState> Execute(CommandEnvelope<TCommand> command, Func<TState, TCommand, Decision<TState>> decide)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(command.CommandId)) return Reject("command_id_required");
            if (command.ExpectedRevision != _revision) return Reject("stale_revision");
            if (command.RulesVersion != _rulesVersion || command.ContentVersion != _contentVersion) return Reject("version_mismatch");
            if (_revision == long.MaxValue) return new(CommandStatus.Fault, _revision, _state, CanonicalJson.Hash(_state), "revision_exhausted");
            Decision<TState> result;
            try { result = decide(_state, command.Payload); }
            catch (Exception ex) { return new(CommandStatus.Fault, _revision, _state, CanonicalJson.Hash(_state), ex.GetType().Name + ": " + ex.Message); }
            if (result.Status != CommandStatus.Accepted)
                return new(result.Status, _revision, _state, CanonicalJson.Hash(_state), result.Error);
            // State must be immutable: only a completed decision can become authoritative.
            var hash = CanonicalJson.Hash(result.State);
            var nextRevision = checked(_revision + 1);
            var outcome = new CommandOutcome<TState>(CommandStatus.Accepted, nextRevision, result.State, hash, null);
            _state = result.State; _revision = nextRevision;
            return outcome;
        }
    }
    private CommandOutcome<TState> Reject(string error) => new(CommandStatus.Rejected, _revision, _state, CanonicalJson.Hash(_state), error);
}
