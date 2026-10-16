"""Recipe-selected Pokemon presentation using the shared source converters.

Selections describe the art each package owns, without predecessor cohorts or
duplicate weight/audio data from the shared runtime catalogs.
"""
import json
import re
from pathlib import Path

from project_paths import PROJECT_ROOT
import pokemon_import as pokemon
from pokemon_import_io import canonical, import_region, load_recipe, safe, unique_pairs

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
RECIPE = PROJECT_ROOT / 'scripts/recipes/pokemon/regional-presentation.json'


def load_selections(path=RECIPE):
    recipe = json.loads(safe(path).read_bytes(), object_pairs_hook=unique_pairs)
    if set(recipe) != {'schemaVersion', 'groups', 'regions'} or recipe['schemaVersion'] != 1:
        raise ValueError('Unsupported Pokemon presentation recipe')
    groups, regions = recipe['groups'], recipe['regions']
    if not groups or set(regions) != set(groups):
        raise ValueError('Empty or unknown presentation selections')
    for name, group in groups.items():
        if not re.fullmatch(r'[a-z0-9-]+', name) or set(group) != {'species'}:
            raise ValueError('Invalid species selection: ' + name)
        species = group['species']
        if (not species or any(not isinstance(s, str) or not re.fullmatch(r'[A-Za-z0-9]+', s) for s in species)
                or len(species) != len(set(species))):
            raise ValueError('Invalid or duplicate source species names: ' + name)
    for region, options in regions.items():
        if set(options) - {'supplement'} or ('supplement' in options and type(options['supplement']) is not bool):
            raise ValueError('Invalid presentation options: ' + region)
    return recipe


def load_lock(region, lock_path=None, selections=None):
    selections = selections if selections is not None else load_selections()
    if region not in selections['regions']:
        raise ValueError('Unknown presentation region: ' + region)
    return load_recipe(lock_path or PROJECT_ROOT / f'scripts/recipes/pokemon/{region}-pokemon.lock.json',
                       commit=COMMIT, profile=f'firered-{region}-pokemon-v1')


def art_species(region, sources, selections):
    names = selections['groups'][region]['species']
    constants = sources['include/constants/species.h'].decode('utf-8')
    result = []
    for name in names:
        symbol = 'SPECIES_' + pokemon.source_symbol(name)
        matches = re.findall(r'^#define ' + symbol + r'[ \t]+(\d+)[ \t]*$', constants, re.M)
        if len(matches) != 1:
            raise ValueError('Missing or ambiguous species identity: ' + symbol)
        result.append((name, int(matches[0])))
    return result


def convert(region, lock, sources, selections=None):
    selections = selections if selections is not None else load_selections()
    options = selections['regions'][region]
    species = art_species(region, sources, selections)
    outputs = pokemon.battle_art(lock, sources, profile=lock['profile'], species=species,
                                 limitation='One static front/back frame, normal and shiny offline PNG.')
    if options.get('supplement', False):
        outputs.update(pokemon.supplement(sources, species=species))
    textures = sum(name.endswith('.png') for name in outputs)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, inputCount=len(lock['files']),
        artSpeciesCount=len(species), textureCount=textures, rgbaCount=textures,
        outputs=[dict(path=name) for name in sorted(outputs)]))
    return outputs


def import_content(region, raw, out, repository=None, lock_path=None, recipe_path=RECIPE):
    selections = load_selections(recipe_path)
    lock = load_lock(region, lock_path, selections)
    return import_region(raw, out, recipe=lock,
        convert=lambda lock, sources: convert(region, lock, sources, selections), repository=repository)
