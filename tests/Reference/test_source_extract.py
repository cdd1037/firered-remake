"""Standard-library checks for the shared, test-only source extractor."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import source_extract as extract


class SourceExtractTests(unittest.TestCase):
    def test_function_preserves_body_and_ignores_declarations(self):
        source = 'void Step(void);\nstatic void Step(void)\n{\n    if (1) { return; }\n}\n'
        self.assertEqual(source.split('\n', 1)[1], extract.function(source, 'Step'))
        with self.assertRaisesRegex(ValueError, 'Missing function: Missing'):
            extract.function(source, 'Missing')
        with self.assertRaisesRegex(ValueError, 'Unclosed source block'):
            extract.function('void Step(void) {', 'Step')

    def test_older_families_keep_their_narrow_return_types(self):
        source = 's8 SignedByte(void) { return -1; }\n'
        self.assertEqual(source, extract.function(source, 'SignedByte'))
        with self.assertRaisesRegex(ValueError, 'Missing function: SignedByte'):
            extract.function(source, 'SignedByte', signed_byte=False)
        for return_type in ('void', 'bool8', 'u8', 'u16', 's16', 'u32', 's32'):
            with self.subTest(return_type=return_type):
                source = return_type + ' Step(void) { return 0; }\n'
                self.assertEqual(source, extract.function(source, 'Step', signed_byte=False))

    def test_arrays_and_enums_preserve_selected_source(self):
        source = 'static const u16 Values[] = {1, {2, 3}};\n'
        self.assertEqual(source, extract.array(source, 'Values'))
        with self.assertRaisesRegex(ValueError, 'Missing array'):
            extract.array(source, 'Other')
        enums = 'enum {OTHER};\nenum\n{\n    SELECTED, NEXT\n};\n'
        self.assertEqual('enum\n{\n    SELECTED, NEXT\n};\n',
                         extract.enum_containing(enums, 'SELECTED'))
        with self.assertRaisesRegex(ValueError, 'SELECT'):
            extract.enum_containing(enums, 'SELECT')

    def test_macros_keep_dependencies_order_and_family_separator_contract(self):
        headers = ['#define BASE 7\n#define ADD(x)(x + BASE)\n#define LAST ADD(3)\n']
        self.assertEqual('#define BASE 7\n#define ADD(x)(x + BASE)\n#define LAST ADD(3)\n',
                         extract.macros(headers, ['LAST', 'BASE']))
        with self.assertRaisesRegex(ValueError, 'Missing macro: ADD'):
            extract.macros(headers, ['ADD'], require_separator=True)
        spaced = ['#define BASE 7\n#define ADD(x) (x + BASE)\n']
        self.assertEqual(spaced[0], extract.macros(spaced, ['ADD'], require_separator=True))
        with self.assertRaisesRegex(ValueError, 'Missing macro: UNKNOWN'):
            extract.macros(headers, ['UNKNOWN'])
        self.assertEqual('#define B A\n#define A B\n',
                         extract.macros(['#define A B\n#define B A'], ['A']))

    def test_verification_uses_explicit_family_pins_and_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'source.c').write_bytes(b'original')
            pins = root / 'family.json'
            pins.write_text(json.dumps({'source.c': hashlib.sha256(b'original').hexdigest()}))
            extract.verify(root, ['source.c'], pins)
            other = root / 'other-family.json'
            other.write_text(json.dumps({'source.c': hashlib.sha256(b'other').hexdigest()}))
            with self.assertRaisesRegex(ValueError, 'Pinned source mismatch: source.c'):
                extract.verify(root, ['source.c'], other)
            pins.write_text('{}')
            with self.assertRaisesRegex(ValueError, 'Pinned source mismatch: source.c'):
                extract.verify(root, ['source.c'], pins)
            with self.assertRaises(FileNotFoundError):
                extract.verify(root, ['source.c'], root / 'missing-pins.json')
            with self.assertRaises(FileNotFoundError):
                extract.verify(root, ['missing.c'], pins)
            self.assertEqual(b'original', (root / 'source.c').read_bytes())

    def test_identity_uses_pinned_revision_and_exact_file_bytes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'source.c').write_bytes(b'input\r\n')
            self.assertEqual({'source.c': {
                'sha256': hashlib.sha256(b'input\r\n').hexdigest(),
                'url': 'https://github.com/pret/pokefirered/blob/' + extract.COMMIT + '/source.c'}},
                extract.identity(root, ['source.c']))


if __name__ == '__main__':
    unittest.main()
