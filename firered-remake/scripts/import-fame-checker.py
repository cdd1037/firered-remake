#!/usr/bin/env python3
"""Extract the complete pinned Fame Checker catalog. No upstream code is executed.
Reads pinned local Git objects or an existing extracted source tree.
--output contains one small deterministic JSON resource. The checked-in lock
declares the source inventory; extracted files are checked by the converter's
format and semantic rules, not authenticated against Git object hashes.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-fame-checker-v1'


def need(value, message):
    if not value:
        raise ValueError(message)


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()


def read_sources(git_dir, lock_path):
    lock_bytes = lock_path.read_bytes()
    lock = json.loads(lock_bytes)
    need(lock['commit'] == COMMIT and lock['profile'] == PROFILE,
         'fame_checker_lock_identity')
    def git(*args):
        return subprocess.check_output(['git', '--git-dir=' + str(git_dir / '.git' if (git_dir / '.git').exists() else git_dir), *args])
    sources = {}
    for row in lock['files']:
        path = row['path']
        need(path not in sources and not Path(path).is_absolute() and '..' not in Path(path).parts,
             'fame_checker_source_path')
        raw = (git_dir / path).read_bytes() if not (git_dir / 'HEAD').exists() and not (git_dir / '.git').exists() else git('show', COMMIT + ':' + path)
        sources[path] = raw.decode('utf-8')
    return lock, sources


def without_comments(text):
    return re.sub(r'//[^\n]*|/\*.*?\*/', '', text, flags=re.S)


def array(source, name):
    m = re.search(r'\b' + re.escape(name) + r'\s*\[[^\]]*\]\s*=\s*\{(.*?)\};', source, re.S)
    need(m is not None, 'fame_checker_array:' + name)
    return [v.strip() for v in without_comments(m[1]).split(',') if v.strip()]


def indexed_array(source, name):
    rows = array(source, name)
    result = {}
    for row in rows:
        m = re.fullmatch(r'\[(\w+)\]\s*=\s*(\w+)', row)
        need(m is not None and m[1] not in result, 'fame_checker_indexed_array:' + name)
        result[m[1]] = m[2]
    return result


def text_record(symbol, path, line, encoded):
    # Keep every escape, format control and placeholder verbatim; no modern text rewriting.
    controls = re.findall(r'\{[^{}]+\}|\\[npl]', encoded)
    return dict(symbol=symbol, sourcePath=path, sourceLine=line, encodedText=encoded, controlCodes=controls)


def parse_texts(sources):
    texts = {}
    path = 'data/text/fame_checker.inc'
    lines = sources[path].splitlines()
    name = None
    body = []
    line_number = 0
    def flush():
        if name is not None:
            need(body and ''.join(body).endswith('$'), 'fame_checker_text_terminator:' + name)
            need(name not in texts, 'fame_checker_duplicate_text:' + name)
            texts[name] = text_record(name, path, line_number, ''.join(body))
    for number, line in enumerate(lines, 1):
        label = re.match(r'^(\w+)::', line)
        if label:
            flush()
            name, body, line_number = label[1], [], number
        elif line.strip():
            m = re.fullmatch(r'\s*\.string "((?:[^"\\]|\\.)*)"\s*', line)
            need(m is not None, 'fame_checker_text_syntax:' + str(number))
            body.append(m[1])
    flush()
    path = 'src/strings.c'
    for m in re.finditer(r'const u8 (gFameChecker\w+)\[\] = _\("((?:[^"\\]|\\.)*)"\);', sources[path]):
        texts[m[1]] = text_record(m[1], path, sources[path][:m.start()].count('\n') + 1, m[2])
    return texts


def parse_constants(text):
    return {m[1]: int(m[2], 0) for m in re.finditer(r'^#define\s+(\w+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$', text, re.M)}


def extract( sources):
    source = sources['src/fame_checker.c']
    constants = parse_constants(sources['include/constants/fame_checker.h'])
    people = sorted(((key, value) for key, value in constants.items() if key.startswith('FAMECHECKER_')), key=lambda row: row[1])
    need([n for _, n in people] == list(range(16)), 'fame_checker_person_count')
    need([constants[key] for key in ['FCPICKSTATE_NO_DRAW', 'FCPICKSTATE_SILHOUETTE', 'FCPICKSTATE_COLORED']] == [0, 1, 2], 'fame_checker_states')
    texts = parse_texts(sources)
    names_quotes = array(source, 'sFameCheckerNameAndQuotesPointers')
    facts = array(source, 'sFameCheckerFlavorTextPointers')
    locations = array(source, 'sFlavorTextOriginLocationTexts')
    origins = array(source, 'sFlavorTextOriginObjectNameTexts')
    icons = array(source, 'sFameCheckerArrayNpcGraphicsIds')
    trainers = indexed_array(source, 'sTrainerIdxs')
    pictures = indexed_array(source, 'sFameCheckerTrainerPicIdxs')
    genders = indexed_array(source, 'sFameCheckerTrainerGenders_Unused')
    nontrainer_names = array(source, 'sNonTrainerNamePointers')
    nontrainers = {m[1]: int(m[2]) for m in re.finditer(r'#define (FAME_CHECKER_\w+)\s+\(FC_NONTRAINER_START \+ (\d+)\)', source)}
    nontrainer_pictures = {m[1]: m[2] for m in re.finditer(r'if \(fcPersonIdx == (FAMECHECKER_\w+)\)\s*\{\s*spriteId = CreateSprite\(&(\w+)', source)}
    need(len(names_quotes) == 32 and all(len(a) == 96 for a in [facts, locations, origins, icons]), 'fame_checker_table_size')
    trainer_constants = parse_constants(sources['include/constants/trainers.h'])
    graphic_constants = parse_constants(sources['include/constants/event_objects.h'])
    persons = []
    for symbol, index in people:
        trainer = trainers[symbol]
        if trainer in nontrainers:
            menu_name = texts[nontrainer_names[nontrainers[trainer]]]
        else:
            m = re.search(r'\[' + trainer + r'\]\s*=\s*\{.*?\.trainerName = _\("([^"]*)"\)', sources['src/data/trainers.h'], re.S)
            need(m is not None, 'fame_checker_trainer_name:' + trainer)
            menu_name = text_record(trainer + '.trainerName', 'src/data/trainers.h', sources['src/data/trainers.h'][:m.start()].count('\n') + 1, m[1])
        rows = []
        for fact in range(6):
            offset = index * 6 + fact
            need(icons[offset] in graphic_constants, 'fame_checker_icon_constant:' + icons[offset])
            rows.append(dict(index=fact, text=texts[facts[offset]], originLocation=texts[locations[offset]],
                             originObjectName=texts[origins[offset]], iconSymbol=icons[offset], iconSourceId=graphic_constants[icons[offset]]))
        persons.append(dict(id='pret:' + symbol, sourceSymbol=symbol, sourceIndex=index, menuName=menu_name,
                            trainerSymbol=trainer, trainerSourceId=trainer_constants.get(trainer),
                            trainerPictureSymbol=pictures[symbol], nonTrainerPictureSymbol=nontrainer_pictures.get(symbol),
                            unusedGenderSymbol=genders[symbol], nameText=texts[names_quotes[index]],
                            completionText=texts[names_quotes[index + 16]], facts=rows))
    events = []
    for path, text in sorted(sources.items()):
        if not (path.startswith('data/') and path.endswith('.inc')):
            continue
        label = None
        for number, line in enumerate(text.splitlines(), 1):
            label_match = re.match(r'^(\w+)::?', line)
            if label_match:
                label = label_match[1]
            m = re.match(r'\s*famechecker\s+([^@]+)', line)
            if not m:
                continue
            args = [a.strip() for a in m[1].split(',')]
            need(label is not None and len(args) in [2, 3], 'fame_checker_event_syntax')
            function = args[2] if len(args) == 3 else 'SetFlavorTextFlagFromSpecialVars'
            need(function in ['SetFlavorTextFlagFromSpecialVars', 'UpdatePickStateFromSpecialVar8005'], 'fame_checker_event_function')
            value = constants[args[1]] if args[1] in constants else int(args[1])
            need(args[0] in constants and value in range(6 if len(args) == 2 else 3), 'fame_checker_event_value')
            events.append(dict(sourcePath=path, scriptLabel=label, sourceLine=number, personIndex=constants[args[0]],
                               value=value, function=function, arguments=args, sourceMaps=[], directBindings=[],
                               explicitlyUnused='Unused' in label))
    maps = {p: json.loads(t) for p, t in sources.items() if p.endswith('/map.json')}
    for event in events:
        path_parts = event['sourcePath'].split('/')
        for path, data in maps.items():
            direct = []
            for group in ['object_events', 'bg_events', 'coord_events']:
                for index, e in enumerate(data.get(group, [])):
                    if e.get('script') == event['scriptLabel']:
                        direct.append(dict(sourceMap=data['id'], sourcePath=path, eventGroup=group, eventIndex=index,
                                           scriptLabel=event['scriptLabel'], flag=e.get('flag'), x=e.get('x'), y=e.get('y')))
            if direct or path_parts[1] == 'maps' and path_parts[2] == data['name']:
                event['sourceMaps'].append(data['id'])
                event['directBindings'] += direct
    # Validate complete fact provenance, including separately identified unused duplicates.
    collected = {(e['personIndex'], e['value']) for e in events if e['function'] == 'SetFlavorTextFlagFromSpecialVars' and not e['explicitlyUnused']}
    need(collected == {(p, f) for p in range(16) for f in range(6)}, 'fame_checker_collection_coverage')
    ui = [texts[key] for key in sorted(texts) if key.startswith('gFameCheckerText_')]
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

                pickStates={key: value for key, value in constants.items() if key.startswith('FCPICKSTATE_')},
                persons=persons, uiTexts=ui, collectionEvents=events)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--git-dir', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--lock', type=Path, default=Path(__file__).with_name('fame-checker.lock.json'))
    args = p.parse_args()
    lock, sources = read_sources(args.git_dir, args.lock)
    result = extract( sources)
    raw = json_bytes(result)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'catalog.json').write_bytes(raw)
    print(json.dumps(dict(profile=PROFILE, persons=len(result['persons']), facts=sum(len(p['facts']) for p in result['persons']),
                          collectionEvents=len(result['collectionEvents']), sourceFiles=len(lock['files']),
                          bytes=len(raw))))


if __name__ == '__main__':
    main()
