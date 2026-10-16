#!/usr/bin/env python3
"""Import the pinned original HELP texts, ordered tables and progression predicates."""
import argparse
import json
import re
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from presentation_source import Source, need, safe
from project_paths import PROJECT_ROOT
from world_source import WorldSource

BASE = 'content/generated/ui/help/help.json'

def body(code, name):
    match = re.search(r'\b' + name + r'\s*\[[^;=]*=\s*\{(.*?)\n\};', code, re.S)
    need(match is not None, 'Missing HELP table ' + name)
    return match[1]

def symbols(value):
    return re.findall(r'\b(?:HELP_[A-Z0-9_]+|TOPIC_[A-Z0-9_]+)\b', value)

def pointers(code, name):
    return dict(re.findall(r'\[(\w+)\]\s*=\s*(Help_Text_\w+)', body(code, name)))

def generate(source):
    code = source.text('src/help_system.c')
    text = source.text('data/text/help_system.inc')
    text = re.sub(r'\.if REVISION < 1\n(.*?)\.else\n.*?\.endif', lambda m: m[1], text, flags=re.S)
    need('.if' not in text, 'Unsupported conditional HELP text')
    texts = {}
    replacements = {'PLUS': '+', 'LEFT_PAREN': '(', 'RIGHT_PAREN': ')', 'BIG_MULT_X': '×', 'TRIANGLE': '△', 'CIRCLE_DOT': '⊙', **{f'CIRCLE_{i}': str(i) for i in range(1, 6)}}
    for label, block in re.findall(r'(Help_Text_\w+)::\s*\n(.*?)(?=\nHelp_Text_|\Z)', text, re.S):
        value = ''.join(re.findall(r'\.string "(.*)"', block)).replace('\\n', '\n').replace('\\l', '\n').replace('\\p', '\n').replace('\\"', '"').removesuffix('$')
        for key, replacement in replacements.items():
            value = value.replace('{' + key + '}', replacement)
        need('\\' not in value and '$' not in value, 'Unsupported HELP text encoding ' + label)
        texts[label] = value
    need(len(texts) == 369, 'Incomplete original HELP text closure')
    topic_names = symbols(body(code, 'sHelpSystemContextTopicOrder'))
    labels = pointers(code, 'sHelpSystemTopicPtrs')
    descriptions = pointers(code, 'sHelpSystemTopicMouseoverDescriptionPtrs')
    table_pairs = [('TOPIC_WHAT_TO_DO', 'SpecializedQuestion', 'SpecializedAnswer'), ('TOPIC_HOW_TO_DO', 'MenuTopic', 'HowToUseMenu'), ('TOPIC_TERMS', 'Term', 'TermDefinitions'), ('TOPIC_ABOUT_GAME', 'GeneralTopic', 'GeneralTopicDescription'), ('TOPIC_TYPE_MATCHUP', 'TypeMatchup', 'TypeMatchupDescription')]
    entries = {}
    for topic, question, answer in table_pairs:
        q = pointers(code, 'sHelpSystem' + question + 'TextPtrs')
        a = pointers(code, 'sHelpSystem' + answer + 'TextPtrs')
        need(q.keys() == a.keys(), 'Unpaired HELP answers')
        entries[topic] = {key: dict(id=key, question=value, answer=a[key], condition=dict(kind='always', value='')) for key, value in q.items()}
    # Compile the simple original case/return predicates; fail closed on new syntax.
    predicate_code = code[code.index('static bool8 IsHelpSystemSubmenuEnabled(u8 id)\n{'):code.index('static bool8 HasGottenAtLeastOneHM(void)\n{')]
    branches = list(re.finditer(r'if \(gHelpSystemState.topic == (TOPIC_\w+)\)', predicate_code))
    for n, branch in enumerate(branches):
        section = predicate_code[branch.end():branches[n+1].start() if n+1 < len(branches) else len(predicate_code)]
        covered = set()
        for match in re.finditer(r'((?:\s*case HELP_\w+:)+)(.*?)(?=\s*case HELP_|\n        \})', section, re.S):
            cases, action = match.groups()
            if caught := re.search(r'GetKantoPokedexCount\(1\) > (\d+)', action):
                condition = dict(kind='caughtAbove', value=caught[1])
            elif 'HasGottenAtLeastOneHM()' in action:
                condition = dict(kind='anyHmFlag', value='')
            elif flag := re.search(r'return FlagGet\((FLAG_\w+)\)', action):
                condition = dict(kind='flag', value=flag[1])
            elif item := re.search(r'return CheckBagHasItem\((ITEM_\w+), 1\)', action):
                condition = dict(kind='item', value=item[1])
            else:
                need('return TRUE;' in action, 'Unsupported HELP predicate: ' + action)
                condition = dict(kind='always', value='')
            for key in re.findall(r'case (HELP_\w+):', cases):
                entries[branch[1]][key]['condition'] = condition
                covered.add(key)
        if branch[1] in ('TOPIC_WHAT_TO_DO', 'TOPIC_HOW_TO_DO'):
            need(covered == entries[branch[1]].keys(), 'Missing HELP unlock predicate')
    lists = {name: [x for x in symbols(block) if x != 'HELP_END'] for name, block in re.findall(r'static const u8 (s(?:AboutGame|HowTo|Terms|WhatToDo|TypeMatchups)_\w+)\[\] = \{(.*?)\n\};', code, re.S)}
    matchup_macro = code.split('#define HELP_TYPE_MATCHUPS')[1].split('static const')[0]
    matchup_order = re.findall(r'\b\d+\b', matchup_macro)
    need(set(matchup_order) == entries['TOPIC_TYPE_MATCHUP'].keys(), 'Incomplete HELP matchup order')
    for name in lists:
        if 'HELP_TYPE_MATCHUPS' in lists[name]:
            lists[name] = matchup_order
    contexts = {}
    standard = [x[0] for x in table_pairs]
    flag_rows = dict(re.findall(r'\[(HELPCONTEXT_\w+)\]\s*= CONTEXT_TOPIC_FLAGS\((.*?)\)', code))
    for line in body(code, 'sHelpSystemSubmenuItemLists').splitlines():
        if '//' not in line:
            continue
        values, context = line.split('//')
        context = context.strip()
        names = re.findall(r'\b(?:NULL|s\w+)\b', values)
        need(len(names) == 5, 'HELP context table width differs')
        enabled = [s.strip() == 'TRUE' for s in flag_rows.get(context, '').split(',')]
        contexts[context] = [dict(id=topic, entries=lists.get(names[standard.index(topic)], []) if topic in standard else []) for topic in topic_names if topic in standard + ['TOPIC_EXIT'] and len(enabled) == 6 and enabled[(standard + ['TOPIC_EXIT']).index(topic)]]
    hm_section = code[code.index('static bool8 HasGottenAtLeastOneHM(void)\n{'):]
    hm_flags = re.findall(r'FlagGet\((FLAG_\w+)\)', hm_section.split('\n}\n', 1)[0])
    basic_flag = re.search(r'if \(FlagGet\((FLAG_\w+)\) == TRUE && gHelpSystemState.topic == TOPIC_TERMS\)', code)[1]
    groups = json.loads(source.text('data/maps/map_groups.json'))
    maps = {}
    group_maps = []
    for group in groups['group_order']:
        names = groups[group]
        rows = [json.loads(source.text('data/maps/' + name + '/map.json')) for name in names]
        group_maps.append(rows)
        for row in rows:
            maps[row['id']] = row
    mart = set(re.findall(r'MAP_\w+', body(code, 'sMartMaps')))
    gym = set(re.findall(r'MAP_\w+', body(code, 'sGymMaps')))
    centers = set(re.findall(r'MAP_\w+', body(source.text('src/save_location.c'), 'sSaveLocationPokeCenterList')))
    indoor_function = source.text('src/overworld.c').split('bool8 IsMapTypeIndoors(u8 mapType)\n{')[1].split('\n}')[0]
    indoor_types = set(re.findall(r'mapType == (MAP_TYPE_\w+)', indoor_function))
    houses = set(re.findall(r'MAP_GROUP\((MAP_\w+)\)', code.split('#define IN_PLAYERS_HOUSE')[1].split('#define IN_OAKS_LAB')[0]))
    labs = set(re.findall(r'MAP_GROUP\((MAP_\w+)\)', code.split('#define IN_OAKS_LAB')[1].split('void SetHelpContextForMap')[0]))
    dungeon_rows = re.findall(r'MAP_GROUP\((MAP_\w+)\), MAP_NUM\(\1\), (\d+)', body(code, 'sDungeonMaps'))
    special = re.search(r'i != (\d+) /\*.*?\*/ \|\| FlagGet\((FLAG_\w+)\)', code)
    dungeons = {}
    for i, (first, count) in enumerate(dungeon_rows):
        group = next(g for g in group_maps if any(m['id'] == first for m in g))
        start = next(j for j, m in enumerate(group) if m['id'] == first)
        subset = group[start:start + int(count)]
        need(len(subset) == int(count), 'Dungeon range outside map group')
        for m in subset:
            dungeons[m['id']] = special[2] if i == int(special[1]) else ''
    map_contexts = {}
    for symbol, m in maps.items():
        context = 'OVERWORLD'
        if m['map_type'] in indoor_types:
            context = next((kind for selection, kind in [(houses, 'PLAYERS_HOUSE'), (labs, 'OAKS_LAB'), (centers, 'POKECENTER'), (mart, 'MART'), (gym, 'GYM')] if symbol in selection), 'INDOORS')
        map_contexts[symbol] = dict(context='HELPCONTEXT_' + context, dungeon=symbol in dungeons, dungeonFlag=dungeons.get(symbol, ''))
    return dict(schemaVersion=1, texts=texts, topicOrder=topic_names, topics={key: dict(label=labels[key], description=descriptions[key], entries=list(entries.get(key, {}).values())) for key in topic_names}, contexts=contexts, maps=map_contexts, basicTerms=lists['sTerms_Basic'], basicTermsFlag=basic_flag, hmFlags=hm_flags)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    inputs = parser.add_mutually_exclusive_group()
    inputs.add_argument('--reference-root', type=Path)
    inputs.add_argument('--raw', type=Path)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output = args.out or safe(args.project_root, BASE)
    raw = args.raw or args.reference_root or args.project_root / 'content/raw/pokefirered/037335f4'
    need(not output.resolve().is_relative_to(raw.resolve()) and not raw.resolve().is_relative_to(output.resolve()), 'Source/output overlap')
    output = safe(output.parent, output.name)
    result = generate(WorldSource(args.raw) if args.raw else Source(args.project_root, args.reference_root))
    if args.check:
        need(json.loads(output.read_bytes()) == result, 'HELP generated document differs from source')
    else:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, ensure_ascii=False, separators=(',', ':')) + '\n')
    print(f'HELP: {len(result["texts"])} texts, {len(result["contexts"])} contexts, {len(result["maps"])} maps')

if __name__ == '__main__':
    main()
