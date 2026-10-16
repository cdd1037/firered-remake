"""Compile original u8 item effect arrays using a restricted expression reader."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import ast
import operator
from pathlib import Path
import re


def read_effects(raw):
    raw = Path(raw)
    header = (raw / 'include/constants/item_effects.h').read_text(encoding='utf-8')
    source = (raw / 'src/data/pokemon/item_effects.h').read_text(encoding='utf-8')
    return read_effects_text(header, source)


def read_effects_text(header, source):
    """Compile already-read source text, including item-to-array bindings."""
    def uncomment(text):
        return re.sub(r'/\*.*?\*/|//[^\n]*', '', text, flags=re.S)
    constants = dict(re.findall(r'^#define (ITEM\w+)\s+([^\n]+)', uncomment(header), re.M))
    operators = {ast.BitOr: operator.or_, ast.LShift: operator.lshift,
                 ast.Add: operator.add, ast.Sub: operator.sub}
    def expression(text, resolving=()):
        text = re.sub(r'\(u8\)', '', text).strip()
        def visit(node):
            if isinstance(node, ast.Constant) and type(node.value) is int:
                return node.value
            if isinstance(node, ast.Name) and node.id in constants and node.id not in resolving:
                return expression(constants[node.id], (*resolving, node.id))
            if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.USub):
                return -visit(node.operand)
            if isinstance(node, ast.BinOp) and type(node.op) in operators:
                return operators[type(node.op)](visit(node.left), visit(node.right))
            raise ValueError('Unsupported item effect expression: ' + text)
        return visit(ast.parse(text, mode='eval').body)
    joined = uncomment(source.replace('\\\n', ' '))
    macros = {}
    for name, parameter, body in re.findall(r'^#define (\w+)(?:\((\w+)\))?\s+([^\n]+)', joined, re.M):
        macros[name] = (parameter, body)
    arrays = {}
    for name, size, body in re.findall(r'static const u8 (sItemEffect_\w+)\[(\d+)\] = \{(.*?)\};', joined, re.S):
        for macro, (parameter, replacement) in macros.items():
            if parameter:
                body = re.sub(r'\b' + macro + r'\((\d+)\)',
                    lambda match: re.sub(r'\b' + parameter + r'\b', match[1], replacement), body)
            else:
                body = re.sub(r'\b' + macro + r'\b', replacement, body)
        output = [0] * int(size); seen = set()
        for row in body.split(','):
            if not row.strip():
                continue
            match = re.fullmatch(r'\s*\[([^]]+)\]\s*=\s*(.+?)\s*', row, re.S)
            if not match:
                raise ValueError('Unsupported item effect initializer: ' + name + ': ' + row)
            index = expression(match[1]); value = expression(match[2])
            if index in seen or not 0 <= index < len(output) or not -128 <= value <= 255:
                raise ValueError('Invalid item effect slot: ' + name)
            seen.add(index); output[index] = value & 255
        if name in arrays:
            raise ValueError('Duplicate item effect: ' + name)
        arrays[name] = output
    rows = re.findall(r'\[(ITEM_\w+) - ITEM_POTION\]\s*=\s*(sItemEffect_\w+)', joined)
    bindings = dict(rows)
    if not arrays or not bindings or len(bindings) != len(rows):
        raise ValueError('Empty or duplicate item effect bindings')
    if any(name not in arrays for name in bindings.values()):
        raise ValueError('Unresolved item effect binding')
    return {item: arrays[name] for item, name in bindings.items()}
