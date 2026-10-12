#!/usr/bin/env bash
# Latest bounded campaign. Each older profile retains its separate save folder.
set -euo pipefail
bash "$(dirname "$0")/ensure-godot-imports.sh"
exec bash "$(dirname "$0")/launch-saffron-gym.sh" "$@"
