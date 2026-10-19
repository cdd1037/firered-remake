"""Pinned item business definitions shared by source compilers.

ItemDefinition is a compiler value: consumers can inspect its business fields,
while JSON emission writes its stable identity. Only the item-catalog compiler
emits the underlying definition. Regional declarations retain their own effect
capabilities, quantities, prices for transactions, and availability.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from collections.abc import Mapping
from dataclasses import dataclass
from functools import lru_cache
import json
from pathlib import Path
import re

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
RAW = Path('content/raw/pokefirered/037335f4')

@dataclass(frozen=True)
class ItemDefinition(Mapping):
    fields: dict

    def __getitem__(self, key):
        return self.fields[key]

    def __iter__(self):
        return iter(self.fields)

    def __len__(self):
        return len(self.fields)

    @property
    def identity(self):
        return self.fields['id']['value']


def item_json(value):
    if isinstance(value, ItemDefinition):
        return value.identity
    raise TypeError('Unsupported item compiler JSON value: ' + type(value).__name__)


@lru_cache(maxsize=None)
def definitions(project_root=ROOT):
    raw = Path(project_root) / RAW
    return definitions_from_source(lambda path: (raw / path).read_bytes())


def definitions_from_source(source):
    """Parse item business values from the caller's source closure, without IO fallback."""
    read = source if callable(source) else source.__getitem__
    def text(path):
        value = read(path)
        return value.decode('utf-8') if isinstance(value, bytes) else value
    constants = text('include/constants/items.h')
    ids = {name: int(number) for name, number in re.findall(r'^#define (ITEM_\w+)\s+(\d+)\b', constants, re.M)}
    secondary = {name: int(number) for name, number in re.findall(r'^#define ((?:OLD|GOOD|SUPER)_ROD)\s+(\d+)\b', constants, re.M)}
    rows = json.loads(text('src/data/items.json'))['items']
    result = {}
    for row in rows:
        symbol = row['itemId']
        if symbol == 'ITEM_NONE':
            continue
        if symbol in result or symbol not in ids:
            raise ValueError('Invalid item identity: ' + symbol)
        result[symbol] = ItemDefinition(dict(id=dict(value='pret:' + symbol), sourceItemSymbol=symbol,
            sourceItemId=ids[symbol], name=row['english'], price=row['price'], importance=row['importance'],
            registrability=row['registrability'], pocket=row['pocket'], type=str(row['type']),
            holdEffect=row['holdEffect'], holdEffectParam=row['holdEffectParam'],
            fieldUseFunction=row['fieldUseFunc'], battleUsage=row['battleUsage'],
            battleUseFunction=row['battleUseFunc'], secondaryId=secondary[row['secondaryId']] if isinstance(row['secondaryId'], str) else row['secondaryId'],
            canToss=row['importance'] == 0, canSell=row['price'] != 0, canGive=row['importance'] == 0))
    if len(result) != 307 or len({value['sourceItemId'] for value in result.values()}) != 307:
        raise ValueError('Expected all 307 pinned source item definitions')
    return result


def item_definition(symbol, project_root=ROOT):
    symbol = symbol.removeprefix('pret:')
    try:
        return definitions(Path(project_root).resolve())[symbol]
    except KeyError as error:
        raise ValueError('Unknown item: ' + symbol) from error


def inventory_item(item, description, heal_hp, cure_status, effect_bytes, use_support):
    return dict(item=item, description=description, healHp=heal_hp, cureStatus=cure_status,
                effectBytes=effect_bytes, useSupport=use_support)


def compile_catalog(project_root=ROOT):
    root = Path(project_root).resolve()
    items = definitions(root)
    descriptions = {row['metadata']['item']: row['metadata']['description']
                    for row in described_items(lambda path: (root / RAW / path).read_bytes(), items)}
    if any(not description.strip() for description in descriptions.values()):
        raise ValueError('Item descriptions must be nonempty')
    return dict(schemaVersion=2, items=[item.fields for item in items.values()], descriptions=descriptions)


def described_items(read, symbols):
    """Source descriptions plus shared item identities, without regional copies."""
    rows = json.loads(read('src/data/items.json'))['items']
    selected = list(symbols)
    if not selected or len(selected) != len(set(selected)):
        raise ValueError('Item selection must be nonempty and unique')
    result = []
    for symbol in selected:
        matches = [row for row in rows if row['itemId'] == symbol]
        if len(matches) != 1:
            raise ValueError('Expected one item declaration: ' + symbol)
        item = matches[0]
        description = item['description_english'].replace('\\n', '\n')
        if re.fullmatch(r'ITEM_(?:TM|HM)\d+', symbol):
            source = read('src/move_descriptions.c').decode('utf-8')
            matches = list(re.finditer(r'^const u8 gMoveDescription_' + re.escape(item['moveId']) +
                                      r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$', source, re.M))
            if len(matches) != 1:
                raise ValueError('Expected one machine move description: ' + symbol)
            description = json.loads(matches[0][1])
        # This projection only emits a stable reference. Its declaration was
        # selected uniquely above; no complete business catalog is needed here.
        result.append(dict(metadata=dict(item='pret:' + symbol, description=description), effectBytes=[]))
    return result
