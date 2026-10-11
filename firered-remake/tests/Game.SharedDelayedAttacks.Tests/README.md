# Delayed move focused verification

Run from this archive with the authenticated product checkout and pinned pret tree:

```sh
bash tests/Game.SharedDelayedAttacks.Tests/run.sh CONTENT_ROOT PRET_ROOT NEW_OUTPUT
```

The runner uses CONTENT_ROOT/scripts/env.sh, the official .NET 10 toolchain,
local-only NuGet configuration, its own archive bin/obj, and the shared build lock.
It fails on empty results, absent source inputs, source drift, malformed JSON,
missing full before serialization, fewer than 13 groups, or missing source vectors.
Logs and structured JSON use separate files. It performs no GUI or campaign run.

Before production edits, the isolated archive of
294eb6c9c7d4b41d8733acc26cbe89a74d2325d7 compiled the twelve original singles
sequences into immutable SHA-256/byte-count records. Its frozen Domain DLL is
fc1a72b085e62ed59d42a575e271942bc711c3912d1a222c98ae3bc94ef93499 and Content DLL is
abb5c1884f889820290ab2043e7bc62c2d7e01e96a2ccb77f4c2edf19d573966.
The complete serialized payloads were recovered from those exact frozen DLLs
and verified against the already-frozen hashes. They are checked on every run.

The separate four-entry Plus/Minus before corpus also comes from those frozen
DLLs. Two special Future Sight cases have the documented source correction;
Doom Desire is unchanged. Comparison allows only the stored future damage value
and requires every other serialized state/event/RNG value to remain identical.
The source C executable checks the corrected damage independently.

The source proof reuses the explicit dependency adapter in
Game.CommonAbilityAdapters.Tests/source-prefix.h and source-main.c; their hashes
and the unchanged CalculateBaseDamage hash are recorded in source-vectors.json.
This adapter is a numerical function boundary, not a synthetic battle or scheduler.
Runtime tests use actual four-slot frames and existing public action APIs.

See docs/shared-delayed-heal-attack.md for scope and source anchors.
