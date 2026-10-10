# Fixed ordinary-cry catalog promotion

The optional `NormalCryCatalog` authenticates a6188-byte external metadata file with SHA256623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e.
Generate locally from the audited phase4 inventory using `python3 tools/build_cry_catalog.py`.
The original2,565,436-byte audio resource pack is unchanged.

Pass the loaded catalog to `new Synth(resources, profile, catalog)`; `PrepareNormalCry(species)` decodes bounded samples outside rendering and `StartNormalCry(species)` starts the same dynamic source template.
Without a catalog, the old Charmander-only API behavior remains unchanged. With the catalog,386 real ordinary species are admitted; placeholders/forms/Egg/None and nonzero modes explicitly fail before stopping or ducking existing audio.
Decoded cry samples are cached per Synth, maximum3,136,467 sample bytes if every cry is prepared (array/dictionary overhead additional). The raw samples are not pre-rendered recordings. Lazy preparation can allocate on the control call; hosts should preload relevant species ahead of an audio deadline.

`catalog-selftest <pack> <catalog>` renders all386 through the public API and matches every phase4 PCM hash exactly. It also checks tampered/truncated metadata, unsupported species/mode, rejection preserving current cry, completion/clipping and total cache count/bytes.
Only centered normal parameters are covered; reverse/weak/faint/chorus/progress/dual-cry remain unsupported.
