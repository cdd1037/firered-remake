"""Compact encoding for generated runtime JSON; source-lock encoders stay separate."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import json


def runtime_json_bytes(value, *, sort_keys=False, default=None):
    return (json.dumps(value, ensure_ascii=False, separators=(',', ':'),
                       sort_keys=sort_keys, default=default) + '\n').encode('utf-8')
