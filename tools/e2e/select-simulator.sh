#!/usr/bin/env bash
# Prints the UDID of the iOS simulator that E2E and consumer tests should use.
#
#   E2E_SIMULATOR_UDID  use this simulator.
#   E2E_SIMULATOR_NAME  use the one available simulator with exactly this name; fail if there is
#                       none or more than one. CI sets this so tests never share a simulator with
#                       other tools on the same Mac (docs/SELF_HOSTED_MACOS_RUNNER.md).
#   neither             use the first available simulator whose name starts with "iPhone".
set -euo pipefail

if [[ -n "${E2E_SIMULATOR_UDID:-}" ]]; then
  echo "$E2E_SIMULATOR_UDID"
  exit 0
fi

if [[ -n "${E2E_SIMULATOR_NAME:-}" ]]; then
  matches="$(
    xcrun simctl list devices available -j |
      E2E_SIMULATOR_NAME="$E2E_SIMULATOR_NAME" python3 -c '
import json, os, sys
name = os.environ["E2E_SIMULATOR_NAME"]
for group in json.load(sys.stdin)["devices"].values():
    for device in group:
        if device["name"] == name:
            print(device["udid"])
'
  )"
  count="$(printf '%s' "$matches" | grep -c . || true)"
  if [[ "$count" -eq 1 ]]; then
    echo "$matches"
    exit 0
  fi
  if [[ "$count" -eq 0 ]]; then
    echo "No available simulator is named \"$E2E_SIMULATOR_NAME\". Create it once on this Mac from an iPhone" >&2
    echo "device type and an iOS runtime that supports it (see docs/SELF_HOSTED_MACOS_RUNNER.md):" >&2
    echo "  xcrun simctl create \"$E2E_SIMULATOR_NAME\" \"<iPhone device type>\" <iOS runtime identifier>" >&2
  else
    echo "$count available simulators are named \"$E2E_SIMULATOR_NAME\"; delete all but one:" >&2
    echo "$matches" >&2
  fi
  exit 1
fi

# Only names that start with "iPhone", so a dedicated CI simulator such as GoogleApisCI-iPhone is
# never picked by a local run.
udid="$(
  xcrun simctl list devices available |
    sed -nE "/^ *iPhone/ { s/.*\\(([0-9A-F-]{36})\\).*/\\1/p; q; }"
)"
if [[ -z "$udid" ]]; then
  echo "No available iPhone simulator could be found." >&2
  exit 1
fi
echo "$udid"
