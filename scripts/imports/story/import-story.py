#!/usr/bin/env python3
"""Compile the explicit current-game source recipe into executable typed story IR.

Raw pret data is read only here and by the import-only C# compiler. The recipe
contains source locations and transformations, never copied source or text.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse, ast, json, os, re, subprocess
from pathlib import Path
from story_menus import extract_menus

ROOT = _PROJECT_ROOT
RAW = ROOT / 'content/raw/pokefirered/037335f4'

def read(path):
    return (RAW / path).read_text(encoding='utf-8')

def assemble_texts(paths):
    bank = {}
    for path in paths:
        label, value = None, ''
        for line in read(path).splitlines():
            line = line.strip()
            match = re.fullmatch(r'([A-Za-z_][A-Za-z0-9_]*):{1,2}(?:\s*@.*)?', line)
            if match:
                label, value = match[1], ''
                continue
            if label and (line.startswith('.string') or line.startswith('.braille')):
                part = re.fullmatch(r'\.(?:string|braille)\s+"(.*)"', line)
                if not part:
                    raise ValueError('Unsupported text directive: ' + label)
                value += part[1]
                if value.endswith('$'):
                    if label in bank:
                        raise ValueError('Duplicate source text: ' + label)
                    bank[label], label = value, None
    return bank

def constants(symbols):
    values = json.loads((ROOT / 'content/generated/data/constants.json').read_text())
    expressions = {}
    for path in ['include/pokemon_storage_system.h', 'include/pokemon_storage_system_internal.h',
                 'include/constants/flags.h', 'include/constants/coins.h', 'include/battle.h']:
        source = re.sub(r'/\*.*?\*/', '', read(path), flags=re.S)
        for name, expression in re.findall(r'^#define[ \t]+(\w+)[ \t]+([^\n]+)', source, re.M):
            expressions[name] = expression.split('//')[0].strip()
        for body in re.findall(r'\benum\s*\{([^}]+)\}', source, re.S):
            previous = '-1'
            for item in body.split(','):
                item = item.split('//')[0].strip()
                match = re.fullmatch(r'(\w+)(?:\s*=\s*(.+))?', item)
                if match:
                    expressions[match[1]] = match[2] or '(' + previous + ') + 1'
                    previous = match[1]
    for name, expression in re.findall(r'^\s*(\w+)\s*=\s*([^\n]+)', read('asm/macros/event.inc'), re.M):
        expressions[name] = expression.split('@')[0].strip()
    def resolve(name, seen=()):
        if name in seen:
            raise ValueError('Cyclic constant: ' + name)
        if name not in expressions:
            return values[name]
        return evaluate(ast.parse(expressions[name], mode='eval').body, seen + (name,))
    def evaluate(node, seen=()):
        if isinstance(node, ast.Constant) and type(node.value) is int: return node.value
        if isinstance(node, ast.Name): return resolve(node.id, seen)
        if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.USub): return -evaluate(node.operand, seen)
        if isinstance(node, ast.BinOp):
            left, right = evaluate(node.left, seen), evaluate(node.right, seen)
            if isinstance(node.op, ast.Add): return left + right
            if isinstance(node.op, ast.Sub): return left - right
            if isinstance(node.op, ast.Mult): return left * right
            if isinstance(node.op, ast.LShift): return left << right
            if isinstance(node.op, ast.BitOr): return left | right
        raise ValueError('Unsupported source integer expression: ' + ast.dump(node))
    for name in symbols:
        if name not in values or name.startswith(('WALLPAPER_', 'OPTION_', 'MOVE_TARGET_')):
            if name in expressions: values[name] = resolve(name)
            elif name.startswith('(MAX_COINS'): values[name] = evaluate(ast.parse(name, mode='eval').body)
    for path in ['PalletTown_RivalsHouse','PalletTown_PlayersHouse_1F','ViridianCity_Mart']:
        for index, actor in enumerate(json.loads(read('data/maps/' + path + '/map.json'))['object_events'], 1):
            if actor.get('local_id') in symbols: values[actor['local_id']] = index
    return {name: values[name] for name in symbols}

def prepare():
    recipe = json.loads((ROOT / 'tools/import-story/recipe.json').read_text())
    bank = assemble_texts(recipe['textPaths'])
    for entry in recipe['supplementalTexts']:
        matches = re.findall(r'const u8 ' + re.escape(entry['symbol']) + r'\[\]\s*=\s*_\("((?:\\.|[^"\\])*)"\);', read(entry['path']))
        if not matches: raise ValueError('Missing C text: ' + entry['symbol'])
        # The original opening UI loader chooses the first English declaration.
        text = matches[0]
        if entry.get('removePause'): text = text.replace('{PAUSE_UNTIL_PRESS}', '')
        if entry.get('decodeNewlines'): text = text.replace(r'\n', '\n')
        if entry.get('escapeNewlines'): text = text.replace('\n', r'\n')
        if entry.get('terminate'): text = text.rstrip('$') + '$'
        bank[entry['symbol']] = text
    repel = json.loads((ROOT / 'content/generated/data/services/early-repel.json').read_text())
    for entry in repel['texts']:
        bank[entry['symbol']] = entry['text'].replace('{PAUSE_UNTIL_PRESS}', '').replace('\n', r'\n').rstrip('$') + '$'
    const = constants(recipe['constantSymbols'])
    pokemon = json.loads((ROOT / 'content/generated/data/pokemon/starters.json').read_text())
    # The starter package defines the initial roster; later catalogs extend it at runtime.
    species_ids = {const[s['id']['value'].removeprefix('pret:')] for s in pokemon['species']}
    species_names = {str(const[name]): text for name, text in re.findall(r'\[(SPECIES_[A-Z0-9_]+)\]\s*=\s*_\("([^"]+)"\)', read('src/data/text/species_names.h')) if name in const and const[name] in species_ids}
    keyboard = re.search(r'static const u8 sKeyboardChars[^=]+=(.*?);', read('src/naming_screen.c'), re.S)
    chars = ''.join(sorted(set(''.join(re.findall(r'__\("([^"]*)"\)', keyboard[1])))))
    move_names = {'pret:' + name: text for name, text in re.findall(r'\[(MOVE_[A-Z0-9_]+)\]\s*=\s*_\("([^"]*)"\)', read('src/data/text/move_names.h'))}
    menus = extract_menus(RAW)
    def pascal(value):
        if isinstance(value, dict): return {key[0].upper()+key[1:]: pascal(item) for key,item in value.items()}
        if isinstance(value, list): return [pascal(item) for item in value]
        return value
    return dict(Sources=[dict(Path=e['path'], Text=read(e['path']), SourceRef=e['sourceRef']) for e in recipe['scripts']],
                Constants=const, TextBank=bank, Menus={m['id']:pascal(m) for m in menus},
                StarterSpeciesNames=species_names, NicknameCharacters=chars, MoveNames=move_names)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', default=os.environ.get('DOTNET', 'dotnet'))
    parser.add_argument('--output-root', type=Path, default=ROOT, help='Write compiled IR to an isolated root for review')
    parser.add_argument('--check', action='store_true', help='Verify the checked-in IR without writing it')
    args = parser.parse_args()
    data = prepare()
    project = ROOT / 'tools/import-story/import-story.csproj'
    subprocess.run([args.dotnet, 'build', str(project), '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-v:quiet'], check=True)
    subprocess.run([args.dotnet, str(project.parent / 'bin/Debug/net10.0/import-story.dll'), str(args.output_root.resolve())] + (['--check'] if args.check else []), input=json.dumps(data), text=True, check=True)

if __name__ == '__main__': main()
