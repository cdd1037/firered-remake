#!/bin/sh
set -eu
PACK=${1:?Pass pack and normal-cry catalog paths}
CATALOG=${2:?Pass normal-cry catalog path}
PACK=$(realpath "$PACK")
CATALOG=$(realpath "$CATALOG")
export DEMO_INTERACTIVE=1
exec "$(dirname "$0")/start.sh" --audio-driver Dummy -- "$PACK" "$CATALOG"
