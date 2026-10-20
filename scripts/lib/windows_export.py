"""Standalone Windows packaging. Linux export and player saves are never touched."""
import hashlib
import json
import os
import pathlib
import shutil
import struct
import tempfile
import zipfile


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def require_x64_pe(path):
    with path.open('rb') as stream:
        if stream.read(2) != b'MZ':
            raise RuntimeError(f'Not a Windows executable: {path.name}')
        stream.seek(0x3c)
        offset = struct.unpack('<I', stream.read(4))[0]
        stream.seek(offset)
        if stream.read(6) != b'PE\0\0\x64\x86':
            raise RuntimeError(f'Not a Windows x64 PE: {path.name}')


def verify_trimmed_runtime(root, data, env):
    config = json.loads((data / 'Game.Godot.runtimeconfig.json').read_text())['runtimeOptions']
    settings = config['configProperties']
    for name in ('System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault',
                 'System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting'):
        if settings.get(name) is not True:
            raise RuntimeError(f'Required reflection/native-host switch missing: {name}')
    framework = next(f for f in config['includedFrameworks'] if f['name'] == 'Microsoft.NETCore.App')
    if not framework['version'].startswith('10.'):
        raise RuntimeError('Windows package must bundle .NET 10.')
    original = pathlib.Path(env['NUGET_PACKAGES']) / 'microsoft.netcore.app.runtime.win-x64' / framework['version'] / 'runtimes/win-x64/lib/net10.0/System.Private.CoreLib.dll'
    linked = root / 'src/Game.Godot/.godot/mono/temp/obj/ExportRelease/win-x64/linked/System.Private.CoreLib.dll'
    if not linked.is_file():
        matches = list((root / 'src/Game.Godot/.godot/mono/temp/obj/ExportRelease').glob('**/win-x64/linked/System.Private.CoreLib.dll'))
        if len(matches) != 1:
            raise RuntimeError('Missing or ambiguous actual ILLink output.')
        linked = matches[0]
    packaged = data / 'System.Private.CoreLib.dll'
    if not original.is_file() or packaged.stat().st_size >= original.stat().st_size:
        raise RuntimeError('Framework DLL did not shrink: actual trimming is unproved.')
    if sha256(packaged) != sha256(linked):
        raise RuntimeError('Package does not contain the verified ILLink output.')
    for name in ('Game.Godot.dll', 'Game.Application.dll', 'Game.Content.dll', 'Game.Domain.dll', 'Game.Audio.dll', 'GodotSharp.dll', 'System.Collections.dll', 'System.Collections.Immutable.dll', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll'):
        if not (data / name).is_file():
            raise RuntimeError(f'Missing bundled runtime file: {name}')
    for name in ('coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll'):
        require_x64_pe(data / name)
    return {'runtime': framework['version'], 'publishTrimmed': True, 'trimMode': 'full',
            'preservedAssemblies': ['GodotSharp', 'Game.Godot', 'Game.Application', 'Game.Content', 'Game.Domain', 'Game.Audio', 'System.Collections', 'System.Collections.Immutable'],
            'coreLibraryOriginalBytes': original.stat().st_size, 'coreLibraryTrimmedBytes': packaged.stat().st_size,
            'coreLibraryLinkedSha256': sha256(linked), 'nativeHostAndJsonReflectionEnabled': True}


def export_windows(root, env, run, godot):
    template_base = (pathlib.Path(env.get('APPDATA', pathlib.Path.home() / 'AppData/Roaming')) / 'Godot'
                     if os.name == 'nt' else pathlib.Path(env['XDG_DATA_HOME']) / 'godot')
    template = template_base / 'export_templates/4.7.2.stable.mono/windows_release_x86_64.exe'
    if not template.is_file():
        raise FileNotFoundError(f'Install official Godot 4.7.2 .NET Windows x64 export templates: {template}')
    generated = root / 'content/generated'
    if not (generated / 'localization/zh-Hans/font/cjk-font.json').is_file():
        raise FileNotFoundError('Missing Chinese generated pack/font; see docs/chinese-edition.md.')
    destination = root / 'exports/windows'
    archive = root / 'exports/FireRed-Windows-x64-zh-Hans.zip'
    if destination.exists() or archive.exists():
        raise FileExistsError('Windows package already exists. Move previous outputs aside first; nothing was overwritten.')
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='firered-windows-') as temporary:
        stage = pathlib.Path(temporary) / 'FireRed-Windows-x64-zh-Hans'
        stage.mkdir()
        executable = stage / 'firered.exe'
        run([godot, '--headless', '--editor', '--path', 'src/Game.Godot', '--import', '--quit'], 'windows-export-import.log')
        run([godot, '--headless', '--path', 'src/Game.Godot', '--export-release', 'Windows x86_64', executable], 'windows-export.log')
        require_x64_pe(executable)
        if not (stage / 'firered.pck').is_file():
            raise RuntimeError('Missing exported PCK.')
        candidates = list(stage.glob('data_*/Game.Godot.runtimeconfig.json'))
        if len(candidates) != 1:
            raise RuntimeError('Expected exactly one bundled .NET runtime.')
        trimming = verify_trimmed_runtime(root, candidates[0].parent, env)
        shutil.copytree(generated, stage / 'content/generated')
        shutil.copy2(root / 'src/Game.Godot/Assets/FONT-LICENSE.txt', stage / 'FONT-LICENSE.txt')
        (stage / 'Play-Chinese.cmd').write_text('@echo off\nsetlocal\nset "FIRERED_PROJECT_ROOT="\nset "FIRERED_CAMPAIGN_CHECKPOINT="\nset "FIRERED_EVIDENCE="\nset "FIRERED_TEXT_PACK=%~dp0content\\generated\\localization\\zh-Hans"\n"%~dp0firered.exe" %*\nendlocal\n', encoding='ascii', newline='\r\n')
        (stage / 'README-Windows.txt').write_text(
            'FireRed Windows x64 / 简体中文测试版\n\n'
            '完整解压 ZIP 后，双击 Play-Chinese.cmd 启动中文版。不要只复制 exe。\n'
            '直接双击 firered.exe 使用英文默认界面。无需安装 Godot、.NET SDK 或 .NET 运行时。\n'
            '方向键移动；Enter 确认；Backspace 返回；Tab 打开菜单；Esc 退出。\n'
            '保留原有 user:// 存档位置。本包不含存档，不会自动覆盖、迁移旧档；schema 1/2 不兼容。\n\n'
            'Godot 4.7.2 .NET；Windows x64；自包含 .NET ' + trimming['runtime'] + '。\n'
            '.NET PublishTrimmed=true / TrimMode=full 已执行。为避免反射误删，保留全部 Game.* 和 GodotSharp；实际裁剪主要作用于框架。\n'
            '这是 Linux 上交叉导出的 Windows 包：结构、PE 架构、运行库和实际 ILLink 输出已检查，尚未在 Windows/Wine 实机启动验证。\n'
            '中文目前缺少少量字形（小字体靓、黾）；原版图片不变。非完整全游戏验收，实际声音与中文 IME 尚未验收。\n', encoding='utf-8-sig', newline='\r\n')
        temporary_archive = pathlib.Path(temporary) / archive.name
        with zipfile.ZipFile(temporary_archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
            for file in sorted(stage.rglob('*')):
                if file.is_file():
                    bundle.write(file, pathlib.Path(stage.name) / file.relative_to(stage))
        with zipfile.ZipFile(temporary_archive) as bundle:
            if bundle.testzip() is not None:
                raise RuntimeError('ZIP CRC verification failed.')
        result = {'package': str(destination), 'zip': str(archive), 'zipBytes': temporary_archive.stat().st_size,
                  'zipSha256': sha256(temporary_archive), 'executableSha256': sha256(executable),
                  'pckSha256': sha256(stage / 'firered.pck'), 'trimming': trimming,
                  'windowsExecution': 'NOT TESTED: cross-export only; Windows or Wine acceptance required.'}
        shutil.move(str(stage), str(destination))
        shutil.move(str(temporary_archive), str(archive))
    (root / 'evidence/windows-export.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    return result
