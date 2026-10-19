"""Project source preview eligibility and authenticated entry-duration flags.

Forest duration uses setworldmapflag's actual entry producer, not the similarly
named presentation registry flag. Only entry callback forms proven below are
accepted; no script execution or runtime source metadata is emitted.
"""
import re
from field_cave_art import preview_registry
from map_scripts import parse_scripts


def compile_metadata(src):
    registry = preview_registry(src)
    maps = {path.split('/')[2]: src.json(path) for path in sorted(src.tree)
            if path.startswith('data/maps/') and path.endswith('/map.json')}
    selected = {name: registry[events['region_map_section']] for name, events in maps.items()
                if events['region_map_section'] in registry}
    if not selected:
        raise ValueError('Empty map preview metadata')
    for path in sorted(src.tree):
        if ((path.startswith('data/maps/') and path.endswith('/scripts.inc')) or
                (path.startswith('data/scripts/') and path.endswith('.inc')) or path == 'data/event_scripts.s'):
            src.read(path, 'script-label-closure')
    blocks, aliases, _, _ = parse_scripts(src)
    flags = set(re.findall(r'^#define\s+(FLAG_\w+)\b', src.text('include/constants/flags.h'), re.M))

    def lines(label):
        label = aliases.get(label, label)
        if label not in blocks:
            raise ValueError('Missing forest entry callback: ' + label)
        return [text for row in blocks[label]['lines'] if (text := row['text'].split('@')[0].strip())]

    def forest_flag(name):
        callbacks = {}
        header = lines(name + '_MapScripts')
        if not header or header[-1] != '.byte 0':
            raise ValueError('Unsupported forest map callback table: ' + name)
        for text in header[:-1]:
            match = re.fullmatch(r'map_script (MAP_SCRIPT_\w+),\s*(\w+)', text)
            if not match or match[1] in callbacks:
                raise ValueError('Unsupported or duplicate forest entry callback: ' + name)
            callbacks[match[1]] = match[2]
        transition = callbacks.get('MAP_SCRIPT_ON_TRANSITION')
        transition = aliases.get(transition, transition)
        first = lines(transition)[0] if transition and lines(transition) else ''
        producer = re.fullmatch(r'setworldmapflag (FLAG_[A-Z0-9_]+)', first)
        if transition and not producer:
            raise ValueError('Forest OnTransition lacks an unconditional first visit flag: ' + name)
        flag = producer[1] if producer else None
        if flag is not None and flag not in flags:
            raise ValueError('Unknown forest entry visit flag: ' + flag)
        # These operations do not touch the first-visit latch. Branch targets
        # are followed transitively, including comma-free conditional macros.
        ordinary = {'setmetatile', 'setvar', 'addvar', 'clearflag', 'setflag',
                    'getplayerxy', 'setescapewarp', 'removeobject'}
        visiting, complete = set(), set()

        def check(label, allow_producer=False):
            label = aliases.get(label, label)
            if label == transition and not allow_producer:
                raise ValueError('Repeated forest entry visit flag: ' + name)
            if label in visiting:
                raise ValueError('Cyclic forest entry callback: ' + label)
            if label in complete:
                return
            visiting.add(label)
            body = lines(label)
            for index, text in enumerate(body):
                tokens = re.split(r'[,\s]+', text)
                op = tokens[0]
                if op == 'setworldmapflag':
                    if label != transition or index != 0 or text != first:
                        raise ValueError('Ambiguous forest entry visit flag: ' + name)
                elif op in ('call', 'goto') or re.fullmatch(r'(?:call|goto)_if_(?:set|unset|defeated|not_defeated|eq|ne|ge|gt|le|lt)', op):
                    check(tokens[-1])
                elif op == 'specialvar' and len(tokens) == 3 and tokens[2] == 'GetBattleOutcome':
                    pass
                elif op not in ordinary and op not in ('end', 'return'):
                    raise ValueError('Unsupported forest entry operation: ' + text)
                if op in ('end', 'return', 'goto'):
                    if index != len(body) - 1:
                        raise ValueError('Unsupported forest entry callback tail: ' + label)
                    break
            else:
                raise ValueError('Unterminated forest entry callback: ' + label)
            visiting.remove(label)
            complete.add(label)

        for kind, callback in callbacks.items():
            if kind not in {'MAP_SCRIPT_ON_TRANSITION', 'MAP_SCRIPT_ON_LOAD', 'MAP_SCRIPT_ON_RESUME'}:
                raise ValueError('Unsupported forest entry callback kind: ' + kind)
            check(callback, kind == 'MAP_SCRIPT_ON_TRANSITION')
        return flag

    return {name: dict(type=row['type'].removeprefix('MPS_TYPE_').lower(),
                       visitFlag=row['flagId'] if row['type'] == 'MPS_TYPE_CAVE' else forest_flag(name))
            for name, row in selected.items()}
