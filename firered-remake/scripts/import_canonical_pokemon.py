#!/usr/bin/env python3
"""Offline, fail-closed source extractor. No C/upstream code, network, ROM, or campaign mutation."""
import argparse
from decimal import Decimal
import json
from pathlib import Path
import re

from build_source_lock import COMMIT, TREE, PROFILE, FILES, encode, git

ROOT = Path(__file__).resolve().parent.parent
MOVE_FILES = [
    'content/generated/metronome-moves/catalog.json',
]
SPECIES_INFO = 'src/data/pokemon/species_info.h'
LEARNSETS = 'src/data/pokemon/level_up_learnsets.h'
POINTERS = 'src/data/pokemon/level_up_learnset_pointers.h'
EVOLUTION = 'src/data/pokemon/evolution.h'
POKEDEX = 'src/data/pokemon/pokedex_entries.h'
POKEMON = 'src/pokemon.c'
STATS = [('hp', 'HP'), ('attack', 'Attack'), ('defense', 'Defense'),
         ('speed', 'Speed'), ('specialAttack', 'SpAttack'), ('specialDefense', 'SpDefense')]
GROWTH = dict(GROWTH_MEDIUM_FAST='mediumFast', GROWTH_ERRATIC='erratic',
              GROWTH_FLUCTUATING='fluctuating', GROWTH_MEDIUM_SLOW='mediumSlow',
              GROWTH_FAST='fast', GROWTH_SLOW='slow')

def require(condition, message):
    if not condition:
        raise ValueError(message)

def one(pattern, text):
    matches = list(re.finditer(pattern, text, re.M | re.S))
    require(len(matches) == 1, 'Expected exactly one source match: ' + pattern)
    return matches[0]

def number(value):
    require(re.fullmatch(r'\d+', value) is not None, 'Unsupported unsigned decimal: ' + value)
    return int(value)

def pair(value, prefix):
    match = re.fullmatch(r'\{(' + prefix + r'\w+), (' + prefix + r'\w+)\}', value)
    require(match is not None, 'Unsupported pair: ' + value)
    return match[1], match[2]

def ident(symbol):
    return dict(value='pret:' + symbol)

def select_firered(text):
    """Keep source offsets and line numbers; only the two reviewed FR/LG blocks are legal."""
    output, active, state, blocks = [], True, None, 0
    for line in text.splitlines(keepends=True):
        directive = line.strip()
        if directive == '#if defined(FIRERED)':
            require(state is None, 'Nested edition selection is unsupported')
            state, active, blocks = 'FR', True, blocks + 1
        elif directive == '#elif defined(LEAFGREEN)':
            require(state == 'FR', 'Unmatched LEAFGREEN branch')
            state, active = 'LG', False
        elif directive == '#endif':
            require(state == 'LG', 'Unmatched edition end')
            state, active = None, True
        elif directive.startswith('#') and not directive.startswith('#define LEVEL_UP_'):
            raise ValueError('Unhandled learnset directive: ' + directive)
        else:
            output.append(line if active else ''.join('\n' if c == '\n' else ' ' for c in line))
            continue
        output.append(''.join('\n' if c == '\n' else ' ' for c in line))
    require(state is None and blocks == 2, 'Expected exactly the Dugtrio and Deoxys edition branches')
    result = ''.join(output)
    require(len(result) == len(text), 'Edition filter changed source coordinates')
    return result

class Sources:
    def __init__(self, sources, metadata):
        self.text = sources
        self.meta = metadata

    def ref(self, path, symbol, start, end):
        text = self.text[path]
        return dict(path=path, symbol=symbol, startLine=text.count('\n', 0, start) + 1,
                    endLine=text.count('\n', 0, end) + 1)

    def block(self, path, symbol):
        m = one(r'^    \[' + re.escape(symbol) + r'\] =\s*\n    \{\n(.*?)^    \},', self.text[path])
        fields = {}
        for line in m[1].splitlines():
            f = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            require(f is not None and f[1] not in fields, 'Unknown/duplicate source field: ' + line)
            fields[f[1]] = f[2]
        return fields, self.ref(path, symbol, m.start(), m.end())

    def numeric_constants(self, path, prefix):
        result = {}
        for m in re.finditer(r'^#define (' + prefix + r'\w+)\s+(0x[0-9A-Fa-f]+|\d+)\s*(?://[^\n]*)?$', self.text[path], re.M):
            require(m[1] not in result, 'Duplicate source constant: ' + m[1])
            result[m[1]] = int(m[2], 0) if m[2].startswith('0x') else int(m[2])
        return result

    def function(self, name):
        m = one(r'^(?:static )?(?:void|bool8|u8|u16|u32|s32) ' + name + r'\([^;]*?\)\n\{', self.text[POKEMON])
        depth, pos = 1, m.end()
        while depth:
            require(pos < len(self.text[POKEMON]), 'Unterminated source function: ' + name)
            depth += (self.text[POKEMON][pos] == '{') - (self.text[POKEMON][pos] == '}')
            pos += 1
        return self.ref(POKEMON, name, m.start(), pos)

def authenticate(root, source_root, move_root):
    lock = dict(schemaVersion=1, repository='pret/pokefirered', commit=COMMIT, profile=PROFILE, gameEdition='FIRERED', romRevision=0, canonicalSpeciesCount=386, officialAbilityCount=76, files=[dict(path=p) for p in FILES])
    require([r['path'] for r in lock['files']] == FILES, 'Source inventory paths differ')
    require(lock['commit'] == COMMIT, 'Source version differs')
    sources, metadata = {}, {}
    for row in lock['files']:
        raw_path = root / 'content/raw/pokefirered/037335f4' / row['path']
        if source_root:
            data = git(source_root, 'show', COMMIT + ':' + row['path'])
            raw_path.parent.mkdir(parents=True, exist_ok=True)
            raw_path.write_bytes(data)
        data = raw_path.read_bytes()
        sources[row['path']], metadata[row['path']] = data.decode(), row
    dep_data = [(move_root / path).read_bytes() for path in MOVE_FILES]
    moves = json.loads(dep_data[0])
    require(len(moves['moves']) == 354 and [m['sourceMoveId'] for m in moves['moves']] == list(range(1, 355)), 'Move closure differs')
    return lock, Sources(sources, metadata), moves

def extract(lock, src, moves):
    species_ids = src.numeric_constants('include/constants/species.h', 'SPECIES_')
    species_ids = {k: v for k, v in species_ids.items() if k not in ('SPECIES_NONE', 'SPECIES_EGG') and not k.startswith('SPECIES_OLD_UNOWN_')}
    require(len(species_ids) == 386 and sorted(species_ids.values()) == list(range(1, 252)) + list(range(277, 412)), 'Canonical internal species ID closure differs')
    abilities = src.numeric_constants('include/constants/abilities.h', 'ABILITY_')
    official = {k: v for k, v in abilities.items() if k not in ('ABILITY_NONE', 'ABILITY_CACOPHONY')}
    require(len(official) == 76 and sorted(official.values()) == list(range(1, 76)) + [77], 'Official ability closure differs')
    constants = src.numeric_constants('include/constants/pokemon.h', '(?:TYPE_|MON_|EVO_|GROWTH_)')
    items = src.numeric_constants('include/constants/items.h', 'ITEM_')
    move_ids = {row['id']['value'] for row in moves['moves']}
    dex_enum = one(r'// National Pokedex order\nenum \{(.*?)\n\};', src.text['include/constants/pokedex.h'])
    dex_symbols = []
    for line in dex_enum[1].splitlines():
        line = line.strip()
        if not line or line.startswith('//'):
            continue
        require(re.fullmatch(r'NATIONAL_DEX_\w+,', line) is not None, 'Unsupported national dex enum syntax')
        dex_symbols.append(line[:-1])
    dex_ids = {symbol: i for i, symbol in enumerate(dex_symbols)}
    dex_map = one(r'^static const u16 sSpeciesToNationalPokedexNum\[NUM_SPECIES - 1\] =\n\{(.*?)^\};', src.text[POKEMON])
    mapped_names = re.findall(r'^    SPECIES_TO_NATIONAL\((\w+)\),$', dex_map[1], re.M)
    require(set(species_ids) <= {'SPECIES_' + name for name in mapped_names}, 'Source species-to-national mapping incomplete')
    learntext = select_firered(src.text[LEARNSETS])
    evolutions = {}
    for em in re.finditer(r'^    \[(SPECIES_\w+)\]\s*= (\{\{.*?\}\}),$', src.text[EVOLUTION], re.M | re.S):
        require(em[1] in species_ids and em[1] not in evolutions, 'Unknown/duplicate evolution source species')
        raw = em[2][1:-1]
        entries, pos = [], 0
        for match in re.finditer(r'\{(EVO_\w+),\s+(\d+|ITEM_\w+),\s+(SPECIES_\w+)\}', raw):
            require(re.fullmatch(r'[\s,]*', raw[pos:match.start()]) is not None, 'Unhandled evolution row syntax')
            method, parameter, target = match.groups()
            require(method in constants and target in species_ids, 'Unknown evolution method/target')
            parameter_value = items[parameter] if parameter.startswith('ITEM_') else number(parameter)
            entries.append(dict(method=method, sourceMethodId=constants[method], parameter=parameter, parameterValue=parameter_value, target=ident(target)))
            pos = match.end()
        require(entries and re.fullmatch(r'[\s,]*', raw[pos:]) is not None, 'Missing/unparsed evolution row')
        evolutions[em[1]] = entries
    require(len(evolutions) == len(re.findall(r'^    \[SPECIES_', src.text[EVOLUTION], re.M)), 'Evolution parser skipped source records')
    species, metadata = [], []
    expected_fields = {'base' + x for _, x in STATS} | {'evYield_' + x for _, x in STATS} | {
        'types', 'catchRate', 'expYield', 'itemCommon', 'itemRare', 'genderRatio', 'eggCycles',
        'friendship', 'growthRate', 'eggGroups', 'abilities', 'safariZoneFleeRate', 'bodyColor', 'noFlip'}
    for symbol, sid in sorted(species_ids.items(), key=lambda x: x[1]):
        f, _ = src.block(SPECIES_INFO, symbol)
        require(set(f) == expected_fields, 'Species fields differ: ' + symbol)
        gender = f['genderRatio']
        if gender.startswith('PERCENT_FEMALE('):
            m = re.fullmatch(r'PERCENT_FEMALE\((\d+(?:\.\d+)?)\)', gender)
            require(m is not None, 'Unsupported gender ratio')
            ratio = min(254, int(Decimal(m[1]) * 255 / 100))
        else:
            require(gender in ('MON_MALE', 'MON_FEMALE', 'MON_GENDERLESS'), 'Unsupported gender constant')
            ratio = constants[gender]
        types, slots = pair(f['types'], 'TYPE_'), pair(f['abilities'], 'ABILITY_')
        require(all(t in constants for t in types) and slots[0] in official and (slots[1] in official or slots[1] == 'ABILITY_NONE'), 'Invalid species type/ability')
        pm = one(r'^    \[' + symbol + r'\] = (\w+),$', src.text[POINTERS])
        pointer = pm[1]
        lm = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', learntext)
        entries, end_seen = [], False
        for line in lm[1].splitlines(keepends=True):
            stripped = line.strip()
            if not stripped:
                continue
            if stripped == 'LEVEL_UP_END':
                require(not end_seen, 'Duplicate learnset terminator')
                end_seen = True
            else:
                match = re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),\n', line)
                require(match is not None and not end_seen, 'Unhandled learnset entry: ' + line)
                level, move = number(match[1]), match[2]
                require(1 <= level <= 100 and 'pret:' + move in move_ids, 'Invalid learnset level/move')
                entries.append(dict(level=level, move=ident(move)))
        require(end_seen and entries and [e['level'] for e in entries] == sorted(e['level'] for e in entries), 'Unterminated/empty/unordered learnset')
        dex_symbol = symbol.replace('SPECIES_', 'NATIONAL_DEX_', 1)
        dex, _ = src.block(POKEDEX, dex_symbol)
        require(1 <= dex_ids[dex_symbol] <= 386, 'Noncanonical national dex identity')
        species.append(dict(id=ident(symbol), sourceSpeciesId=sid, baseStats={k: number(f['base' + suffix]) for k, suffix in STATS}, type1=types[0], type2=types[1], catchRate=number(f['catchRate']), experienceYield=number(f['expYield']), evYield={k: number(f['evYield_' + suffix]) for k, suffix in STATS}, growthRate=GROWTH[f['growthRate']], genderRatio=ratio, ability1=slots[0], ability2=slots[1], friendship=number(f['friendship']), levelUpMoves=entries, evolutions=[]))
        require(f['noFlip'] in ('FALSE', 'TRUE'), 'Invalid noFlip value')
        metadata.append(dict(id=ident(symbol), nationalDexNumber=dex_ids[dex_symbol], heightDecimeters=number(dex['height']), weightHectograms=number(dex['weight']), sourceEvolutions=evolutions.get(symbol, []), eggGroups=list(pair(f['eggGroups'], 'EGG_GROUP_')), eggCycles=number(f['eggCycles']), itemCommon=f['itemCommon'], itemRare=f['itemRare'], safariZoneFleeRate=number(f['safariZoneFleeRate']), bodyColor=f['bodyColor'], noFlip=f['noFlip'] == 'TRUE'))
    ability_rows = [dict(symbol=symbol, sourceAbilityId=value)
                    for symbol, value in sorted(official.items(), key=lambda row: row[1])]
    deoxys = one(r'#if defined\(FIRERED\)\n// Attack forme\nstatic const u16 sDeoxysBaseStats\[\] = \s*\n\{(.*?)\n\};', src.text[POKEMON])
    deoxys_fields = dict(re.findall(r'^    \[(STAT_\w+)\]\s*= (\d+),$', deoxys[1], re.M))
    stat_keys = ['STAT_HP', 'STAT_ATK', 'STAT_DEF', 'STAT_SPEED', 'STAT_SPATK', 'STAT_SPDEF']
    require(set(deoxys_fields) == set(stat_keys), 'Deoxys form stat fields differ')
    form = dict(species=ident('SPECIES_DEOXYS'), scope='firered-ordinary-non-link-stat-read', baseStats={k: number(deoxys_fields[stat]) for (k, _), stat in zip(STATS, stat_keys)})
    contract = dict(maximumHpOneSpecies=ident('SPECIES_SHEDINJA'), formStatOverrides=[form])
    return dict(schemaVersion=1, species=species, metadata=metadata, abilities=ability_rows, generationContract=contract)

def build(root, source_root, move_root):
    lock, sources, moves = authenticate(root, source_root, move_root)
    doc = extract(lock, sources, moves)
    data = encode(doc)
    counts = dict(species=len(doc['species']), officialAbilities=len(doc['abilities']), moves=354,
        levelUpEntries=sum(len(s['levelUpMoves']) for s in doc['species']),
        sourceEvolutionEntries=sum(len(m['sourceEvolutions']) for m in doc['metadata']),
        sourceEvolutionMethods=len({e['method'] for m in doc['metadata'] for e in m['sourceEvolutions']}))
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
         inputCount=len(lock['files']), counts=counts,
        outputs=[dict(path='catalog.json')],
        campaignAdmissionChanged=False, upstreamScriptsExecuted=False)
    target = root / 'content/generated/canonical-pokemon'
    target.mkdir(parents=True, exist_ok=True)
    (target / 'catalog.json').write_bytes(data)
    (target / 'manifest.json').write_bytes(encode(manifest))
    return manifest

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--source-root', type=Path, help='Verify pinned Git source and materialize locked local raw files.')
    parser.add_argument('--move-root', type=Path, required=True, help='Existing authenticated 354-move project dependency.')
    args = parser.parse_args()
    print(json.dumps(build(args.root, args.source_root, args.move_root), sort_keys=True))
