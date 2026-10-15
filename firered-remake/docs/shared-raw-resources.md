# Shared original resources

The current game and importers read original FireRed files from
`content/raw/pokefirered/037335f4/`, preserving each upstream relative path.
The source revision is `037335f4c725d7c9aecdac87066f2002b4bd7e14`.
Feature packages no longer keep their own copies of the same original file.
These are ordinary files; loading does not use a content-addressed store,
hash index, symlink reconstruction, or fallback to the former package paths.

Package-local importer manifests remain in their package directories. Three
forest-trainer palettes with different line endings remain under
`content/raw/local/forest-trainer-art/`; they were not merged with different
upstream bytes. Runtime presentation continues to use generated assets.

The migration reduced raw input storage from 17,381 files / 453,065,866 bytes
to 5,452 files / 22,437,332 bytes. This includes removing two unused saved
Git-tree listings after their importer consumers switched to existing explicit
source lists. Other upstream files with different paths remain separate even
when their bytes happen to match. Generated maps, graphics, audio, gameplay
state, and save schemas were not changed by this path migration.

Validation: the maintained `FireRed.Current.slnx` builds with zero warnings
and errors. The current composition check loads 365 maps and 189 admitted
species, starts a new game, saves and continues, executes movement, and loads
the existing generation-57604 checkpoint. All 13 checks pass, including
unchanged checkpoint payload and RNG. The resource recipes' explicit raw
paths resolve in the shared layout. This was a loading/path check, not a
historical gameplay replay or native audiovisual acceptance.

Original scripts and text are still parsed at game startup. Therefore the
shared raw directory and the relevant source recipes remain runtime inputs;
the current project is not yet a generated-only release package. Importers
use the same shared paths when regenerating assets. Original source checkout
access is needed only by importers that explicitly fetch additional source
files, not by normal startup of the supplied complete project.
