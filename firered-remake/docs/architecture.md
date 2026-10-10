# Implementation boundaries

The product remains a modular monolith. Domain has no product-project references. Application and Content reference only Domain. Godot and Tooling assemble the modules; neither is a source of game-rule truth. Domain state uses immutable records and immutable arrays. Callers of the generic command session must supply immutable state and pure decisions.

A domain transition reports Accepted, Rejected or Fault. The application publishes only accepted, completed transitions. Repeated command IDs return a stored result only when the full payload/context fingerprint matches. Version mismatch, stale revision and conflicting reuse reject without invoking rules. Supported battle inputs are chosen per actor; a complete turn resolves atomically when both sides' commands are available. The synthetic harness composes the player and opponent decisions as one application commit.

Stable IDs have explicit serialized strings. Original trainer ownership and an array-based battler model are present; the current battle profile still rejects doubles and unsupported mechanics. There is no silent substitution of singles for doubles. The full scope includes 386-era data capacity, ordinary NPC trades, eggs/breeding, roamer state and post-game; this implementation does not falsely enable those systems.

Canonical JSON sorts object keys ordinally and preserves array order. Collections whose order is not semantic must be normalized before hashing. Rendering state, frame count and wall-clock time never enter domain decisions. RNG algorithms and semantic consumption are versioned; internal repeatability is not GBA frame-exact reproduction.

Saves record schema/rules/content/edition, generation, payload and SHA-256. The codec rejects unsupported versions and corruption. SaveService verifies staged data before commit and backs up only a verified primary; an invalid primary cannot overwrite a good backup. Platform adapter failures retain the prior committed state. Domain never serializes Godot nodes.

The original engineering harness is explicitly isolated under fixture IDs and versions. It is not a map/story conversion, and its toy reward flow is not the game economy. Content conversion may proceed only after its evidence gate, never by marking an unsupported source script implemented.

## Player save boundary
Player save/load actions reject while a battle is active, before any storage write. ValidatePlayerSave also rejects battle payloads during loading. Domain snapshot JSON and the generic codec still support battle states for diagnostics/replay, deliberately separate from in-game player permissions. Historical native checkpoint evidence is labeled engineering-only and not original FireRed save behavior.
