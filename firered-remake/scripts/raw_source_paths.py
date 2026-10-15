"""Locate the three explicitly retained local trainer palette inputs."""
from pathlib import Path


def local_trainer_palette_directory(raw):
    raw = Path(raw)
    if raw.parts[-2:] == ('pokefirered', '037335f4'):
        return raw.parent.parent / 'local/forest-trainer-art'
    return raw
