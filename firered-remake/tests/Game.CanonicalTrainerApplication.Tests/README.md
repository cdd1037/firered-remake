# Current trainer Application contract

This bounded current test requires authenticated game content and two retained proof inputs; they are intentionally excluded from the pure-code export:

- `evidence/current-composition-20261013/conversion-run01/current.json`: the strict current conversion of the actual Celio `bill-fact.json` milestone, generation 47819. Recreate it with the documented `Game.Tooling convert-current-checkpoint` command using `tests/Game.CelioCampaign.Tests/evidence/integrated-run01/bill-fact.json` from the full resource/checkpoint package. The original remains unchanged.
- `tests/Game.FiveIslandCampaign.Tests/evidence/recovery-20261013-run02/before-gideon.json`: the actual selected generation-47357 Five Island milestone retained in the stage11 package. The test validates it with current semantics and explicitly re-encodes one new current fixture. This is test preparation, not an old production runtime.

The identities and executed commands are recorded in `evidence/canonical-trainer-application/proof.json`. Missing inputs are prerequisites to restore from the full backup or reproduce through their documented producer route; do not substitute an unrelated save or inject a victory receipt.

Build this project with the configured official toolchain and locked restore, then execute its DLL with `RESOURCE_ROOT OUTPUT_JSON`. Create the output directory first. The four cases use explicit controlled source-adjacent placement, health/EXP preparation and actual public battle commands. They cover entry, shared rewards, Shift, reserve Revive, KO replacement, learning, Sapphire source callback and strict current Continue. No complete historical journey or native rendering is claimed.
