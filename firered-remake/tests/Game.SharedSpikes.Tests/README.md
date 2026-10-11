# Shared FireRed Spikes

Bounded canonical four-slot support for the real Spikes move and actual voluntary, Baton Pass, Roar/Whirlwind and end-turn KO entries. Existing singles call the same move and entry recipe through their historical storage/event adapter. Initial entry rejects preloaded hazards: FireRed clears its side timers before first send-out and does not run the switch-in hazard script then.

`bash tests/Game.SharedSpikes.Tests/run.sh CONTENT_ROOT PINNED_SOURCE NEW_OUTPUT`

The runner builds only focused Domain/Content test projects with the existing .NET 10 toolchain, local-only NuGet configuration and `/tmp/firered-remake-build.lock` held only during each build. It extracts the original C from `037335f4c725d7c9aecdac87066f2002b4bd7e14`; no reference-repository script is executed. It rejects missing/empty evidence and compares unchanged singles hashes to a separately compiled fixed `e14578e` source snapshot. No full solution, Godot or GUI build is involved.

## Source contract

- `data/battle_scripts_1.s:1540` and `battle_script_commands.c:8110`: update the opposing side before attack-string/PP commands. Three layers cap; failed fourth use still costs one PP and suppresses Pressure. Successful Spikes counts both opposing Pressure slots, including source-retained zero-HP/absent bodies, never an ally. No accuracy or hazard RNG exists. The selected target remains the original resolved position for event/history purposes; the layer side comes from the actor's side.
- `battle_script_commands.c:4961`: damage is integer `max(1, maxHP / ((5-layers)*2))`, thus 1/8, 1/6, 1/4. Either effective Flying type or effective Levitate is immune. FireRed has no Rooted override. The shared entry command bypasses Substitute/Endure/Focus Band and never creates a Counter/Mirror Coat damage receipt.
- `data/battle_scripts_1.s:3341` and `battle_script_commands.c:2833`: HP/faint processing precedes Truant and switch-in abilities. The side latch lasts one synchronous switch-in script; each next real entrant can be damaged, including another same-side slot or the next member of a KO chain.
- `battle_main.c:2240,2856`: initial entry clears hazards and uses the initial ability phase, distinct from actual switch-in effects.

## Owner and continuation

The existing identity owner binds the exact party member and cancels the departing occupant's queued action. The entry recipe mutates permanent HP, emits passive damage, clears faint state and records the new occupant's defeat token. Incoming Trace is evaluated after hazard damage. A hazard KO never activates its own Drought/Drizzle/Intimidate/Trace/Truant.

Only the user-approved doubles KO replacement timing differs from original immediate replacement: voluntary and move-driven switches still happen inside their real action; an entrant that faints waits for the same end-turn KO boundary as other defeated occupants. Opponent EXP precedes further entry or terminal victory and is settled once for that member. A KO during end-turn replacement resumes the same saved residual cursor, so poison, weather and timers do not run again. Exhausted sides finish the battle rather than requesting impossible reserves.

The reusable source side state contains only layers, not an invented remembered hazard setter. Event attribution uses the existing passive-damage convention. Unrelated unsupported entry held items, Rooted/Substitute doubles bodies, transformations, status/lock phases and malformed state retain their admission errors. Rapid Spin's doubles damage/clear lifecycle is outside this slice; historical singles clearing remains unchanged.

## Evidence

The C runner verifies pinned file and extracted-block hashes and compiles actual source blocks. It runs 7,221,510 factored scalar/control-flow vectors and returns 1,296 compact samples. These are not full ARM/ROM battle executions. C# directly checks the sampled layer arithmetic and shared PP command against those outputs, then exercises real turns/entry/reward chains. The maintained singles comparison includes hazard layers, Pressure cap, KO chains, Baton/forced/voluntary transitions and Rooted/Substitute/Flying/Levitate combinations.

See the generated `result.json`, `singles-current.json`, `singles-baseline.json`, `source-summary.json` and logs for actual counts and tool versions. Initial development failures remain separate from final evidence; no golden was regenerated to fit the implementation.

The normal runner compares the current 33 full-state/event/RNG hashes with `../Game.SharedSpikesParity.Tests/legacy-e14578e.json` (SHA-256 `fdfca111be01a38f08f3869739e0d1115bd3a576af84c1e3b7e82533d96fd372`). That baseline was independently compiled from fixed e14578e during initial implementation; routine regression does not rebuild the historical tree.
