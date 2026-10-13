# Current field-item contract

Run this fixture-dependent console test explicitly against the current-composition generation-47819 checkpoint. The checkpoint must already use `firered-current-campaign-v1`; the test does not convert or overwrite it.

```sh
source scripts/env.sh
dotnet run --no-build --project tests/Game.CurrentFieldItems.Tests -- RESOURCE_ROOT CURRENT_CHECKPOINT OUTPUT_JSON
```

The three parameters are the reconstructed resource root, the current checkpoint file, and an output JSON file in an existing directory. The generation and original party are intentional fixture requirements. This test belongs in current builds; do not run it in a fixture-free test gate.

The public-command cases cover Repel/Super Repel/Max Repel deferred sound acknowledgement, once-only consumption, stale or repeated acknowledgements, expiry, Revive/Ether/Max Elixir effects, Rare Candy, and exact TM/HM selection/protection. Whole-state command hashes are recorded. The test also rejects a cloned current Pokemon catalog and a mismatched owned proof.

Controlled fixtures are explicit: extra consumable stacks; a level-100 version of the checkpoint Gyarados with source-calculated stats; and three PP Ups plus depletion on the checkpoint Charizard's existing Cut move. These are not naturally acquired gameplay claims. All tests retain original source ownership and verify the input checkpoint bytes remain unchanged.

The historical pre-fix characterization is separate evidence: the old current Candy command faulted on Cinnabar-only admission without changing state. This current test requires that same command to succeed and verifies level-100 nonconsumption, Charizard learning, legal Ekans evolution, owned identity, and existing PP/PP Ups.

Moon/Sun Stone cases now share this same project. They use the existing checkpoint-owned boxed Clefairy and Gloom, deposited/withdrawn through source storage rules, plus explicitly supplied stones. Controlled Everstone, nickname, HP/PP/PP-Up and level/HM variants are disclosed in the generated `.stones/fixture-provenance.json`. Separate Domain factory cases cover the four Moon Stone families and exact-level Nidoking learning; they do not claim natural acquisition. The test verifies exact owned catalog proof, original source evolution edges, one-time consumption, mandatory evolution, HM protection, strict saved lineage, and stable Restore. Generated stable saves and their trace are written beside OUTPUT_JSON in `OUTPUT_JSON.stones/`.
