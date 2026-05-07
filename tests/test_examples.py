"""testing examples"""

import glob
import sys

import pytest

sys.path.append(".")
from tests import EXAMPLES_DIR, build_example  # isort:skip # pylint: disable=wrong-import-position


@pytest.mark.parametrize("script_path", glob.glob(str(EXAMPLES_DIR / "example_*.py")))
def test_examples(script_path):
    build_example(script_path)
