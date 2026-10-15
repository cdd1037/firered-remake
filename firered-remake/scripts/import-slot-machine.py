#!/usr/bin/env python3
"""Recover deterministic FireRed slot rules from pinned Git objects. No upstream script execution.
REFERENCE OUTPUT [--check]. Generated data and copied original source stay outside source control.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-slot-machine-rules-v1'
BASE = 'content/generated/slot-machine/'
RAW = 'content/raw/pokefirered/037335f4/'
TABLES = ['sSecondReelBiasCheckIndices', 'sThirdReelBiasCheckIndices', 'sRowAttributes',
          'sReelBiasChances', 'sReelIconAnimByReelAndPos', 'sPayoutTable']
FUNCTIONS = ['InitSlotMachineState', 'MainTask_SlotsGameLoop', 'MainTask_ConfirmExitGame',
             'MainTask_NoCoinsGameOver', 'MainTask_ShowHelp', 'MainTask_DarnNoPayout',
             'MainTask_WinHandlePayout', 'Task_SpinReels', 'GetNextReelPosition',
             'StopReel1', 'StopReel2', 'StopReel3', 'TwoReelBiasCheck', 'OneReelBiasCheck',
             'TestReelIconAttribute', 'ReelIconToPayoutRank', 'CalcSlotBias', 'ResetMachineBias', 'CalcPayout']

def need(value, message):
    if not value:
        raise ValueError(message)

def encoded(value):
    return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()


def generate(reference):
    def git(*args):
        return subprocess.check_output(['git', *args], cwd=reference)
    sources = {}
    for path in ['src/slot_machine.c', 'src/field_specials.c', 'src/random.c', 'src/coins.c',
                 'src/malloc.c', 'data/maps/CeladonCity_GameCorner/scripts.inc',
                 'data/maps/CeladonCity_GameCorner/map.json']:
        sources[path] = git('show', COMMIT + ':' + path)
    source = sources['src/slot_machine.c'].decode()
    symbols = {}
    for prefix in ['ICON_', 'PAYOUT_']:
        match = re.search(r'enum \{\n\s+' + prefix + r'.*?\n\};', source, re.S)
        need(match is not None, 'Missing source enum ' + prefix)
        symbols.update({name: i for i, name in enumerate(re.findall(r'\b(' + prefix + r'[A-Z0-9]+)\s*,', match[0]))})
    def declaration(text, symbol):
        matches = list(re.finditer(r'^static const [^\n]+\b' + symbol + r'[^=]*=\s*\{.*?^\};', text, re.M | re.S))
        need(len(matches) == 1, 'Exact table declaration: ' + symbol)
        return matches[0]
    def parse_table(text, symbol):
        body = declaration(text, symbol)[0].split('=', 1)[1].rstrip(';')
        body = re.sub(r'//[^\n]*', '', body)
        body = re.sub(r'\[[A-Z_0-9]+\]\s*=', '', body)
        for name, value in symbols.items():
            body = re.sub(r'\b' + name + r'\b', str(value), body)
        body = re.sub(r'0x[0-9a-fA-F]+', lambda m: str(int(m[0], 16)), body)
        body = body.replace('{', '[').replace('}', ']')
        body = re.sub(r',\s*\]', ']', body)
        return json.loads(body)
    tables = {name: parse_table(source, name) for name in TABLES}
    tables['sSlotMachineIndices'] = parse_table(sources['src/field_specials.c'].decode(), 'sSlotMachineIndices')
    need([len(r) for r in tables['sReelIconAnimByReelAndPos']] == [21, 21, 21], 'Three original reels')
    need([len(r) for r in tables['sReelBiasChances']] == [7] * 6, 'Six bias tables')
    need(len(tables['sSlotMachineIndices']) == 22, 'Twenty-two machine selection entries')
    need(tables['sPayoutTable'] == [0, 2, 6, 8, 15, 100, 300], 'Original payouts')
    need([r[-1] for r in tables['sRowAttributes']] == [3, 2, 1, 2, 3], 'Original enabled lines')
    rules = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, tables=tables)
    refs = []
    for name in FUNCTIONS:
        match = re.search(r'^static [^\n;]+\b' + name + r'\([^;\n]*\)\n\{.*?^\}', source, re.M | re.S)
        need(match is not None, 'Exact function declaration: ' + name)
        refs.append(dict(symbol=name, startLine=source.count('\n', 0, match.start()) + 1,
                         endLine=source.count('\n', 0, match.end()) + 1))
    map_data = json.loads(sources['data/maps/CeladonCity_GameCorner/map.json'])
    machines = [entry for entry in map_data['bg_events'] if re.fullmatch(r'CeladonCity_GameCorner_EventScript_SlotMachine\d+', entry.get('script', ''))]
    need(len(machines) == 19, 'Nineteen usable machine map bindings; IDs 4, 15, 18 are unusable')
    facts = dict(sourceCommit=COMMIT,  profile=PROFILE,
                 functions=refs, machineBindings=machines,
                 boundaries=['original heap-dependent initial machineBias/biasCooldown must be supplied explicitly',
                             'StopReel1 PAYOUT_NONE negative table index is rejected until original ROM layout is authenticated',
                             'presentation task completion and input timing belong to the Application/Godot integration'])
    inputs = [dict(path=p)
              for p, b in sorted(sources.items())]
    files = {BASE + 'rules.json': encoded(rules), BASE + 'source-contract.json': encoded(facts),
             BASE + 'source-input-hashes.json': encoded(dict(sourceCommit=COMMIT,  inputs=inputs))}
    files.update({RAW + p: b for p, b in sources.items()})
    return files

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    files = generate(args.reference)
    for path, data in files.items():
        destination = args.output / path
        need(not destination.is_symlink(), 'Refuse symlink output: ' + path)
        if args.check:
            need(destination.read_bytes() == data, 'Regeneration differs: ' + path)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    print(json.dumps(dict(checked=args.check, outputs=len(files))))
