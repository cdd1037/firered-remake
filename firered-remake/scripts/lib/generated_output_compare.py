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
