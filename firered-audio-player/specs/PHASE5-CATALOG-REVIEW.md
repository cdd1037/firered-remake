# Independent normal-cry catalog review

2026-10-09. Scope: commit `c0e6c75a5c3ba8e5bcc741dfe57e04756e3f1041`, its authenticated metadata generator, public cry API changes, cache and regression evidence. No runtime source was edited. The separately evolving music research and Godot demo are not accepted by this catalog review.

## Result

**No blocking defect found for the fixed, optional 386-species normal-cry catalog under the documented single-owner-thread model.** Without a catalog, the old Charmander-only behavior still holds. This does not extend support to reverse/special modes, arbitrary assets, dual-cry playback, all-species fidelity acceptance or target-device audio guarantees.

Independent checks completed:

- Re-ran `catalog-selftest resources/firered.pack resources/normal-cries.bin`: **386 public renders exactly match the earlier research PCM hashes**, complete without clipping; full cache count386 and source-sample bytes3,136,467. This review run took3.87363s; the committed run's3.9260445s is a separate measurement.
- Re-ran the ordinary runtime suite: **79 tests passed**.
- Executed a temporary external harness against the current core assembly: **731 assertions passed**. It checked every metadata row, multiple tampered byte positions, wrong lengths, input-buffer mutation after loading, cache isolation/idempotence, legacy behavior, invalid-request continuation, repeated replacement and cancellation.
- Verified metadata SHA-256 `623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e` and unchanged audio-pack SHA-256 `00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d` against the on-disk inputs.
- Tested core DLL SHA-256: `a28d82b97855d13a9534cde00bb2d840a1ccd769a2ad7721a9b382d64e79f0fb`.


## Metadata admission and limits

`NormalCryCatalog` has no public constructor accepting arbitrary entries. `FromFile` checks length before allocating the6188-byte buffer; `FromBytes` checks exact length and SHA-256 before interpreting rows. The authenticated snapshot contains386 sixteen-byte records after a12-byte header. The hash covers reserved padding as well as every species/index/count/payload/guard value.

The parser additionally checks the header/count, ordinary internal ID ranges, cry indices including exclusion of unused265/268, guard flags and minimum decoder payload requirement. The fixed hash makes malformed numeric fields and duplicate rows unreachable without changing the accepted identity; these structural checks are useful secondary defenses, not a substitute for the hash.

An independent row-by-row comparison with the audited phase4 manifest confirmed all386 entries and3,136,467 cached sample bytes. Metadata input bytes are not retained: mutating the original byte array after loading did not change catalog entries. Returned entries are value records and cannot replace the private dictionary's data.

The external audio pack remains separately authenticated by `Resources.FromPack`. The public low-level `Resources(byte[])` constructor still permits custom images as it did before; the fixed-library guarantee assumes the documented authenticated loading path.

## Mutation order, caching and rendering

`StartNormalCry` rejects nonzero modes before building/preparing anything. Catalog species lookup and bounded preparation finish before `Stop(2)` or new ducking state. The independent harness started BGM plus Wailord, attempted invalid species/preparation calls and modes−1/1/2/MaxValue, then compared300 following512-frame blocks against an untouched control. PCM values, cry-playing state and ducking state remained exactly identical. No invalid request increased the prepared count.

Same-species retrigger reuses its prepared sample and replaces the old cry. Different-species retrigger adds only that new sample and retains one cry voice. Cancellation followed by rendering restores duck state. These checks supplement, rather than replace, the existing lifecycle suite.

Preparation validates the fixed tone and wave metadata and uses the original unchanged bounded decoder. Stored guards decode `SampleCount+1`; authenticated missing-guard entries decode only `SampleCount` and append the verified final value. There is no adjacent-ROM read or exception-driven repair of arbitrary malformed data. Resource spans and the decoder's one-million-sample budget remain in force.

The source arrays are held in a per-Synth dictionary keyed by wave address. Preparing all species twice in opposite orders leaves count386 and bytes3,136,467; a second Synth begins with an empty cache. Arrays remain cached for that Synth's lifetime, with no eviction API. Array/dictionary/instrument overhead is additional; cached source bytes are not a working-set estimate. Existing public mutable `Voice` diagnostic objects still expose live sample references, so authenticated input admission is not a security boundary against a caller deliberately mutating engine internals.

Every supported `StartNormalCry(species)` prepares its sample before the render path uses it. This is decoded original DPCM fed to the same live mixer and dynamic template, not a prerecorded WAV or alternative render fallback. The reference-hash file read in `CatalogVerification` belongs to the development CLI self-test, not the core runtime project. Lazy control calls can allocate; this review makes no broader zero-allocation claim.

## Integration notes, not blockers

- Adding optional parameters preserves source-level calls but changes CLR constructor/method signatures. Already-compiled hosts using the earlier two-argument constructor or parameterless `PrepareNormalCry` must be rebuilt; binary drop-in compatibility is not established.
- The small generator currently prints its output hash but does not itself assert the accepted constant before writing. A changed inventory can therefore generate an unusable file, which the runtime correctly rejects. An expected-hash assertion before replacing the output would improve the development workflow.
- The inspected in-progress Godot demo started Bulbasaur at3s and called `Stop(2)` at5s. Its audible sample normally ends around3.78s, so that schedule did not establish cancellation of an active cry. This observation was sent to the parent for the ongoing demo work; it is not a failure of the catalog API, and this review does not approve the evolving demo.
