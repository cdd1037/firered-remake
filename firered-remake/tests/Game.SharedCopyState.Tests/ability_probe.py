#!/usr/bin/env python3
"""Compile authenticated original ability exchange commands against authored register shims."""
import hashlib, json, re, subprocess, sys
from pathlib import Path
root, out = map(Path, sys.argv[1:])
out.mkdir(parents=True, exist_ok=False)
here = Path(__file__).resolve().parent
pin = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
paths = ['src/battle_script_commands.c', 'src/battle_util.c', 'data/battle_scripts_1.s',
         'include/constants/abilities.h', 'include/constants/battle.h']
texts, hashes = {}, {}
for path in paths:
    data = (root / path).read_bytes()
    assert data == subprocess.check_output(['git', 'show', pin + ':' + path], cwd=root), path
    texts[path] = data.decode()
    hashes[path] = hashlib.sha256(data).hexdigest()
names = ['Cmd_trycopyability', 'Cmd_tryswapabilities']
fragments = [re.search(r'^static void ' + name + r'\(void\)\n\{.*?^\}', texts[paths[0]], re.M | re.S).group() for name in names]
constants = '\n'.join(line for line in texts[paths[4]].splitlines() if line.startswith('#define MOVE_RESULT_'))
code = (here / 'ability-prefix.h').read_text() + '\n' + texts[paths[3]] + '\n' + constants + '\n' + '\n'.join(fragments) + '\n' + (here / 'ability-main.c').read_text()
(out / 'ability-oracle.c').write_text(code)
for suffix, flags in [('optimized', ['-O2']), ('ubsan', ['-O1', '-fsanitize=undefined', '-fno-sanitize-recover=all'])]:
    subprocess.run(['cc', '-std=c11', '-Wall', '-Wextra', '-Werror', *flags, str(out / 'ability-oracle.c'), '-o', str(out / suffix)], check=True)
    result = subprocess.check_output([str(out / suffix)])
    assert len(json.loads(result)['exchange']) == 648
    (out / (suffix + '.json')).write_bytes(result)
assert (out / 'optimized.json').read_bytes() == (out / 'ubsan.json').read_bytes()
scripts = texts[paths[2]]
for label, command in [('RolePlay', 'trycopyability'), ('SkillSwap', 'tryswapabilities')]:
    body = re.search(r'BattleScript_Effect' + label + r'::\n.*?(?=\nBattleScript_)', scripts, re.S).group()
    assert body.index('ppreduce') < body.index('accuracycheck') < body.index(command) < body.index('BattleScript_MoveEnd')
    assert 'jumpifsubstituteblocks' not in body
(out / 'manifest.json').write_text(json.dumps({'passed': True, 'sourceCommit': pin, 'sourceHashes': hashes,
    'fragmentHashes': {name: hashlib.sha256(body.encode()).hexdigest() for name, body in zip(names, fragments)},
    'authoredHarnessHashes': {name: hashlib.sha256((here / name).read_bytes()).hexdigest() for name in ['ability-prefix.h', 'ability-main.c']},
    'cases': 648, 'optimizedEqualsUbsan': True}, indent=2) + '\n')
print('PASS 648 unchanged source ability exchange outcomes')
