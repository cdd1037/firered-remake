#!/usr/bin/env python3
"""Check Snorlax in the shared catalogs and its source-derived presentation."""
import argparse
import io
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from PIL import Image
from generated_output_compare import generated_output_matches
from import_canonical_pokemon import Sources
from pokemon_import_io import safe
from project_paths import PROJECT_ROOT
from species_art import build as build_art


def verify(root, raw):
    # Read full species and move definitions from the shared current catalogs;
    # regional additions now contain references rather than copied definitions.
    catalog = json.loads(safe(root, 'content/generated/data/pokemon/canonical/catalog.json').read_bytes())
    species = next(row for row in catalog['species'] if row['id']['value'] == 'pret:SPECIES_SNORLAX')
    paths = ('src/data/pokemon/species_info.h', 'include/constants/pokemon.h',
             'include/constants/abilities.h', 'include/constants/species.h',
             'src/data/pokemon/level_up_learnset_pointers.h', 'src/data/pokemon/level_up_learnsets.h')
    source = Sources({path: safe(raw, path).read_text() for path in paths}, {})
    expected, _ = source.species_definition('SPECIES_SNORLAX')
    assert species == expected, 'Snorlax shared definition differs from source'
    assert species['baseStats'] == dict(hp=160, attack=110, defense=65, speed=30, specialAttack=65, specialDefense=110)
    assert (species['sourceSpeciesId'], species['genderRatio'], species['growthRate'], species['catchRate'], species['experienceYield']) == (143, 31, 'slow', 25, 154)
    assert (species['ability1'], species['ability2']) == ('ABILITY_IMMUNITY', 'ABILITY_THICK_FAT')
    expected_moves = [(1,'TACKLE'), (5,'AMNESIA'), (9,'DEFENSE_CURL'), (13,'BELLY_DRUM'),
                      (17,'HEADBUTT'), (21,'YAWN'), (25,'REST'), (29,'SNORE'), (33,'BODY_SLAM'),
                      (37,'SLEEP_TALK'), (41,'BLOCK'), (45,'COVET'), (49,'ROLLOUT'), (53,'HYPER_BEAM')]
    assert [(row['level'], row['move']['value'][10:]) for row in species['levelUpMoves']] == expected_moves
    moves = {row['id']['value']: row for row in json.loads(
        safe(root, 'content/generated/data/services/metronome-moves.json').read_bytes())['moves']}
    for symbol, pp, effect, power in [('SNORE',15,'EFFECT_SNORE',40), ('SLEEP_TALK',10,'EFFECT_SLEEP_TALK',0),
                                      ('BLOCK',5,'EFFECT_MEAN_LOOK',0), ('COVET',40,'EFFECT_THIEF',40)]:
        move = moves['pret:MOVE_' + symbol]
        assert (move['pp'], move['effect'], move['power']) == (pp, effect, power), symbol
    for level, expected in {1:['TACKLE'], 13:['TACKLE','AMNESIA','DEFENSE_CURL','BELLY_DRUM'],
                            30:['HEADBUTT','YAWN','REST','SNORE'], 33:['YAWN','REST','SNORE','BODY_SLAM'],
                            37:['REST','SNORE','BODY_SLAM','SLEEP_TALK'], 40:['REST','SNORE','BODY_SLAM','SLEEP_TALK'],
                            41:['SNORE','BODY_SLAM','SLEEP_TALK','BLOCK'], 100:['BLOCK','COVET','ROLLOUT','HYPER_BEAM']}.items():
        learned = [row['move']['value'] for row in species['levelUpMoves'] if row['level'] <= level][-4:]
        assert learned == ['pret:MOVE_' + symbol for symbol in expected], level
    assert [moves['pret:MOVE_' + symbol]['pp'] for symbol in ('HEADBUTT','YAWN','REST','SNORE')] == [15,10,10,15]

    pixels = build_art(root, 'snorlax', raw_root=raw)
    manifest = json.loads(pixels['manifest.json'])
    for relative, data in pixels.items():
        assert generated_output_matches(safe(root, 'content/generated/sprites/pokemon/snorlax/' + relative), data), relative
    dex = json.loads(safe(root, 'content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json').read_bytes())
    existing = next(row for row in dex['entries'] if row['sourceSpeciesId'] == 143)
    for key, value in manifest['dex'].items():
        assert existing[key] == value, key
    # Independent pixel oracle keeps transparent RGB and floor RGB5 expansion.
    for sprite in manifest['sprites']:
        with Image.open(safe(raw, sprite['sourceImagePath'])) as source_image:
            indices = source_image.tobytes()
        lines = safe(raw, sprite['sourcePalettePath']).read_text().splitlines()
        colors = [list(map(int, line.split())) for line in lines[3:]]
        expected = bytes(channel for index in indices for channel in
                         (*[(value // 8) * 255 // 31 for value in colors[index]], 255 if index else 0))
        with Image.open(io.BytesIO(pixels[sprite['texturePath']])) as decoded:
            assert decoded.size == (64,64) and decoded.convert('RGBA').tobytes() == expected
    assert manifest['dex']['weightHectograms'] == 4600


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dependency-project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--reference-root', type=Path, help='Pinned local raw input directory')
    args = parser.parse_args()
    raw = args.reference_root or args.dependency_project_root / 'content/raw/pokefirered/037335f4'
    verify(args.dependency_project_root, raw)
    print('PASS: Snorlax shared source definition, natural move boundaries, shared move definitions, Dex and full RGBA art.')


if __name__ == '__main__':
    main()
