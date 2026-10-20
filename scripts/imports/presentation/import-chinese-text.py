#!/usr/bin/env python3
"""Project pinned Chinese declarations into the existing sparse text-pack groups.

Only display text is emitted. English generated catalogs own membership; original
rule definitions, encoded seed inputs and images never enter the language pack.
"""
import argparse
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from project_paths import PROJECT_ROOT
from chinese_catalogs import collect_catalogs, collect_ui_groups, _decode
from source_text_collector import (collect_declarations, parse_charmap,
    canonicalize_controls, apply_declared_transform)

HAN = re.compile(r'[\u3400-\u4dbf\u4e00-\u9fff]')

def read(path):
    return json.loads(path.read_text(encoding='utf-8'))

def build(root):
    recipe = read(root / 'scripts/recipes/config/localization-zh-Hans.source.json')
    chinese = root / 'content/raw/PretFRLG' / recipe['commit'][:8]
    english = root / 'content/raw/pokefirered' / recipe['rulesCommit'][:8]
    declarations = collect_declarations(chinese)
    if any(declarations.diagnostics[key] for key in ('issues', 'conflicts', 'unknownConditions')):
        raise ValueError('Chinese source declarations are ambiguous: ' + str(declarations.diagnostics))
    groups, originals, coverage = {}, {}, {}
    for collected in (collect_catalogs(root, chinese), collect_ui_groups(root, chinese, declarations)):
        if collected['warnings']:
            raise ValueError('Missing catalog inputs: ' + str(collected['warnings']))
        groups.update(collected['groups']); originals.update(collected['originals'])
        coverage.update(collected['coverage'])
    generated = root / 'content/generated'
    originals['story'] = read(generated / 'data/story.json')['TextBank']
    originals['battle'] = read(generated / 'battle/text.json')
    transforms = {row['symbol']: row for row in read(root / 'tools/import-story/recipe.json')['supplementalTexts']}
    for group in ('story', 'battle'):
        groups[group] = {}
        missing = set(originals[group]) - declarations.texts.keys() - declarations.excluded.keys()
        if missing:
            raise ValueError('Missing Chinese declarations: ' + str(sorted(missing)))
        for symbol, original in originals[group].items():
            if symbol not in declarations.texts: continue
            value = declarations.texts[symbol]
            if group == 'story' and symbol in transforms:
                value = apply_declared_transform(value, transforms[symbol])
            if value != original and HAN.search(value): groups[group][symbol] = value
    zh_map, en_map = parse_charmap(chinese / 'charmap.txt'), parse_charmap(english / 'charmap.txt')
    aliases = 0
    for group, rows in groups.items():
        for key, value in rows.items():
            result = canonicalize_controls(value, originals[group][key], zh_map, en_map)
            rows[key] = result['text']; aliases += len(result['changes'])
    # Small reviewed authored corrections, never an English catalog copy.
    corrections = root / 'scripts/recipes/localization/zh-Hans'
    for path in sorted(corrections.glob('*.json')):
        group = path.stem
        if group not in originals: raise ValueError('Unknown correction group: ' + group)
        for key, value in read(path).items():
            if key not in originals[group]: raise ValueError('Unknown correction ID: ' + group + '/' + key)
            if not isinstance(value, str) or not value.strip(): raise ValueError('Empty correction: ' + key)
            groups[group][key] = value
    # These existing consumer catalogs store decoded display lines, unlike script banks.
    for group in ('species-descriptions', 'item-descriptions', 'machine-descriptions',
                  'summary-moves', 'summary-abilities', 'summary-memos'):
        groups[group] = {key: _decode(value) for key, value in groups[group].items()}
    files = {}
    for group, rows in sorted(groups.items()):
        files['source/' + group + '.json'] = rows
    for path in sorted((root / 'src/Game.Content/Text/zh-Hans').glob('*.json')):
        values, baseline = read(path), read(root / 'src/Game.Content/Text/en' / path.name)
        if values.keys() != baseline.keys(): raise ValueError('Authored group keys differ: ' + path.name)
        files[path.name] = values
    report = {'groups': {group: {'total': len(originals[group]), 'overridden': len(rows),
        'fallback': sorted(originals[group].keys() - rows.keys())} for group, rows in groups.items()},
        'canonicalizedAliases': aliases, 'authoredGroups': len(files)-len(groups)}
    return files, report

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--report', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    root = args.root.resolve(); output = args.out or root / 'content/generated/localization/zh-Hans'
    files, report = build(root)
    for relative, value in files.items():
        data = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + '\n'
        path = output / relative
        if args.check:
            if not path.is_file() or read(path) != value: raise ValueError('Generated text differs: ' + str(path))
        else:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_text(data, encoding='utf-8')
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'sourceGroups': len(report['groups']), 'authoredGroups': report['authoredGroups'],
        'overrides': sum(row['overridden'] for row in report['groups'].values()),
        'canonicalizedAliases': report['canonicalizedAliases']}, ensure_ascii=False))

if __name__ == '__main__': main()
