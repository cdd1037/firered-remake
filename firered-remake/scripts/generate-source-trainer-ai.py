#!/usr/bin/env python3
"""Assemble the three pinned FireRed trainer scripts. Reads, never executes upstream code."""
import ast,  json, operator, re, struct, subprocess, sys
from pathlib import Path

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'

def assemble(source):
    sources = {}
    def read(path):
        text = subprocess.check_output(['git', '-C', str(source), 'show', COMMIT + ':' + path]).decode()
        sources[path] = text
        return text
    defines = {}
    for file in ['battle', 'battle_ai', 'abilities', 'items', 'moves', 'battle_move_effects', 'hold_effects', 'pokemon']:
        for line in read('include/constants/' + file + '.h').splitlines():
            m = re.match(r'#define\s+(\w+)\s+(.+)', line)
            if m: defines[m[1]] = m[2].split('//')[0].strip()
    def value(expression):
        node = ast.parse(expression, mode='eval').body
        def evaluate(n):
            if isinstance(n, ast.Constant) and isinstance(n.value, int): return n.value
            if isinstance(n, ast.Name): return value(defines[n.id])
            if isinstance(n, ast.UnaryOp): return {ast.USub: operator.neg, ast.Invert: operator.invert, ast.UAdd: operator.pos}[type(n.op)](evaluate(n.operand))
            if isinstance(n, ast.BinOp):
                return {ast.Add: operator.add, ast.Sub: operator.sub, ast.Mult: operator.mul, ast.Div: operator.floordiv,
                    ast.FloorDiv: operator.floordiv, ast.BitOr: operator.or_, ast.BitAnd: operator.and_, ast.LShift: operator.lshift,
                    ast.RShift: operator.rshift}[type(n.op)](evaluate(n.left), evaluate(n.right))
            raise ValueError(expression)
        return evaluate(node)
    macros = {}; active = None
    for raw in read('asm/macros/battle_ai_script.inc').splitlines():
        line = raw.split('@')[0].strip()
        if line.startswith('.macro '):
            name, *args = line[7:].split(maxsplit=1)
            params = [x.strip().split(':')[0] for x in args[0].split(',')] if args else []
            active = (params, []); macros[name] = active
        elif line == '.endm': active = None
        elif active is not None and line: active[1].append(line)
    script = read('data/battle_ai_scripts.s')
    selected = script[script.index('AI_CheckBadMove::'):script.index('AI_SetupFirstTurn::')]
    output = bytearray(); labels = {}; fixups = []; lines = {}
    def emit(line, source_line):
        line = line.split('@')[0].strip()
        if not line: return
        if line.endswith(':'):
            labels[line.rstrip(':')] = len(output); return
        name, *rest = line.split(maxsplit=1)
        args = [x.strip() for x in rest[0].split(',')] if rest else []
        if name in macros:
            params, body = macros[name]; assert len(params) == len(args), line
            substitutions = dict(zip(params, args))
            for inner in body:
                emit(re.sub(r'\\(\w+)', lambda m: substitutions[m[1]], inner), source_line)
        elif name in ['.byte', '.2byte', '.4byte']:
            width = {'.byte': 1, '.2byte': 2, '.4byte': 4}[name]
            for arg in args:
                if re.fullmatch(r'[A-Za-z_]\w*', arg) and arg not in defines:
                    assert width == 4, arg
                    fixups.append((len(output), arg)); output.extend(b'\0' * 4)
                else: output.extend((value(arg) & ((1 << (8 * width)) - 1)).to_bytes(width, 'little'))
        elif name == '.space': output.extend(b'\0' * value(args[0]))
        elif name == '.align': output.extend(b'\0' * (-len(output) % (1 << value(args[0]))))
        else: raise ValueError('Unknown original macro: ' + line)
    first_line = script[:script.index('AI_CheckBadMove::')].count('\n') + 1
    for index, line in enumerate(selected.splitlines(), first_line):
        if line.strip() and not line.strip().startswith(('@', '.')) and not line.strip().endswith(':'):
            lines[len(output)] = index
        emit(line, index)
    for offset, label in fixups: output[offset:offset+4] = struct.pack('<I', labels[label])
    constants = {k: value(k) for k in defines if k.startswith(('EFFECT_', 'ABILITY_', 'HOLD_EFFECT_', 'TYPE_', 'STATUS1_', 'STATUS2_', 'STATUS3_', 'SIDE_STATUS_', 'MON_', 'STAT_')) and '(' not in k
        and k not in ['STAT_STAGE_MIN', 'STAT_STAGE_MAX']}
    items = json.loads(read('src/data/items.json'))['items']
    item_values = {item['itemId']: (value(item['itemId']), value(item['holdEffect'])) for item in items}
    return dict(code=output, labels=labels, lines=lines, constants=constants, items=item_values, script=selected)

def generate(source, destination):
    p = assemble(source)
    payload = dict(sourceCommit=COMMIT, program=list(p['code']), entries={x: p['labels'][x] for x in ['AI_CheckBadMove', 'AI_CheckViability', 'AI_TryToFaint']}, constants=p['constants'], items={k: dict(id=v[0], effect=v[1]) for k, v in p['items'].items()})
    target=Path(destination);target.parent.mkdir(parents=True,exist_ok=True)
    data=(json.dumps(payload,sort_keys=True,separators=(',',':'))+'\n').encode()
    target.write_bytes(data)
    print(json.dumps(dict(source=COMMIT, programBytes=len(p['code']), viabilityDispatches=len(re.findall('^\\s*if_effect ', p['script'].split('AI_CheckViability::')[1].split('\n\tend')[0], re.M)))))

if __name__ == '__main__': generate(*sys.argv[1:])
