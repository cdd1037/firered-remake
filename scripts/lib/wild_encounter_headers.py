"""Select ordinary version headers; numbered external-event variants are not defaults."""
import re


def default_version_headers(groups, maps, version='FireRed'):
    wanted = set(maps)
    result = []
    for group in groups:
        for header in group['encounters']:
            label = header['base_label']
            if header['map'] not in wanted or not label.endswith('_' + version):
                continue
            # Source tables place externally selected variants after the ordinary
            # map header. Importing them as independent maps would both invent an
            # event selection and produce duplicate runtime encounter keys.
            if re.search(r'_\d+_' + re.escape(version) + r'$', label):
                continue
            result.append(header)
    if len({header['map'] for header in result}) != len(result):
        raise ValueError('Duplicate ordinary encounter header')
    return result
