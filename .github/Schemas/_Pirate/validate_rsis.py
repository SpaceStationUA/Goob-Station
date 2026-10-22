#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later

# Pirate: runs the engine's RSI validator with extra allowed licenses.
# The engine schema only accepts Creative Commons licenses; this repo is AGPL,
# so AGPL art (e.g. sprites ported from AGPL SS13 codebases like Sojourn) is
# compatible here and must keep its real license instead of a false CC label.

import os
import runpy

import jsonschema

EXTRA_LICENSES = [
    "AGPL-3.0-or-later",
]

repo_root = os.path.abspath(os.path.join(os.path.dirname(os.path.realpath(__file__)), "..", "..", ".."))
engine_script = os.path.join(repo_root, "RobustToolbox", "Schemas", "validate_rsis.py")

_EngineValidator = jsonschema.Draft7Validator


# The engine script runs on import (module-level exit(main())), so instead of patching
# its load_schema we widen the license enum in whatever schema it hands the validator.
def _validator(schema, *args, **kwargs):
    allowed = schema.get("properties", {}).get("license", {}).get("enum")
    if allowed is not None:
        allowed.extend(l for l in EXTRA_LICENSES if l not in allowed)
    return _EngineValidator(schema, *args, **kwargs)


jsonschema.Draft7Validator = _validator

runpy.run_path(engine_script, run_name="__main__")
