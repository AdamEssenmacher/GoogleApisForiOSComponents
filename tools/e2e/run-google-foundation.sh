#!/bin/zsh
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/e2e/run-google-foundation.sh [options]

  --package-dir <dir>        Local NuGet feed to restore AdamE.* from (default: output)
  --configuration <cfg>      Debug or Release (default: Debug)
  --target <name>            Binding target to exercise (default: Places)
  --package-version <ver>    Version of the target package to restore
  --allow-xcode-mismatch     Set ValidateXcodeVersion=false. Opt-in escape hatch for local machines
                             whose Xcode does not exactly match the .NET for iOS workload band.
                             Leave this off in CI.

Exercises binding NuGet packages from a local feed in a consumer-style app. Verifies package
delivery -- native linkage and bundled resources -- not backend behavior. No API keys required.
EOF
}

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
harness_root="$repo_root/tests/E2E/Google.Foundation"
project_dir="$harness_root/GoogleFoundationE2E"
project_file="$project_dir/GoogleFoundationE2E.csproj"
# Read from Info.plist rather than duplicating it: install/launch target the bundle id the app was
# actually built with, so the two cannot drift.
bundle_id="$(/usr/bin/plutil -extract CFBundleIdentifier raw -o - "$project_dir/Info.plist")"

configuration="Debug"
target="Places"
package_version=""
package_dir="$repo_root/output"
allow_xcode_mismatch="false"

artifacts_dir="$harness_root/artifacts"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --package-dir) package_dir="$2"; shift 2 ;;
    --configuration) configuration="$2"; shift 2 ;;
    --target) target="$2"; shift 2 ;;
    --package-version) package_version="$2"; shift 2 ;;
    --allow-xcode-mismatch) allow_xcode_mismatch="true"; shift ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 1 ;;
  esac
done

if [[ "$package_dir" != /* ]]; then
  package_dir="$repo_root/$package_dir"
fi

log_file="$artifacts_dir/simulator.log"
result_file="$artifacts_dir/result.json"
restore_config="$artifacts_dir/NuGet.generated.config"
repo_restore_config="$harness_root/NuGet.config"
packages_cache_dir="$artifacts_dir/packages"

mkdir -p "$artifacts_dir"
: > "$log_file"

rm -rf "$packages_cache_dir"
mkdir -p "$packages_cache_dir"

# Map the runtime adapter to its package and version property. Places is intentionally the only
# implemented adapter; adding another target requires an explicit package-specific runtime case.
case "$target" in
  Places)
    package_id="AdamE.Google.iOS.Places"
    package_version_property="PlacesPackageVersion"
    ;;
  *) echo "Unknown target: $target" >&2; exit 1 ;;
esac

if [[ ! -d "$package_dir" ]]; then
  echo "Package directory does not exist: $package_dir" >&2
  exit 1
fi

if [[ -n "$package_version" ]]; then
  nupkg="$package_dir/$package_id.$package_version.nupkg"
  if [[ ! -f "$nupkg" ]]; then
    echo "Package not found: $nupkg" >&2
    exit 1
  fi
else
  package_matches=()
  while IFS= read -r package_match; do
    package_matches+=("$package_match")
  done < <(
    find "$package_dir" -maxdepth 1 -type f -name "$package_id.*.nupkg" \
      ! -name "*.symbols.nupkg" -print | sort
  )

  case "${#package_matches[@]}" in
    0)
      echo "No $package_id package found in $package_dir." >&2
      echo "Pack the target first or pass --package-dir explicitly." >&2
      exit 1
      ;;
    1)
      nupkg="${package_matches[1]}"
      package_file_name="$(basename "$nupkg")"
      package_version="${package_file_name#${package_id}.}"
      package_version="${package_version%.nupkg}"
      ;;
    *)
      echo "Multiple $package_id packages found in $package_dir:" >&2
      for package_match in "${package_matches[@]}"; do
        echo "  $package_match" >&2
      done
      echo "Pass --package-version to select one explicitly." >&2
      exit 1
      ;;
  esac
fi

msbuild_args=(
  "-p:GoogleE2ETarget=$target"
  "-p:$package_version_property=$package_version"
)
if [[ "$allow_xcode_mismatch" == "true" ]]; then
  echo "WARNING: ValidateXcodeVersion=false -- building against a non-matching Xcode." >&2
  msbuild_args+=("-p:ValidateXcodeVersion=false")
fi

if [[ "$package_dir" == "$repo_root/output" ]]; then
  restore_config="$repo_restore_config"
else
  cat > "$restore_config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-output" value="$package_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-output">
      <package pattern="AdamE.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF
fi

platform="iPhoneSimulator"
rid="iossimulator-arm64"

echo "Package: $nupkg"
echo "Restoring GoogleFoundationE2E ($target $package_version) from $package_dir"
dotnet restore "$project_file" \
  --configfile "$restore_config" \
  --packages "$packages_cache_dir" \
  --force-evaluate \
  "${msbuild_args[@]}"

echo "Cleaning GoogleFoundationE2E"
dotnet clean "$project_file" \
  --configuration "$configuration" \
  --framework net10.0-ios \
  -p:Platform="$platform" \
  -p:RuntimeIdentifier="$rid" \
  "${msbuild_args[@]}" >/dev/null

echo "Building GoogleFoundationE2E for $rid"
dotnet build "$project_file" \
  --configuration "$configuration" \
  --framework net10.0-ios \
  --no-restore \
  -p:Platform="$platform" \
  -p:RuntimeIdentifier="$rid" \
  "${msbuild_args[@]}"

app_path="$(find "$project_dir/bin" -path "*$platform/$configuration/net10.0-ios/$rid/GoogleFoundationE2E.app" -print -quit)"
if [[ ! -d "$app_path" ]]; then
  echo "Built app not found under $project_dir/bin" >&2
  exit 1
fi
echo "App: $app_path"

# Capture target-neutral diagnostics for failed-run investigation. These are not golden baselines;
# the runtime cases below make the delivery assertions.
capture_manifest() {
  local out_dir="$1"
  mkdir -p "$out_dir"
  (cd "$app_path" && find . -type f | sed 's|^\./||' | sort) > "$out_dir/app-files.txt"
  nm -gU "$app_path/GoogleFoundationE2E" 2>/dev/null \
    | awk 'NF { print $NF }' \
    | sort -u > "$out_dir/native-symbols.txt" || true
  otool -L "$app_path/GoogleFoundationE2E" 2>/dev/null \
    | sed '1s|^.*:|GoogleFoundationE2E:|' > "$out_dir/linked-libraries.txt" || true
  otool -l "$app_path/GoogleFoundationE2E" 2>/dev/null \
    | grep -c "__LLVM" > "$out_dir/llvm-segment-count.txt" || echo 0 > "$out_dir/llvm-segment-count.txt"
  echo "Manifest captured: $out_dir"
}

capture_manifest "$artifacts_dir/manifest-$rid"

simulator_udid="${E2E_SIMULATOR_UDID:-}"
if [[ -z "$simulator_udid" ]]; then
  simulator_udid="$(
    xcrun simctl list devices available |
      sed -nE "/iPhone/ { s/.*\\(([0-9A-F-]{36})\\).*/\\1/p; q; }"
  )"
fi

if [[ -z "$simulator_udid" ]]; then
  echo "No available iPhone simulator could be found." >&2
  exit 1
fi

echo "Using simulator: $simulator_udid"
xcrun simctl boot "$simulator_udid" >/dev/null 2>&1 || true
xcrun simctl bootstatus "$simulator_udid" -b

log_pid=""
cleanup() {
  if [[ -n "$log_pid" ]] && kill -0 "$log_pid" >/dev/null 2>&1; then
    kill "$log_pid" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

(xcrun simctl spawn "$simulator_udid" log stream \
  --style compact \
  --level debug \
  --predicate "processImagePath ENDSWITH[c] 'GoogleFoundationE2E' OR eventMessage CONTAINS[c] 'E2E_STATUS:' OR eventMessage CONTAINS[c] 'E2E_RESULT:'" \
  > "$log_file" 2>&1) &
log_pid="$!"

echo "Installing app"
xcrun simctl uninstall "$simulator_udid" "$bundle_id" >/dev/null 2>&1 || true
xcrun simctl install "$simulator_udid" "$app_path"

echo "Launching app"
xcrun simctl launch --terminate-running-process "$simulator_udid" "$bundle_id" >> "$log_file" 2>&1

data_container="$(xcrun simctl get_app_container "$simulator_udid" "$bundle_id" data)"
container_result_file="$data_container/Library/Caches/google-foundation-e2e-result.json"

timeout_seconds="${E2E_TIMEOUT_SECONDS:-90}"
echo "Waiting up to $timeout_seconds seconds for E2E result"

elapsed=0
while [[ ! -f "$container_result_file" ]]; do
  if (( elapsed >= timeout_seconds )); then
    echo "Timed out waiting for E2E result file: $container_result_file" >&2
    exit 1
  fi

  sleep 2
  elapsed=$((elapsed + 2))
done

cp "$container_result_file" "$result_file"

success="$(/usr/bin/plutil -extract Success raw -o - "$result_file" 2>/dev/null || true)"

echo "E2E result file: $result_file"
cat "$result_file"

xcrun simctl terminate "$simulator_udid" "$bundle_id" >/dev/null 2>&1 || true

if [[ "$success" == "true" ]]; then
  echo
  echo "Google foundation E2E ($target) passed."
  exit 0
fi

echo
echo "Google foundation E2E ($target) failed." >&2
exit 1
