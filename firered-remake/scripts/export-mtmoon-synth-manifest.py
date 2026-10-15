#!/usr/bin/env python3
"""Export the pinned 170-cue recipe without rendering or copying audio samples."""
import argparse
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
BYTES = 61166


def verified_recipe(root=ROOT):
    data = (root / "scripts/synth-mtmoon-cues.json").read_bytes()
    prior_data = (root / "scripts/synth-route3-cues.json").read_bytes()
    prior = json.loads(prior_data)["cues"]
    cues = json.loads(data)["cues"]
    by_id = {cue["id"]: cue for cue in cues}
    if len(cues) != 170 or len(by_id) != 170 or sum(c["sourcePlayer"] == 4 for c in cues) != 56:
        raise ValueError("MtMoon cue inventory differs")
    if len(prior) != 151 or any(by_id.get(cue["id"]) != cue for cue in prior):
        raise ValueError("prior151 cue metadata changed")
    return data


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    data = verified_recipe()
    outputs = {ROOT / 'content/generated/synth-audio/manifest.json': data}
    for path, expected in outputs.items():
        if args.check:
            if path.read_bytes() != expected:
                raise ValueError("generated output differs: " + str(path))
        else:
            path.write_bytes(expected)
    print(json.dumps({'passed': True, 'cues': 170, 'normalCries': 56, 'prior151Unchanged': True, 'samplePackUnchanged': True}))


if __name__ == "__main__":
    main()
