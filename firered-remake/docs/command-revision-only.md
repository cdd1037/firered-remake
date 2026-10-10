# Revision-only command handling

The shared `CommandSession<TState,TCommand>` now owns only the current state,
its revision, fixed rules/content identities and a serialization lock. It
retains no outcome history, command IDs, fingerprints, eviction ring or
tombstones. UI and automation use the same implementation; there is no
Debug/Release split or alternate ledger policy.

The [actual-use review](command-receipt-usage-review.md) found that production
callers generate fresh trace IDs synchronously. Retrying a full envelope to
retrieve a historical accepted result was exercised only by deliberate tests.
The user approved removing that unused contract before the new archive.

## Current API contract

- `CommandId` remains a required nonblank diagnostic trace label. It is not a
  deduplication key and has no uniqueness guarantee across requests/sessions.
- `ExpectedRevision` must equal the current authoritative revision. An old
  envelope, including one submitted again after intervening commands, returns
  `Rejected / stale_revision` with the **current** state, revision and hash.
  The rule delegate does not run and current state remains unchanged.
- Reusing a trace ID with a current revision is a new request. Changed payloads
  are evaluated normally. There is no `command_id_conflict`,
  `command_retry_expired`, or `IsDuplicate` result anymore.
- Rules/content mismatches still reject before rule execution. Only accepted
  immutable decisions atomically replace state and increment revision. Rule
  rejections, explicit faults, exceptions and exhausted revisions cannot publish
  partially changed state.
- Concurrent requests using one revision can commit at most one transition,
  regardless of whether their IDs are identical. Other callers see a stale
  revision and must inspect current state before deciding on a new request.

This is not an exactly-once transport or lost-response recovery protocol.
Blindly changing an old request's revision to resubmit a purchase/action can
request that action again. The current UI does not perform such transport
retries. One-time rewards, trainer/capture settlement and inventory ownership
continue to use their domain state and source flags, independently of trace IDs.

Save bytes, schema, content/rules IDs and canonical state hashing are unchanged.
Only game state and generation are saved, as before. Continue constructs a fresh
session with those values. State memory may legitimately grow with game content;
the narrower claim is that this command dispatcher no longer roots past states
or accumulates command-ID history.

## Verification

`./dev test-command-retention` retains its existing CLI name to avoid breaking
runbooks, but runs the current `command-revision-no-history` suite: delayed stale
requests, reused trace IDs at current revision, concurrency, failure atomicity,
overflow, save round trips, business reward flags, and 4,096 transitions followed
by full collection. Its weak references require exactly the current full state
and no original command-ID strings to remain alive through the session.

Campaign drivers keep their deliberate retry attempts. Their assertions now
require rejection and preservation of the live state; delayed retries compare
against the later state, not a historical outcome. Fixed-SHA audits keep their
original source/expectations and are labeled historical rather than relabeled
as new-contract regressions. The final checkpoint validation matrix records
the exact current-source runs separately.
