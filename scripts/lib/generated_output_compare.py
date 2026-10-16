"""Compare generated JSON values and PNG pixels without serialization noise."""
import io
import json
from PIL import Image


def _same_json(left, right):
    if type(left) is not type(right):
        return False
    if isinstance(left, dict):
        return left.keys() == right.keys() and all(_same_json(left[k], right[k]) for k in left)
    if isinstance(left, list):
        return len(left) == len(right) and all(_same_json(a, b) for a, b in zip(left, right))
    return left == right


def generated_output_matches(path, expected):
    """Keep every JSON value, array order, PNG dimension and RGBA channel exact.

    JSON object ordering/whitespace and PNG compression are not runtime data.
    Other formats, including raw audio, retain exact byte comparison.
    """
    if not path.is_file():
        return False
    actual = path.read_bytes()
    if path.suffix.lower() == '.json':
        return _same_json(json.loads(actual), json.loads(expected))
    if path.suffix.lower() == '.png':
        with Image.open(io.BytesIO(actual)) as a, Image.open(io.BytesIO(expected)) as b:
            return a.size == b.size and a.convert('RGBA').tobytes() == b.convert('RGBA').tobytes()
    return actual == expected


def check_item_outputs(generated, outputs):
    """Compare item documents and logical icon pixels before or after atlas packing."""
    import copy
    import io
    import json
    from pathlib import Path
    from PIL import Image
    from item_icon_exports import decode_icon_page
    generated = Path(generated)
    checked_images = set()
    for relative, data in outputs.items():
        if not relative.endswith('.json'):
            continue
        expected = json.loads(data)
        actual = json.loads((generated / relative).read_bytes())
        collection = 'icons' if isinstance(expected, dict) and 'icons' in expected else 'assets'
        if isinstance(expected, dict) and collection in expected:
            if len(actual.get(collection, [])) != len(expected[collection]):
                raise ValueError('Item icon count differs: ' + relative)
            actual = copy.deepcopy(actual)
            for index, icon in enumerate(expected[collection]):
                current = actual[collection][index]
                field = 'pngPath' if 'pngPath' in icon else 'texturePath'
                if field not in icon: continue
                name = icon[field].removeprefix('content/generated/') if icon[field].startswith('content/generated/') else ((Path(relative).parent / icon[field]).as_posix())
                if name not in outputs: raise ValueError('Missing source item icon: ' + name)
                path = generated / current[field].removeprefix('content/generated/') if current[field].startswith('content/generated/') else (generated / relative).parent / current[field]
                rebuilt = Image.open(io.BytesIO(outputs[name])).convert('RGBA')
                retained = decode_icon_page(path.read_bytes(), current)
                if rebuilt.size != retained.size or rebuilt.tobytes() != retained.tobytes():
                    raise ValueError('Item icon pixels differ: ' + name)
                normalized = {key: value for key, value in current.items() if key not in (field, 'region')}
                if normalized != {key: value for key, value in icon.items() if key != field}:
                    raise ValueError('Item icon definition differs: ' + name)
                actual[collection][index] = icon
                checked_images.add(name)
        if actual != expected: raise ValueError('Generated item document differs: ' + relative)
    for relative, data in outputs.items():
        if not relative.endswith('.json') and relative not in checked_images and not generated_output_matches(generated / relative, data):
            raise ValueError('Generated image differs: ' + relative)
