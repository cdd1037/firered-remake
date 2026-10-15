"""Locate the three explicitly retained local trainer palette inputs."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from pathlib import Path


def local_trainer_palette_directory(raw):
    raw = Path(raw)
    if raw.parts[-2:] == ('pokefirered', '037335f4'):
        return raw.parent.parent / 'local/forest-trainer-art'
    return raw
