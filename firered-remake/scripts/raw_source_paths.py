"""Keep importer metadata separate from the shared pinned source tree."""
from pathlib import Path


def raw_metadata_directory(raw, package):
    """Return the package metadata directory, preserving explicit staging roots."""
    raw = Path(raw)
    if raw.parts[-2:] == ('pokefirered', '037335f4'):
        return raw.parent.parent / package
    return raw
