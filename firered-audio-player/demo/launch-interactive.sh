#!/bin/sh
set -eu
export DEMO_INTERACTIVE=1
exec "$(dirname "$0")/start.sh" --audio-driver Dummy
