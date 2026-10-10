#!/usr/bin/env bash
# Latest bounded campaign. Each older profile retains its separate save folder.
set -euo pipefail
exec bash "$(dirname "$0")/launch-celadon-gym.sh" "$@"
