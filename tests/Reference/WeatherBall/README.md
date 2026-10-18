# Weather Ball native oracle

Generate explicitly, after integrating the renderer and importer:

```sh
python tests/Reference/WeatherBall/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/weather-ball-source-vectors.json
```

The generator extracts the pinned Up, Up_Step, Down, GetWeather and original linear translation functions. It uses the existing extracted native sprite frame/affine scheduler and source frame/affine tables, with O0/O2 parity. No original repository script is executed. The source-input hashes belong only to this offline oracle.

48 complete callback lives cover all six templates, both sides, all canonical layouts, ignored retained operands, signed position/accumulator wrapping, one-frame duration, and the final callback-install-before-destroy boundary. Another 256 rows execute every original weather flag combination and assert retained argument words. Coordinate fixture transport and host callback-pointer tokens are explicit seams; BIOS matrix conversion reuses the existing shared tests. The native immediate-END dummy frame used by hail has no displayed motion or callback effect; its unused END bit is recorded but not compared to the generated static tile representation.

Generated JSON is local ignored evidence. Ordinary Presentation tests consume it without invoking a compiler and fail clearly if missing. Renderer tests additionally exercise all five full roots, layout/visibility combinations, actual indexed composition, cancellation at every frame, missing versus known-zero weather, malformed recipes/widths, and shared mixed-rock capacity rejection. No gameplay random state participates.
