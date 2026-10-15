# Original import inputs

Import tools read original FireRed files from `content/raw/pokefirered/037335f4/`, preserving upstream relative paths at revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`. The running game does not read this tree or import recipes. It loads converted gameplay data and assets from `content/generated`, plus the Godot project font and authored effects.

Three local trainer palette variants remain under `content/raw/local/forest-trainer-art/`. Their bytes differ from upstream and are retained as genuine conversion inputs. Original inputs remain ordinary files, without a hash-addressed store, reconstruction links, or package-path fallback.

Actual import recipes live under `scripts/recipes/config` and the relevant import tools. Redundant fetch receipts and generated source-lock inventories have been removed. Source checkout access is needed only by tools that fetch or regenerate original resources.

Story programs, text/control codes, declarations used by game rules, map-number constants and field tile attributes are compiled before startup. See [generated-only runtime](generated-only-runtime.md), [map format](runtime-map-data.md), and [render atlases](map-render-atlases.md).
