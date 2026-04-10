#!/bin/zsh
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/e2e/run-firebase-foundation.sh [--package-dir output] [--configuration Release] [--enable-nullability-validation]
EOF
}

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
project_dir="$repo_root/tests/E2E/Firebase.Foundation/FirebaseFoundationE2E"
project_file="$project_dir/FirebaseFoundationE2E.csproj"
bundle_id="com.googleapisforioscomponents.tests.firebase.e2e"
configuration="Release"
enable_nullability_validation="false"
package_dir="$repo_root/output"
artifacts_dir="$repo_root/tests/E2E/Firebase.Foundation/artifacts"
log_file="$artifacts_dir/firebase-foundation-sim.log"
result_file="$artifacts_dir/firebase-foundation-result.json"
restore_config="$artifacts_dir/NuGet.generated.config"
repo_restore_config="$repo_root/tests/E2E/Firebase.Foundation/NuGet.config"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --package-dir)
      package_dir="$2"
      shift 2
      ;;
    --configuration)
      configuration="$2"
      shift 2
      ;;
    --enable-nullability-validation)
      enable_nullability_validation="true"
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ "$package_dir" != /* ]]; then
  package_dir="$repo_root/$package_dir"
fi

mkdir -p "$artifacts_dir"
: > "$log_file"

required_packages=(
  "AdamE.Firebase.iOS.Core"
  "AdamE.Firebase.iOS.Installations"
  "AdamE.Firebase.iOS.Analytics"
  "AdamE.Google.iOS.GoogleAppMeasurement"
  "AdamE.Google.iOS.GoogleDataTransport"
  "AdamE.Google.iOS.GoogleUtilities"
  "AdamE.Google.iOS.Nanopb"
  "AdamE.Google.iOS.PromisesObjC"
)

msbuild_args=()
if [[ "$enable_nullability_validation" == "true" ]]; then
  required_packages+=(
    "AdamE.Firebase.iOS.AppCheck"
    "AdamE.Firebase.iOS.CloudFirestore"
    "AdamE.Firebase.iOS.CloudMessaging"
    "AdamE.Firebase.iOS.Crashlytics"
    "AdamE.Firebase.iOS.Database"
    "AdamE.Firebase.iOS.PerformanceMonitoring"
  )
  msbuild_args+=("-p:EnableNullabilityValidation=true")
fi

for package_name in "${required_packages[@]}"; do
  if ! find "$package_dir" -maxdepth 1 -name "${package_name}.*.nupkg" -print -quit | grep -q .; then
    echo "Missing package in local feed: $package_name" >&2
    exit 1
  fi
done

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

config_file="$project_dir/GoogleService-Info.plist"
if [[ ! -f "$config_file" ]]; then
  echo "Missing Firebase config file: $config_file" >&2
  exit 1
fi

echo "Restoring FirebaseFoundationE2E from $package_dir"
dotnet restore "$project_file" --configfile "$restore_config" "${msbuild_args[@]}"

echo "Building FirebaseFoundationE2E for iOS simulator"
dotnet build "$project_file" \
  --configuration "$configuration" \
  --framework net9.0-ios \
  --no-restore \
  -p:Platform=iPhoneSimulator \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  "${msbuild_args[@]}"

app_path="$(find "$project_dir/bin" -path "*iPhoneSimulator/$configuration/net9.0-ios/iossimulator-arm64/FirebaseFoundationE2E.app" -print -quit)"
if [[ ! -d "$app_path" ]]; then
  echo "Built app not found: $app_path" >&2
  exit 1
fi

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
  --predicate "processImagePath ENDSWITH[c] 'FirebaseFoundationE2E' OR eventMessage CONTAINS[c] 'E2E_STATUS:' OR eventMessage CONTAINS[c] 'E2E_RESULT:'" \
  > "$log_file" 2>&1) &
log_pid="$!"

echo "Installing app"
xcrun simctl uninstall "$simulator_udid" "$bundle_id" >/dev/null 2>&1 || true
xcrun simctl install "$simulator_udid" "$app_path"

echo "Launching app"
xcrun simctl launch --terminate-running-process "$simulator_udid" "$bundle_id" >> "$log_file" 2>&1

data_container="$(xcrun simctl get_app_container "$simulator_udid" "$bundle_id" data)"
container_result_file="$data_container/Library/Caches/firebase-foundation-e2e-result.json"

timeout_seconds="${E2E_TIMEOUT_SECONDS:-90}"
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

success="$(
  /usr/bin/plutil -extract Success raw -o - "$result_file" 2>/dev/null || true
)"

echo "E2E result file: $result_file"
cat "$result_file"

if [[ "$success" == "true" ]]; then
  echo
  echo "Firebase foundation E2E passed."
  xcrun simctl terminate "$simulator_udid" "$bundle_id" >/dev/null 2>&1 || true
  exit 0
fi

echo
echo "Firebase foundation E2E failed." >&2
xcrun simctl terminate "$simulator_udid" "$bundle_id" >/dev/null 2>&1 || true
exit 1
