#!/usr/bin/env python3
"""Convert a locked one-map gym package using its source-script inventory recipe."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
import argparse
import importlib.util
import json
import shutil
import tempfile
from project_paths import PROJECT_ROOT
from map_runtime import COMMIT, encode, generated_root, publish_map_package, require
from map_source import LockedSource, connections_and_warps, render_map, source_directory, require_safe_target, read_source_lock
from shared_constants import package_documents


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def run(recipe, raw, out, *, project=PROJECT_ROOT, reference=None):
    recipe, raw, out, project = map(Path, (recipe, raw, out, project))
    require_safe_target(recipe.parent, recipe)
    lock = json.loads(recipe.read_bytes())
    require(len(lock.get('maps', [])) == 1, 'Expected one source map')
    lock = read_source_lock(recipe, maps=[(lock['maps'][0],)], profile=lock['profile'])
    config = lock['conversion']
    inventory_path = project / config['scriptInventory']
    require_safe_target(project / 'scripts', inventory_path)
    generated = generated_root(out)
    require_safe_target(generated / 'maps/regions', out)
    require(out.resolve() != (generated / 'maps/regions').resolve(), 'Select one region output directory')
    require(not out.resolve().is_relative_to(raw.resolve()) and not raw.resolve().is_relative_to(out.resolve()), 'Source/output overlap')
    shared = module(project / 'scripts/imports/maps/import-route22-region.py', '_gym_source_contracts')
    if reference:
        shared.read_lock = lambda path=None: lock
        shared.materialize(raw, Path(reference), source_package=recipe.name.removesuffix('.lock.json'))
    src = LockedSource(raw, lock)
    name = lock['maps'][0]
    directory = source_directory(name)
    stable_id = 'map.' + directory
    inventory = module(inventory_path, '_gym_script_inventory')
    # Bind geometry from the source declarations, not a second regional tileset table.
    from map_source import tileset_bindings
    tilesets = tileset_bindings(src)
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.gym-import-', dir=out.parent) as temporary:
        stage = Path(temporary)
        world = render_map(src, name, stable_id, stage / directory, tilesets=tilesets)
        scripts = inventory.script_inventory(src, [world])
        constants = shared.constants_inventory(src, [world], scripts)
        npcs = shared.npc_inventory(src, [world], stage / 'npc')
        connections, warps = connections_and_warps(src, [world])
        catalog = dict(schemaVersion=1, commit=COMMIT, maps=[dict(sourceName=name, stableId=stable_id,
            directory=directory, sourceMapSymbol=world['events']['id'])], connections=connections, warps=warps)
        documents = package_documents(generated, constants, {
            config['catalog']: catalog, 'scripts.json': scripts, 'npc-source.json': npcs})
        for filename, document in documents.items():
            require_safe_target(stage, stage / filename)
            (stage / filename).write_bytes(encode(document))
        publish_map_package(stage)
        manifest = dict(schemaVersion=1, outputs=[dict(path=p.relative_to(stage).as_posix())
            for p in sorted(stage.rglob('*')) if p.is_file()])
        (stage / 'manifest.json').write_bytes(encode(manifest))
        for path in sorted(stage.rglob('*')):
            if path.is_file():
                target = out / path.relative_to(stage)
                require_safe_target(out, target)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(path, target)
    (out / 'dependencies.json').unlink(missing_ok=True)
    return dict(schemaVersion=1, maps=1, outputs=len(manifest['outputs']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recipe', type=Path, required=True)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--reference-repo', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(run(args.recipe, args.raw, args.out, project=args.project_root, reference=args.reference_repo), sort_keys=True))


if __name__ == '__main__':
    main()
