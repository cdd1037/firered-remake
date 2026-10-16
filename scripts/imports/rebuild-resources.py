#!/usr/bin/env python3
"""Rebuild explicitly selected final resources from pinned local inputs.

This is not a whole-game cold rebuild. In particular, audio-cues needs the
existing matching audio pack and normal-cry catalog; neither is synthesized here.
Other item, Pokemon, trainer, UI and story producers retain their own input
contracts. No reference-repository scripts, downloads or historical stages run.
"""
import argparse
from pathlib import Path
import shlex
import subprocess
import sys

PROJECT_ROOT = Path(__file__).resolve().parents[2]
TARGET_SCRIPTS = {
    'world': ('scripts/imports/maps/import-world.py',),
    'field-rules': ('scripts/imports/story/import-early-field-rules.py',
                    'scripts/imports/story/import-world-field-rules.py'),
    'field-services': ('scripts/imports/story/import-city-services.py',
                       'scripts/imports/story/import-field-services.py'),
    'audio-cues': ('scripts/imports/audio/build-cue-catalog.py',),
    'diploma': ('scripts/imports/presentation/import-diploma.py',),
    'help': ('scripts/imports/presentation/import-help-system.py',),
}


def build_commands(targets, raw, out, *, pack=None, cries=None, check=False):
    """Translate final resource targets to their actual producer contracts."""
    if not targets or len(set(targets)) != len(targets):
        raise ValueError('Select one or more distinct resource targets')
    if any(target not in TARGET_SCRIPTS for target in targets):
        raise ValueError('Unknown resource target')
    if (pack is not None or cries is not None) and 'audio-cues' not in targets:
        raise ValueError('--pack and --cries apply only to audio-cues')
    raw, out = Path(raw).resolve(), Path(out).resolve()
    if raw.is_relative_to(out) or out.is_relative_to(raw):
        raise ValueError('Source and output trees overlap')
    commands = []
    # Target order is explicit and stable, regardless of the selection order.
    for target, scripts in TARGET_SCRIPTS.items():
        if target not in targets:
            continue
        for script in scripts:
            if target == 'audio-cues':
                pack = Path(pack).resolve() if pack is not None else out / 'audio/synth/firered.pack'
                cries = Path(cries).resolve() if cries is not None else out / 'audio/synth/normal-cries.bin'
                output = out / 'audio/cues.json'
                if output in (pack, cries):
                    raise ValueError('Audio cue output overlaps an audio input')
                arguments = ['--reference', str(raw), '--pack', str(pack), '--cries', str(cries),
                             '--output', str(output)]
            elif target == 'help':
                arguments = ['--raw', str(raw), '--out', str(out / 'ui/help/help.json')]
            elif target == 'diploma':
                arguments = ['--raw', str(raw), '--out', str(out / 'ui/diploma')]
            else:
                arguments = ['--raw', str(raw), '--out', str(out)]
            if check:
                arguments.append('--check')
            commands.append([sys.executable, str(PROJECT_ROOT / script), *arguments])
        if target == 'field-services' and not check:
            commands.append([sys.executable, str(PROJECT_ROOT / 'scripts/lib/item_icon_exports.py'),
                             '--generated', str(out)])
    return commands


def preflight(commands, raw):
    """Catch missing producers and external audio inputs before starting any step.

Each producer validates its own source closure and output contract. A nonempty
raw directory alone is not a claim that all selected source files are present.
"""
    if not Path(raw).is_dir():
        raise FileNotFoundError('Pinned raw directory is missing: ' + str(raw))
    for command in commands:
        if not Path(command[1]).is_file():
            raise FileNotFoundError('Required resource producer is missing: ' + command[1])
        for flag in ('--pack', '--cries'):
            if flag in command:
                path = Path(command[command.index(flag) + 1])
                if not path.is_file():
                    raise FileNotFoundError('Existing matching audio input is required: ' + str(path))


def main(argv=None):
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog='Targets:\n'
               '  world          All selected maps, tilesets, NPC art and map atlases.\n'
               '  field-rules    Early/world field rules, encounters and interactive field art.\n'
               '  field-services City/rod items, trades, field service/battle bindings and item atlas.\n'
               '  audio-cues     Cue catalog from source + existing pack and normal-cries.\n'
               '  diploma        Original Kanto/National certificate artwork, text and layout.\n'
               '  help           Original contextual HELP texts, ordered tables and unlock predicates.\n'
               '\nThere is no implicit all target or best-effort skipping. A failed step stops the run.\n'
               '--check is read-only for every target; world recompiles in a temporary directory.')
    parser.add_argument('targets', nargs='+', choices=tuple(TARGET_SCRIPTS))
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4',
                        help='Pinned upstream source files, read directly without running Git or upstream scripts')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated',
                        help='Final generated resource root')
    parser.add_argument('--pack', type=Path, help='Existing audio pack; defaults to OUT/audio/synth/firered.pack')
    parser.add_argument('--cries', type=Path, help='Existing normal-cry catalog; defaults to OUT/audio/synth/normal-cries.bin')
    parser.add_argument('--check', action='store_true', help='Compare selected outputs without writing')
    parser.add_argument('--dry-run', action='store_true', help='Preflight inputs and print commands without executing them')
    args = parser.parse_args(argv)
    try:
        commands = build_commands(args.targets, args.raw, args.out, pack=args.pack, cries=args.cries, check=args.check)
        preflight(commands, args.raw)
    except (ValueError, FileNotFoundError) as error:
        parser.error(str(error))
    for command in commands:
        print(shlex.join(command), flush=True)
        if not args.dry_run:
            result = subprocess.run(command, cwd=PROJECT_ROOT)
            if result.returncode:
                print('Resource producer failed: ' + command[1], file=sys.stderr)
                return result.returncode
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
