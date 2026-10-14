# Egg factory bounded runner

Use the official environment from `scripts/env.sh`. Generate the original-source
oracle with `python3 tests/Game.EggFactory.Tests/probe_source_iv.py REFERENCE_GIT
OUTPUT_JSON`. The script reads the pinned original C function and compiles only
the bounded oracle; its generated file is not an authored source dependency.

Build this project against the current Domain/Content and run its DLL with
`PRODUCT_ROOT PRODUCT_ROOT OUTPUT_JSON RESULT_JSON`. The current Content must
include the breeding species and presentation packages. The runner uses the
current authenticated owner and does not need a historical 174-species build.
The original component result and the adapted-current result are separate
cohorts in the integration evidence.
