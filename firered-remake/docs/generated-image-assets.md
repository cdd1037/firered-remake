# Generated image representation

Generated artwork with an existing PNG companion now stores only the PNG file. Image metadata keeps one `texturePath`; geometry, frame selection, palettes, placement and source references retain their original values. The image importers emit that representation directly.

`Game.Content.PngRgba8` is the shared managed decoder for generated, non-interlaced RGBA8 PNGs. It reads multiple IDAT chunks and all five PNG scanline filters, checks image dimensions when supplied, and rejects malformed headers, unsupported formats and incomplete or excess pixel streams. Existing `ReadRgba` methods still return RGBA8 pixels in memory. They decode the PNG; callers do not choose between two files or use a fallback.

The conversion removed 1,895 redundant `.rgba` files totaling 28,457,664 bytes. A bounded C# check decoded every retained PNG and compared all pixels against the original raw companion before deletion: all 1,895 matched. The check also rejected 1,895 deliberately mismatched dimensions and invalid PNG headers. A separate backup comparison confirmed that all 1,895 PNG files were byte-for-byte unchanged and 2,814 image metadata records retained dimensions, frame data and placement.

Six generated raw-only images remain in `itemfinder-source` and `event-ticket-ui`; they had no PNG companion. A further 160 inline `rgbaBase64` payloads in 31 JSON documents were compared against their existing PNGs, with exact pixel and dimension matches, then removed (566,624 base64 characters). Their existing `pngPath` values remain unchanged. Pure JSON parsers validate icon metadata; image readers receive the PNG bytes from the declared package and decode them with the same managed decoder.

Raw-only field effects under `src/Game.Godot/Assets/Reference` and original source assets remain unchanged. No gameplay, save or audio acceptance is implied by these checks.
