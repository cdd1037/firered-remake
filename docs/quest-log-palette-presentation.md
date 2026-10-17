# Quest Log field palette presentation

The recap field and final saved-location caption use the original RGB555 weighted grayscale transform. One pixel authority applies the same transform to main/connected/border map layers, actors and Surf sheets. Header, bars and caption textures remain outside that field transform, corresponding to the source UI palette reservation. Original texture pixels remain immutable.

Pinned `palette.c` implements grayscale as `(76*r + 151*g + 29*b) >> 8`. `CopyPaletteInvertedTint` restores each channel with `channel + ((tone * (gray - channel)) >> 4)`, including arithmetic-floor rounding for negative differences. Coefficient zero preserves original bytes. This is separate from the different battle grayscale command.

`overworld.c` selects `QL_TINT_BACKUP_GRAYSCALE` for the saved scene. `Task_EndQuestLog` first clears the header, then `RestoreScreenAfterPlayback` restores tones 15 through 0 during the existing 16 bar updates. Playback exposes that same phase; there is no second exit clock or save field. Skip and natural completion retain their existing input isolation and release timing.

The source restoration covers BG entries 1..223 and OBJ entries 256..511. The existing field/UI texture ownership supplies the corresponding render boundary; the black backdrop is unchanged. This slice does not implement additional weather palette-bank darkening. Source Pallet Town uses `WEATHER_SUNNY`, which does not enter `SlightlyDarkenPalsInWeather`'s rainy/snow/shade cases.

## Verification

A portable original-C extractor compares every RGB555 color at all 17 coefficients through compact per-tone SHA256 vectors. Managed tests also cover alpha preservation, original-byte restoration and the existing exact exit-frame vectors. Native recap evidence is recorded after the bounded validation window.

The full solution and subsequent coherent Godot refresh passed with zero warnings/errors. Three focused Presentation tests passed, including all 557,056 source color/tone combinations; all 165 Quest Log Application tests and 28 maintained layout checks passed. Regenerating the compact oracle reproduced it byte-for-byte. Native candidate: MVID `1bb7c47d-b5c7-46ad-a17f-8a66253a1c4a`, SHA256 `82e881c37d7041f44b9a74bf1d59c01b570bca6ba559b4a02a4c8e2fa551e468`.

A single normal-speed native recap on that exact DLL passed: historical field and saved-location caption remained grayscale while blue-gray UI bars/caption stayed colored; the continuous screenshot sequence captured partial then full source-color restoration. A Right input during recap did not change the final North-facing actor. The game closed and its DLL hash remained unchanged. Evidence is retained under `/workspace/shared/firered-quest-gray-native-20261017/REPORT.md`. Sparse screenshots do not independently establish every restoration callback; that timing is covered by the original-task and managed tests. This fixture does not establish all weather, connected-border or Surf scenes.
