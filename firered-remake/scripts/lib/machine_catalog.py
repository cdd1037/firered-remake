"""Shared TM/HM import API. Definitions and compatibility come only from raw data.

Regional importers select admitted item/species IDs; they never copy predecessor
machine definitions or expand an old regional output into a new one.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from functools import lru_cache
import importlib.util
from pathlib import Path

_IMPORTER = (_PROJECT_ROOT / 'scripts/imports/items/import-machine-catalog.py')
_spec = importlib.util.spec_from_file_location('shared_machine_source_compiler', _IMPORTER)
_compiler = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_compiler)


@lru_cache(maxsize=None)
def load_catalog(project_root):
    root = Path(project_root).resolve()
    return _compiler.Catalog(_compiler.compile_catalog(root / _compiler.RAW_PATH,
        _compiler.read_json(root / _compiler.RECIPE_PATH)))


def machine_reference(catalog, item, species):
    species = list(species)
    catalog.machine(item, species)  # validate the requested admitted definition and roster
    return dict(item=item, species=species)


def compatibility_reference(catalog, item, species):
    catalog.compatibility(item, species)
    return dict(item=item, species=species)


def compact(catalog, value, relative_path):
    """Validate a freshly parsed importer value and emit typed references."""
    return _compiler.transform(value, catalog, relative_path)


@lru_cache(maxsize=None)
def _admission_views(project_root, relative_path):
    root = Path(project_root).resolve()
    recipe = _compiler.read_json(root / 'scripts/recipes/config/machine-admissions.json')
    _compiler.require(recipe['schemaVersion'] == 1, 'Unsupported machine admissions recipe')
    _compiler.require(relative_path in recipe['files'], 'Unknown machine view: ' + relative_path)
    catalog = load_catalog(root)
    result = {}
    for group in recipe['files'][relative_path]:
        species = group['species']
        _compiler.require(len(species) == len(set(species)), 'Duplicate admitted species')
        for view in group['views']:
            pointer, item = view['pointer'], view['item']
            _compiler.require(pointer not in result, 'Duplicate machine view pointer: ' + pointer)
            if view['kind'] == 'machine':
                value = machine_reference(catalog, item, species)
            elif view['kind'] == 'compatibility':
                value = [compatibility_reference(catalog, item, species_id) for species_id in species]
            else:
                raise ValueError('Unknown machine view kind: ' + view['kind'])
            result[pointer] = (item, tuple(species), value)
    return result


def admission_view(project_root, relative_path, pointer):
    """Return the target region's fixed ordered machine/cell view, independently."""
    import copy
    return copy.deepcopy(_admission_views(project_root, relative_path)[pointer][2])


def admission_species(project_root, relative_path, pointer):
    """Ordered full species IDs from a flat target-region admission recipe."""
    return _admission_views(project_root, relative_path)[pointer][1]


def admission_item(project_root, relative_path, pointer):
    return _admission_views(project_root, relative_path)[pointer][0]


def admission_rows(project_root, relative_path, pointer):
    """Typed eligibility rows with source species IDs, without source-location proof."""
    catalog = load_catalog(project_root)
    return [catalog.compatibility(admission_item(project_root, relative_path, pointer), species)
            for species in admission_species(project_root, relative_path, pointer)]


def compatibility_additions(project_root, relative_path, field='additions'):
    """Ordered {item, compatibility} selections for a target extension document."""
    import re
    rows = []
    pattern = re.compile('/' + re.escape(field) + r'/(\d+)/compatibility')
    for pointer, (item, _, _) in _admission_views(project_root, relative_path).items():
        match = pattern.fullmatch(pointer)
        if match:
            rows.append((int(match[1]), dict(item=dict(value=item),
                         compatibility=admission_view(project_root, relative_path, pointer))))
    rows.sort()
    _compiler.require(rows and [index for index, _ in rows] == list(range(len(rows))),
                      'Missing/non-contiguous compatibility additions: ' + relative_path + '/' + field)
    return [row for _, row in rows]


def machine_definitions(project_root, relative_path, field='newMachines'):
    """Ordered compact definition views for a target document, with no predecessor."""
    import re
    rows = []
    pattern = re.compile('/' + re.escape(field) + r'/(\d+)')
    for pointer in _admission_views(project_root, relative_path):
        match = pattern.fullmatch(pointer)
        if match:
            rows.append((int(match[1]), admission_view(project_root, relative_path, pointer)))
    rows.sort()
    _compiler.require(rows and [index for index, _ in rows] == list(range(len(rows))),
                      'Missing/non-contiguous machine definitions: ' + relative_path + '/' + field)
    return [row for _, row in rows]
