#!/usr/bin/env python3
"""Runtime serialization is compact and never silently filters document fields."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import json
import unittest

from runtime_json import runtime_json_bytes


class RuntimeJsonTests(unittest.TestCase):
    def test_preserves_all_fields_and_ordered_values(self):
        value = {'inputs': [3, 1, 2], 'name': 'Pokémon',
                 'region': {'x': 4, 'y': 8, 'width': 16, 'height': 32},
                 'sourceRef': {'path': 'map.json', 'line': 7}}
        data = runtime_json_bytes(value)
        self.assertEqual(value, json.loads(data))
        self.assertNotIn(b'\\u00e9', data)
        self.assertNotIn(b'\n', data[:-1])
        self.assertEqual(b'\n', data[-1:])

    def test_sorting_is_explicit(self):
        value = {'z': 2, 'a': 1}
        self.assertEqual(b'{"z":2,"a":1}\n', runtime_json_bytes(value))
        self.assertEqual(b'{"a":1,"z":2}\n', runtime_json_bytes(value, sort_keys=True))

    def test_preserves_explicit_custom_encoding(self):
        class Identifier:
            pass
        self.assertEqual(b'{"id":"value"}\n', runtime_json_bytes(
            {'id': Identifier()}, default=lambda value: 'value'))


if __name__ == '__main__':
    unittest.main()
