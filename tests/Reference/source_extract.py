"""Shared test-only textual extraction; no upstream code is imported or executed.

Callers select their own pin file. Older Weather/Bg1Overlay extraction boundaries
remain explicit options, independent of the production importers.
"""
import hashlib
import re

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'

def block(text, opening):
    start = opening
    depth = 0
    # Requested source functions have no braces inside strings/comments.
    for end in range(start, len(text)):
        if text[end] == '{': depth += 1
        elif text[end] == '}':
            depth -= 1
            if depth == 0: return text[start:end+1], end+1
    raise ValueError('Unclosed source block')

def function(text, name, *, signed_byte=True):
    returns = 'void|bool8|u8|u16|s16|u32|s32' + ('|s8' if signed_byte else '')
    m = re.search(r'(?m)^(?:static )?(?:' + returns + r')\s+' + re.escape(name) + r'\([^;]*?\)\s*\{', text)
    if not m: raise ValueError('Missing function: ' + name)
    _, end = block(text, text.index('{', m.start()))
    return text[m.start():end] + '\n'

def array(text, name):
    m = re.search(r'(?m)^(?:static )?const\s+[^;=]+\b' + re.escape(name) + r'\s*\[[^;=]*=\s*\{', text)
    if not m: raise ValueError('Missing array: ' + name)
    _, end = block(text, text.index('{', m.start()))
    return text[m.start():text.index(';', end)+1] + '\n'

def enum_containing(text, member):
    for m in re.finditer(r'\benum\s*\{', text):
        body, end = block(text, text.index('{', m.start()))
        if re.search(r'\b'+re.escape(member)+r'\b', body): return 'enum\n' + body + ';\n'
    raise ValueError(member)

def macros(headers, names, *, require_separator=False):
    separator = r'\s+' if require_separator else r'[ \t]*'
    definitions = {}
    for text in headers:
        for line in text.splitlines():
            m = re.match(r'#define\s+([A-Za-z_0-9]+)(\([^)]*\))?' + separator + r'(.+)', line)
            if m: definitions[m[1]] = (line, m[2] or '', m[3])
    found, ordered = set(), []
    def take(name):
        if name in found: return
        if name not in definitions: raise ValueError('Missing macro: ' + name)
        found.add(name)
        line, params, value = definitions[name]
        for token in re.findall(r'\b[A-Za-z_][A-Za-z_0-9]*\b', value):
            if token in definitions and token not in params: take(token)
        ordered.append(line)
    for name in names: take(name)
    return '\n'.join(ordered) + '\n'

def identity(root, paths):
    return {str(path): {'sha256': hashlib.sha256((root/path).read_bytes()).hexdigest(),
        'url':'https://github.com/pret/pokefirered/blob/'+COMMIT+'/'+str(path)} for path in paths}

def verify(root, paths, pin_file):
    import json
    from pathlib import Path
    pins=json.loads(Path(pin_file).read_text())
    for path in paths:
        actual=hashlib.sha256((root/path).read_bytes()).hexdigest()
        if path not in pins or actual != pins[path]:
            raise ValueError('Pinned source mismatch: '+path)
