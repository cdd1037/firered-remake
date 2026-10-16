"""Recipe-selected Pokemon presentation using the shared source converters.

Selections describe additions once. Includes compose weight tables without reading
prior generated regions or copying canonical species definitions into recipes.
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
    if not groups or not regions or not set(regions) <= set(groups):
        raise ValueError('Empty or unknown presentation selections')
    for name, group in groups.items():
        if not re.fullmatch(r'[a-z0-9-]+', name) or set(group) - {'species', 'include'}:
            raise ValueError('Invalid species selection: ' + name)
        if not group.get('species') or any(not isinstance(s, str) or not re.fullmatch(r'[A-Za-z0-9]+', s) for s in group['species']):
            raise ValueError('Invalid source species names: ' + name)
        selection_species(recipe, name)
    for region, options in regions.items():
        if set(options) - {'artSpecies', 'supplement', 'sortPriorWeights'}:
            raise ValueError('Unknown presentation option: ' + region)
        if 'sortPriorWeights' in options and not isinstance(options['sortPriorWeights'], bool):
            raise ValueError('Invalid weight order')
        art = options.get('artSpecies', groups[region]['species'])
        if not art or len(art) != len(set(art)) or not set(art) <= set(selection_species(recipe, region)):
            raise ValueError('Invalid art selection: ' + region)
        if 'supplement' in options:
            supplement = options['supplement']
            if set(supplement) - {'naturalLevelLimit', 'cryWaves'} or 'naturalLevelLimit' not in supplement:
                raise ValueError('Invalid supplement options')
            limit = supplement['naturalLevelLimit']
            if limit is not None and (type(limit) is not int or not 1 <= limit <= 100):
                raise ValueError('Invalid natural level limit')
            if 'cryWaves' in supplement and not isinstance(supplement['cryWaves'], bool):
                raise ValueError('Invalid cry source selection')
    return recipe


def selection_species(recipe, name, active=()):
    if name in active or name not in recipe['groups']:
        raise ValueError('Cyclic or unknown species selection: ' + name)
    group = recipe['groups'][name]
    prior = selection_species(recipe, group['include'], (*active, name)) if 'include' in group else []
    result = prior + group['species']
    if len(result) != len(set(result)):
        raise ValueError('Duplicate species selection: ' + name)
    return result


def load_lock(region, lock_path=None, selections=None):
    selections = selections if selections is not None else load_selections()
    if region not in selections['regions']:
        raise ValueError('Unknown presentation region: ' + region)
    return load_recipe(lock_path or PROJECT_ROOT / f'scripts/recipes/pokemon/{region}-pokemon.lock.json',
                       commit=COMMIT, profile=f'firered-{region}-pokemon-v1')


def art_species(region, sources, selections):
    names = selections['regions'][region].get('artSpecies', selections['groups'][region]['species'])
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
    composed = selection_species(selections, region)
    if 'supplement' in options:
        weights = [pokemon.source_symbol(name) for name in composed]
        if options.get('sortPriorWeights'):
            added = len(selections['groups'][region]['species'])
            weights = sorted(weights[:-added]) + weights[-added:]
        supplement = options['supplement']
        outputs.update(pokemon.supplement(lock, sources, profile=lock['profile'], species=species,
            weight_species=weights, natural_level_limit=supplement['naturalLevelLimit'],
            cry_waves=supplement.get('cryWaves', False)))
    textures = sum(name.endswith('.png') for name in outputs)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, inputCount=len(lock['files']),
        addedSpeciesCount=len(selections['groups'][region]['species']), composedSpeciesCount=len(composed),
        artSpeciesCount=len(species), textureCount=textures, rgbaCount=textures,
        outputs=[dict(path=name) for name in sorted(outputs)]))
    return outputs


def import_content(region, raw, out, repository=None, lock_path=None, recipe_path=RECIPE):
    selections = load_selections(recipe_path)
    lock = load_lock(region, lock_path, selections)
    return import_region(raw, out, recipe=lock,
        convert=lambda lock, sources: convert(region, lock, sources, selections), repository=repository)
