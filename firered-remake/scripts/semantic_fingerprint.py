#!/usr/bin/env python3
"""Independent source-to-Domain admission fingerprint; this produces evidence, not an admission gate."""
import argparse
import hashlib
import json
from pathlib import Path

from import_canonical_pokemon import ROOT, authenticate, extract

SCHEMA = 'firered-source-mechanics-semantic-v1'

def semantic_envelope(doc, move_doc):
    return dict(
        sourceCommit=doc['sourceCommit'],
        species=sorted(doc['species'], key=lambda s: s['sourceSpeciesId']),
        moves=sorted(move_doc['moves'], key=lambda m: m['sourceMoveId']),
        metadata=[dict(species=m['id'], weightHectograms=m['weightHectograms'], sourceEvolutions=m['sourceEvolutions'])
                  for m in sorted(doc['metadata'], key=lambda m: m['id']['value'])],
        abilities=[dict(symbol=a['symbol'], sourceAbilityId=a['sourceAbilityId'])
                   for a in sorted(doc['abilities'], key=lambda a: a['symbol'])],
        maximumHpOneSpecies=doc['generationContract']['maximumHpOneSpecies'],
        formStatOverrides=doc['generationContract']['formStatOverrides'],
    )

def semantic_bytes(doc, move_doc):
    # Deliberately no terminal LF: this is the Domain proof's exact byte contract.
    return json.dumps(semantic_envelope(doc, move_doc), sort_keys=True,
                      separators=(',', ':'), ensure_ascii=False).encode('utf-8')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--move-root', type=Path, required=True)
    parser.add_argument('--source-root', type=Path)
    args = parser.parse_args()
    lock, sources, moves = authenticate(args.root, args.source_root, args.move_root)
    doc = extract(lock, sources, moves)
    data = semantic_bytes(doc, moves)
    out = args.root / 'content/generated/canonical-pokemon'
    out.mkdir(parents=True, exist_ok=True)
    (out / 'semantic-envelope.json').write_bytes(data)
    contract = dict(schema=SCHEMA, sourceCommit=doc['sourceCommit'], byteLength=len(data),
        sha256=hashlib.sha256(data).hexdigest(),
        encoding='UTF-8 JSON, recursive ordinal object-key sort, original array order after specified row sorts, ensure_ascii=False, compact separators, no terminal LF',
        speciesOrder='sourceSpeciesId ascending', movesOrder='sourceMoveId ascending',
        metadataOrder='species.Value ordinal', abilitiesOrder='symbol ordinal',
        objectFields='See semantic_fingerprint.py semantic_envelope; computed SixStats.Values omitted; GrowthRate camelCase string; source refs and false battleEffectImplemented included.')
    (out / 'semantic-contract.json').write_text(json.dumps(contract, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(contract, sort_keys=True))
