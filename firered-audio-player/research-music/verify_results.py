#!/usr/bin/env python3
"""Check bounded render evidence, per-track loops, and prior passing hashes."""
import argparse
import collections
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('output', type=pathlib.Path)
args = parser.parse_args()
output = args.output
selection = json.loads((output / 'selection.json').read_text())
coverage = json.loads((output / 'coverage.json').read_text())
previous = json.loads((ROOT / 'results/phase5-music-expanded/coverage.json').read_text())
old_rows = {row['name']: row for row in previous['rows']}
rows = {row['name']: row for row in coverage['rows']}
assert len(rows) == 12 and rows.keys() == old_rows.keys()
assert coverage['sampleRate'] == 48000 and coverage['blocks'] == [512, 257]
assert coverage['runtimeSourceSha256'] == selection['runtimeSourceSha256']
for relative, expected in selection['runtimeSourceSha256'].items():
    assert hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() == expected, relative

loops = []
preserved = []
for song in selection['songs']:
    name = song['name']
    row = rows[name]
    assert row['status'] == 'rendered' and row['blockPartitionExact'], name
    for render in (row['first'], row['second']):
        assert render['Error'] is None and render['Nonfinite'] == render['Clipped'] == 0, name
        assert render['CompletedFrames'] == render['OutputFrames'] == song['seconds'] * 48000, name
        assert render['Ended'] != song['expectedLooping'], name
    assert row['first']['Hash'] == row['second']['Hash'], name
    trace = (output / (name + '.events.txt')).read_text()
    observed = collections.defaultdict(list)
    for sample, track, tick in re.findall(r'sample=(\d+).*?track=(\d+).*?tick=(\d+) goto ', trace):
        observed[int(track)].append({'outputFrame': int(sample), 'seconds': int(sample) / 48000, 'tick': int(tick)})
    declared = [track for track in song['sourceTracks'] if track['gotoLines']]
    assert len(song['sourceTracks']) == song['trackCount'], name
    assert sorted(observed) == [track['trackIndex'] for track in declared], name
    assert bool(declared) == song['expectedLooping'], name
    tracks = [{'trackIndex': track['trackIndex'], 'sourceGotoLines': track['gotoLines'],
               'gotoCount': len(observed[track['trackIndex']]),
               'firstGoto': observed[track['trackIndex']][0]} for track in declared]
    loops.append({'name': name, 'secondsRendered': song['seconds'], 'sourcePath': song['sourcePath'],
                  'trackCount': song['trackCount'], 'declaredLoopingTracks': len(declared),
                  'allDeclaredLoopingTracksReachedGoto': True, 'tracks': tracks,
                  'ended': row['first']['Ended'],
                  'actualVoiceFamilies': sorted({int(kind) for kind in re.findall(r' kind=(\d+)', trace)})})
    if old_rows[name]['status'] == 'rendered':
        assert row['first']['Hash'] == old_rows[name]['first']['Hash'], name
        preserved.append({'name': name, 'sha256': row['first']['Hash'], 'exact': True})

catalog = json.loads((output / 'catalog-tests.json').read_text())
assert catalog['rendered'] == catalog['exactAgainstResearch'] == 386
assert catalog['invalidRequestsRejected']
rollback = json.loads((output / 'rollback-hashes.json').read_text())
assert len(rollback) == 8 and all(row['exact'] and row['sha256'] == row['actualSha256'] for row in rollback)
tests = (output / 'tests.txt').read_text()
test_count = int(re.search(r'^(\d+) tests passed$', tests, re.M)[1])
summary = {
    'scope': 'These 12 fixed original selections only; original data with documented modern host timing.',
    'selftestsPassed': test_count, 'selectedRendered': len(rows),
    'finiteBlockExactUnclipped': len(rows), 'loopingSongsCoveredOnEveryLoopingTrack': 7,
    'finiteSongsEnded': 5, 'catalogHashesExact': 386, 'legacyWavHashesExact': 8,
    'previouslyPassingMusicHashesExact': preserved,
    'renderedSecondsPerPartition': sum(song['seconds'] for song in selection['songs']),
    'loops': loops,
}
(output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps({key: value for key, value in summary.items() if key not in ('loops', 'previouslyPassingMusicHashesExact')}))
