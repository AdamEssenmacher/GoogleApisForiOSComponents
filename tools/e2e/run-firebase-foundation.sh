#!/bin/zsh
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/e2e/run-firebase-foundation.sh [--package-dir output] [--configuration Release] [--enable-nullability-validation] [--runtime-drift-case <id>] [--binding-surface-target <target|all>]
EOF
}

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
project_dir="$repo_root/tests/E2E/Firebase.Foundation/FirebaseFoundationE2E"
project_file="$project_dir/FirebaseFoundationE2E.csproj"
bundle_id="com.googleapisforioscomponents.tests.firebase.e2e"
configuration="Release"
enable_nullability_validation="false"
runtime_drift_case=""
binding_surface_target=""
package_dir="$repo_root/output"
artifacts_dir="$repo_root/tests/E2E/Firebase.Foundation/artifacts"
log_file="$artifacts_dir/firebase-foundation-sim.log"
result_file="$artifacts_dir/firebase-foundation-result.json"
restore_config="$artifacts_dir/NuGet.generated.config"
repo_restore_config="$repo_root/tests/E2E/Firebase.Foundation/NuGet.config"
packages_cache_dir="$artifacts_dir/packages"
runtime_drift_manifest="$repo_root/tests/E2E/Firebase.Foundation/runtime-drift-cases.json"
runtime_drift_props="$artifacts_dir/runtime-drift-case.generated.props"
runtime_drift_info="$artifacts_dir/runtime-drift-case.info"
runtime_drift_method=""
runtime_drift_binding_package=""
binding_surface_manifest="$repo_root/tests/E2E/Firebase.Foundation/binding-surface-coverage.json"
binding_surface_document="$artifacts_dir/binding-surface-coverage.generated.json"
binding_surface_props="$artifacts_dir/binding-surface-coverage.generated.props"
binding_surface_info="$artifacts_dir/binding-surface-coverage.info"

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
    --runtime-drift-case)
      runtime_drift_case="$2"
      shift 2
      ;;
    --binding-surface-target)
      binding_surface_target="$2"
      shift 2
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

rm -rf "$packages_cache_dir"
mkdir -p "$packages_cache_dir"

selected_modes=0
[[ "$enable_nullability_validation" == "true" ]] && selected_modes=$((selected_modes + 1))
[[ -n "$runtime_drift_case" ]] && selected_modes=$((selected_modes + 1))
[[ -n "$binding_surface_target" ]] && selected_modes=$((selected_modes + 1))
if (( selected_modes > 1 )); then
  echo "--enable-nullability-validation, --runtime-drift-case, and --binding-surface-target are mutually exclusive." >&2
  exit 1
fi

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
restore_args=()
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

if [[ -n "$runtime_drift_case" ]]; then
  if [[ ! -f "$runtime_drift_manifest" ]]; then
    echo "Missing runtime drift manifest: $runtime_drift_manifest" >&2
    exit 1
  fi

  python3 - "$runtime_drift_manifest" "$runtime_drift_case" "$runtime_drift_props" > "$runtime_drift_info" <<'PY'
import json
import pathlib
import re
import sys
from xml.sax.saxutils import escape

manifest_path = pathlib.Path(sys.argv[1])
case_id = sys.argv[2]
props_path = pathlib.Path(sys.argv[3])
manifest = json.loads(manifest_path.read_text())

case = next((entry for entry in manifest.get("cases", []) if entry.get("id") == case_id), None)
if case is None:
    available = ", ".join(sorted(entry.get("id", "<missing>") for entry in manifest.get("cases", [])))
    raise SystemExit(f"Unknown runtime drift case '{case_id}'. Available cases: {available}")

method = case.get("method")
binding_package = case.get("bindingPackage")
packages = case.get("packages", [])

if not method or not binding_package or packages is None:
    raise SystemExit(f"Runtime drift case '{case_id}' is missing required manifest fields.")

symbol = "ENABLE_RUNTIME_DRIFT_CASE_" + re.sub(r"[^A-Za-z0-9]+", "_", case_id).strip("_").upper()
props_path.write_text(
    "<Project>\n"
    "  <PropertyGroup>\n"
    f"    <RuntimeDriftCase>{escape(case_id)}</RuntimeDriftCase>\n"
    f"    <RuntimeDriftCaseMethod>{escape(method)}</RuntimeDriftCaseMethod>\n"
    f"    <DefineConstants>$(DefineConstants);ENABLE_RUNTIME_DRIFT_CASE;{escape(symbol)}</DefineConstants>\n"
    "  </PropertyGroup>\n"
    "  <ItemGroup>\n"
    + "".join(
        f"    <PackageReference Include=\"{escape(package['id'])}\" Version=\"{escape(package['version'])}\" />\n"
        for package in packages
    )
    + "  </ItemGroup>\n"
    "</Project>\n"
)

print(method)
print(binding_package)
for package in packages:
    print(package["id"])
PY

  runtime_drift_details=("${(@f)$(<"$runtime_drift_info")}")
  runtime_drift_method="${runtime_drift_details[1]}"
  runtime_drift_binding_package="${runtime_drift_details[2]}"
  required_packages+=("$runtime_drift_binding_package")
  for (( i = 3; i <= ${#runtime_drift_details[@]}; i++ )); do
    required_packages+=("${runtime_drift_details[$i]}")
  done

  msbuild_args+=(
    "-p:RuntimeDriftCase=$runtime_drift_case"
    "-p:RuntimeDriftCaseMethod=$runtime_drift_method"
    "-p:RuntimeDriftCasePropsPath=$runtime_drift_props"
  )
  restore_args+=("--force-evaluate")

  echo "Runtime drift case: $runtime_drift_case ($runtime_drift_binding_package)"
fi

if [[ -n "$binding_surface_target" ]]; then
  if [[ ! -f "$binding_surface_manifest" ]]; then
    echo "Missing binding surface coverage manifest: $binding_surface_manifest" >&2
    exit 1
  fi

  echo "Generating binding surface coverage inventory for target: $binding_surface_target"
  dotnet run --project "$repo_root/scripts/FirebaseBindingAudit/FirebaseBindingAudit.csproj" -- \
    --generate-binding-surface-coverage \
    --repo-root "$repo_root" \
    --coverage-manifest "$binding_surface_manifest" \
    --coverage-output "$binding_surface_document" \
    --coverage-props-output "$binding_surface_props" \
    --binding-surface-target "$binding_surface_target" \
    > "$binding_surface_info"

  python3 - "$binding_surface_document" >> "$binding_surface_info" <<'PY'
import json
import pathlib
import sys

document = json.loads(pathlib.Path(sys.argv[1]).read_text())
seen = set()
for target in document.get("targets", []):
    for package in target.get("requiredPackages", []):
        package_id = package.get("id")
        if package_id and package_id not in seen:
            seen.add(package_id)
            print(package_id)
PY

  binding_surface_details=("${(@f)$(<"$binding_surface_info")}")
  for detail in "${binding_surface_details[@]}"; do
    if [[ "$detail" == AdamE.* ]]; then
      required_packages+=("$detail")
    fi
  done

  msbuild_args+=(
    "-p:BindingSurfaceCoverageTarget=$binding_surface_target"
    "-p:BindingSurfaceCoveragePropsPath=$binding_surface_props"
  )
  restore_args+=("--force-evaluate")

  echo "Binding surface coverage target: $binding_surface_target"
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
dotnet restore "$project_file" --configfile "$restore_config" --packages "$packages_cache_dir" "${restore_args[@]}" "${msbuild_args[@]}"

echo "Cleaning FirebaseFoundationE2E for iOS simulator"
dotnet clean "$project_file" \
  --configuration "$configuration" \
  --framework net9.0-ios \
  -p:Platform=iPhoneSimulator \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  "${msbuild_args[@]}"

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
