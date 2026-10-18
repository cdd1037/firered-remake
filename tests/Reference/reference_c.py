"""Compile authored, bounded C reference harnesses without executing upstream scripts.

Normal tests consume ignored, locally generated numerical vectors; this module is only for explicit
fixture generation. Windows uses an installed Visual Studio C compiler, Linux cc.
"""
import hashlib
import os
from pathlib import Path
import shutil
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def verify_reference(root):
    root = Path(root)
    for relative, digest in {
        'src/battle_script_commands.c': '3b7341db8cd58c54fb0b54b47e6eef89c5c05e8a60ab4e9b45bdc907845c19b3',
        'src/battle_util.c': '87cf810eb2d58361d1beb1537b3bd71c9160a77c2a17d703399f6ed373bb5720',
    }.items():
        if hashlib.sha256((root / relative).read_bytes()).hexdigest() != digest:
            raise ValueError('Reference revision mismatch: ' + relative)


def compile_c(arguments):
    if os.name != 'nt':
        subprocess.run(arguments, check=True)
        return
    vswhere = Path(os.environ.get('ProgramFiles(x86)', 'C:/Program Files (x86)')) / 'Microsoft Visual Studio/Installer/vswhere.exe'
    installation = subprocess.check_output([str(vswhere), '-latest', '-products', '*', '-requires',
        'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-property', 'installationPath'], text=True).strip()
    if not installation:
        raise RuntimeError('Install Visual Studio C++ build tools to regenerate C vectors.')
    vcvars = Path(installation) / 'VC/Auxiliary/Build/vcvars64.bat'
    settings = subprocess.check_output(f'cmd /d /u /s /c ""{vcvars}" >nul && set"',
        text=True, encoding='utf-16le')
    env = os.environ.copy()
    for line in settings.splitlines():
        key, separator, value = line.partition('=')
        if separator and key:
            env[key] = value
    args = list(map(str, arguments[1:]))
    output = Path(args[args.index('-o') + 1]).resolve()
    sources = [str(Path(a).resolve()) for a in args if a.endswith('.c')]
    command = [shutil.which('cl', path=env.get('Path', env.get('PATH'))), '/nologo', '/std:c11', '/utf-8',
        '/Od' if '-O0' in args else '/O2', '/D_CRT_SECURE_NO_WARNINGS',
        '/Fe:' + str(output.with_suffix('.exe')), '/Fo:' + str(output.with_suffix('.obj'))]
    command += ['/I' + str(Path(a[2:]).resolve()) for a in args if a.startswith('-I')]
    subprocess.run(command + sources, env=env, cwd=output.parent, check=True)
