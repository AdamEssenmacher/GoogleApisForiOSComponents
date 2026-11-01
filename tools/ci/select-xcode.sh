#!/usr/bin/env bash
# Select the pinned Xcode for this job only, without sudo or changing the machine's xcode-select.
# Usage: tools/ci/select-xcode.sh <version>   (for example 27.1)
set -euo pipefail

want="${1:?usage: select-xcode.sh <version>}"
found=""
for app in /Applications/Xcode*.app; do
  plist="$app/Contents/Info.plist"
  [[ -f "$plist" ]] || continue
  have=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$plist" 2>/dev/null || true)
  # 27.1 matches 27.1 and 27.1.0; it does not match 27.1.1 or 27.10.
  if [[ "$have" == "$want" || "$have" == "$want.0" ]]; then
    found="$app"
    break
  fi
done
if [[ -z "$found" ]]; then
  echo "Xcode $want is not installed under /Applications." >&2
  exit 1
fi

developer="$found/Contents/Developer"
if [[ -n "${GITHUB_ENV:-}" ]]; then
  echo "DEVELOPER_DIR=$developer" >> "$GITHUB_ENV"
fi
DEVELOPER_DIR="$developer" xcodebuild -version
DEVELOPER_DIR="$developer" xcode-select -p
