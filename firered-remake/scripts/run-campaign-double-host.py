#!/usr/bin/env python3
"""Build and verify the actual doubles view; preserve every new run's evidence."""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time
import traceback
from xml.sax.saxutils import quoteattr


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path, help='new evidence directory (never reused)')
    parser.add_argument('--checkpoint', type=Path,
                        default=root / 'tests/Game.Route8TwinsCampaign.Tests/Fixtures/original-party-before-twins.json',
                        help='actual public before-Twins checkpoint')
    parser.add_argument('--native', action='store_true',
                        help='capture native viewports only after coordinating the GUI; requires DOUBLE_UI_NATIVE_COORDINATED=1')
    args = parser.parse_args()
    output = args.output.absolute()
    if os.path.lexists(output):
        parser.error('Output must be new; keep failed evidence')
    output.mkdir(parents=True)
    output = output.resolve()
    host = root / 'tests/CampaignDoubleHost'
    commands = []
    sources = identity = None
    status = {'passed': False, 'native': args.native, 'stage': 'setup'}

    def save(name, value):
        (output / name).write_text(json.dumps(value, indent=2) + '\n')

    def run(command, name, timeout=240):
        status['stage'] = name
        command = list(map(str, command))
        record = {'command': command, 'cwd': str(root), 'log': name, 'timeoutSeconds': timeout}
        commands.append(record)
        save('commands.json', commands)
        with (output / name).open('wb') as log:
            process = subprocess.Popen(command, cwd=root, env=env, stdout=log,
                                       stderr=subprocess.STDOUT, start_new_session=True)
            try:
                record['exitCode'] = process.wait(timeout=timeout)
            except (subprocess.TimeoutExpired, KeyboardInterrupt):
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                process.wait()
                record['interrupted'] = True
                save('commands.json', commands)
                raise
        save('commands.json', commands)
        print(f'{name}: exit {record["exitCode"]}', flush=True)
        if record['exitCode']:
            raise RuntimeError(f'{name} exited {record["exitCode"]}; see retained log')
        text = (output / name).read_text(errors='replace')
        if any(token in text for token in ('ERROR:', 'Leaked instance:', 'ObjectDB instance')):
            raise RuntimeError(f'{name} contains an engine error/leak; see retained log')
        return text.strip()

    def source_manifest():
        files = {Path(__file__).resolve(), root / 'global.json', root / 'toolchain.lock.json',
                 host / 'project.godot', host / 'CampaignDoubleHost.tscn', font_source}
        files.update(root.glob('Directory.Build.*'))
        for directory in [host, *(root / 'src' / name for name in
                                 ('Game.Domain', 'Game.Application', 'Game.Content', 'Game.Godot'))]:
            files.update(path for path in directory.rglob('*')
                         if path.is_file() and not {'bin', 'obj', '.godot'}.intersection(path.relative_to(directory).parts)
                         and (path.suffix in {'.cs', '.csproj', '.props', '.targets'} or path.name == 'packages.lock.json'))
        return {'files': {str(path.relative_to(root)): sha256(path) for path in sorted(files)},
                'checkpoint': {'path': str(checkpoint), 'sha256': sha256(checkpoint)}}

    def assembly_identity():
        binary = host / '.godot/mono/temp/bin/Debug'
        # These are the exact copies loaded by Godot, including product dependencies.
        required = [binary / (name + '.dll') for name in
                    ('CampaignDoubleHost', 'Game.Application', 'Game.Content', 'Game.Domain')]
        files = set(required) | set(binary.glob('*.dll')) | set(binary.glob('*.deps.json')) | set(binary.glob('*.runtimeconfig.json'))
        return {'files': {str(path.relative_to(root)): sha256(path) for path in sorted(files)},
                'godot': {'path': str(godot), 'sha256': sha256(godot)},
                'dotnet': {'path': str(dotnet), 'sha256': sha256(dotnet)}}

    try:
        if args.native and os.environ.get('DOUBLE_UI_NATIVE_COORDINATED') != '1':
            raise RuntimeError('--native needs a coordinated GUI slot and DOUBLE_UI_NATIVE_COORDINATED=1')
        checkpoint = args.checkpoint.resolve(strict=True)
        tools_setting = os.environ.get('FIRERED_TOOLCHAIN_ROOT')
        tools = Path(tools_setting or (root / '.tools/toolchain-root').read_text().strip()).resolve(strict=True)
        dotnet = tools / 'dotnet/dotnet'
        godot_dir = tools / 'godot/Godot_v4.7.2-stable_mono_linux_x86_64'
        godot = godot_dir / 'Godot_v4.7.2-stable_mono_linux.x86_64'
        packages = Path(os.environ.get('NUGET_PACKAGES') or str(tools / 'nuget-cache')).resolve(strict=True)
        local_source = godot_dir / 'GodotSharp/Tools/nupkgs'
        for path in (dotnet, godot, local_source, packages):
            if not path.exists():
                raise FileNotFoundError(f'Required installed official tool/package path: {path}')
        env = {key: value for key, value in os.environ.items()
               if not key.startswith(('FIRERED_', 'DOUBLE_UI_', 'HIT_'))}
        env.pop('DoubleTestReferenceRoot', None)
        env.update(FIRERED_TOOLCHAIN_ROOT=str(tools), DOTNET_ROOT=str(tools / 'dotnet'),
                   DOTNET_CLI_HOME=str(output / 'dotnet-home'), NUGET_PACKAGES=str(packages),
                   PATH=str(tools / 'dotnet') + os.pathsep + env.get('PATH', ''),
                   DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
                   DOTNET_MULTILEVEL_LOOKUP='0', DOTNET_NOLOGO='1', GODOT_SILENCE_ROOT_WARNING='1',
                   FIRERED_PROJECT_ROOT=str(root), FIRERED_FUCHSIA_GYM='1', FIRERED_PRESET_DOUBLES='1',
                   FIRERED_PLAYER_ENTRY='0', FIRERED_AUDIO='0', FIRERED_AUDIO_BACKEND='synth',
                   FIRERED_CAMPAIGN_CHECKPOINT=str(checkpoint), FIRERED_EVIDENCE=str(output / 'campaign'),
                   DOUBLE_UI_EVIDENCE=str(output), DOUBLE_UI_VERIFY_ENTRY='1')
        for key, directory in [('XDG_DATA_HOME', 'data'), ('XDG_CONFIG_HOME', 'config'), ('XDG_CACHE_HOME', 'cache')]:
            path = output / 'runtime' / directory
            path.mkdir(parents=True)
            env[key] = str(path)
        (output / 'campaign').mkdir()
        save('environment.json', {key: value for key, value in env.items()
                                 if key.startswith(('FIRERED_', 'DOUBLE_UI_', 'DOTNET_', 'XDG_'))
                                 or key in {'NUGET_PACKAGES', 'GODOT_SILENCE_ROOT_WARNING'}})
        config = output / 'nuget.config'
        config.write_text('<configuration><packageSources><clear/>'
                          f'<add key="official-godot-local" value={quoteattr(str(local_source))}/>'
                          f'<add key="existing-local-cache" value={quoteattr(str(packages))}/>'
                          '</packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>\n')
        # Do not overwrite an existing asset or follow an unexpected Assets link.
        font_source = (root / 'src/Game.Godot/Assets/ProbeFont.ttc').resolve(strict=True)
        assets = host / 'Assets'
        if assets.is_symlink() and assets.resolve() != font_source.parent:
            raise RuntimeError(f'Unexpected Assets symlink: {assets}')
        assets.mkdir(exist_ok=True)
        font = assets / 'ProbeFont.ttc'
        if os.path.lexists(font):
            if not font.is_file() or not font.samefile(font_source):
                raise RuntimeError(f'Existing host font is not the production font: {font}')
        else:
            font.symlink_to(os.path.relpath(font_source, assets))

        # This host loads its own copied dependency DLLs. Serialize this host's runs,
        # but release the global compiler lock before its interactive/runtime checks.
        # Another project can then compile without changing these executable copies.
        host_run_lock = (host / '.godot' / 'double-host-run.lock')
        host_run_lock.parent.mkdir(exist_ok=True)
        host_lock = host_run_lock.open('a')
        fcntl.flock(host_lock, fcntl.LOCK_EX)
        with Path('/tmp/firered-remake-build.lock').open('a') as lock:
            status['stage'] = 'build-lock'
            deadline = time.monotonic() + 240
            while True:
                try:
                    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    break
                except BlockingIOError:
                    if time.monotonic() >= deadline:
                        raise TimeoutError('Timed out waiting for /tmp/firered-remake-build.lock')
                    time.sleep(0.25)
            sources = source_manifest()
            save('source-manifest.before.json', sources)
            if run([dotnet, '--version'], 'dotnet-version.log', 30) != '10.0.401':
                raise RuntimeError('The pinned .NET SDK 10.0.401 is required')
            if not run([godot, '--headless', '--version'], 'godot-version.log', 30).startswith('4.7.2.stable.mono.'):
                raise RuntimeError('The pinned official Godot 4.7.2 .NET editor is required')
            run([dotnet, 'build', host / 'CampaignDoubleHost.csproj', '--configuration', 'Debug',
                 '--configfile', config, '--no-incremental', '--disable-build-servers', '-m:1', '-nr:false',
                 '-p:UseSharedCompilation=false', '-p:NuGetAudit=false', '-p:RestoreLockedMode=true',
                 '-p:DoubleTestReferenceRoot=', '-p:BuildProjectReferences=true'], 'build.log')
            run([godot, '--headless', '--editor', '--import', '--path', host, '--quit'], 'import.log')
            identity = assembly_identity()
            save('assembly-identity.before.json', identity)
            if source_manifest() != sources:
                raise RuntimeError('Sources/checkpoint changed during build/import')
            fcntl.flock(lock, fcntl.LOCK_UN)
            run([godot, *([] if args.native else ['--headless']), '--audio-driver', 'Dummy',
                 '--path', host, '--verbose'], 'runtime.log')
            status['stage'] = 'result-validation'
            result = json.loads((output / 'result.json').read_text())
            checks = result.get('results', [])
            required = {'actual runtime major 10', 'actual doubles profile',
                        'actual public before-Twins checkpoint',
                        'keyboard completed Intro enters four-slot combat',
                        'mouse completed Intro enters four-slot combat'}
            if (result.get('passed') is not True or not isinstance(result.get('checks'), int)
                    or result['checks'] < 40 or not isinstance(checks, list)
                    or len(checks) != result['checks'] or not required.issubset(checks)):
                raise RuntimeError('Missing successful nonempty host suite or actual checkpoint entry checks')
            if (not isinstance(result.get('display'), str) or not result['display']
                    or (result['display'] == 'headless') == args.native):
                raise RuntimeError('Host display mode does not match the requested mode')
            if args.native and not (output / '01-trainer.png').is_file():
                raise RuntimeError('Native run did not save its viewport evidence')
            after_sources, after_identity = source_manifest(), assembly_identity()
            save('source-manifest.after.json', after_sources)
            save('assembly-identity.after.json', after_identity)
            if after_sources != sources or after_identity != identity:
                raise RuntimeError('Source/checkpoint/compiled executable identity changed during verification')
            status.update(passed=True, checks=result['checks'], stage='complete')
    except (Exception, KeyboardInterrupt) as error:
        status.update(error=str(error), errorType=type(error).__name__)
        (output / 'error.log').write_text(traceback.format_exc())
        print(f'FAIL: {error}; evidence retained in {output}', file=sys.stderr)
    finally:
        # Bind failed runs, including timeouts, to whatever remains on disk too.
        if not status['passed']:
            for name, before, capture in [('source-manifest.after.json', sources, source_manifest),
                                          ('assembly-identity.after.json', identity, assembly_identity)]:
                if before is not None:
                    try:
                        after = capture()
                        save(name, after)
                        status[name.replace('.after.json', 'Unchanged')] = after == before
                    except Exception as error:
                        status[name] = f'Cannot capture final identity: {error}'
        save('runner-result.json', status)
    if status['passed']:
        print(json.dumps(status))
    return 0 if status['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
