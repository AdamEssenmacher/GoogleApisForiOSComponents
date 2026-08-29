#!/bin/zsh
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/check-maps-consumers.sh [options]

  --package-dir <dir>        Local NuGet feed (default: output)
  --package-version <ver>    AdamE.Google.iOS.Maps version to consume
  --allow-xcode-mismatch     Pass ValidateXcodeVersion=false for local diagnostics
  --keep-work                Retain the temporary consumer projects after a failure

Builds direct and transitive net10.0-ios consumers of the locally packed Maps package. Verifies
the native symbol is linked and the app contains exactly one complete GoogleMaps.bundle.
EOF
}

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
source_targets="$repo_root/source/Google/Maps/Maps.targets"
package_id="AdamE.Google.iOS.Maps"
resource_bundle="GoogleMaps.bundle"
package_dir="$repo_root/output"
package_version=""
allow_xcode_mismatch="false"
keep_work="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --package-dir) package_dir="$2"; shift 2 ;;
    --package-version) package_version="$2"; shift 2 ;;
    --allow-xcode-mismatch) allow_xcode_mismatch="true"; shift ;;
    --keep-work) keep_work="true"; shift ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 1 ;;
  esac
done

[[ "$package_dir" != /* ]] && package_dir="$repo_root/$package_dir"

if [[ -n "$package_version" ]]; then
  nupkg="$package_dir/$package_id.$package_version.nupkg"
else
  nupkgs=("$package_dir"/"$package_id".*.nupkg(N))
  if (( ${#nupkgs[@]} > 1 )); then
    echo "Multiple $package_id packages found in $package_dir; pass --package-version." >&2
    printf '  %s\n' "${nupkgs[@]}" >&2
    exit 1
  fi
  nupkg="${nupkgs[1]:-}"
  package_version="${${nupkg:t}#$package_id.}"
  package_version="${package_version%.nupkg}"
fi

if [[ -z "${nupkg:-}" || ! -f "$nupkg" ]]; then
  echo "No $package_id package found in $package_dir" >&2
  exit 1
fi

echo "Testing $package_id $package_version from $nupkg"

# NuGet treats /var and /private/var as different project identities even though one is a symlink.
# Resolve mktemp's result so the ProjectReference graph in the transitive shape remains connected.
work="$(cd "$(mktemp -d)" && pwd -P)"
# Maps.targets concatenates this property while it is evaluated, before XBD normalizes it.
xbd_dir="$work/xbd/"
packages_dir="$work/packages"
artifacts_dir="$repo_root/artifacts/maps-resource-integrity"
mkdir -p "$xbd_dir" "$packages_dir"
# Keep the generated projects on the same pinned SDK/workload set as the repository. The
# .NET SDK resolves global.json from the project tree, not from this script's working directory.
cp "$repo_root/global.json" "$work/global.json"

failures=0
completed="false"
pass() { print -r -- "  PASS  $1"; }
fail() { print -r -- "  FAIL  $1" >&2; failures=$((failures + 1)); }

cleanup() {
  local exit_status=$?

  if [[ "$completed" != "true" ]]; then
    mkdir -p "$artifacts_dir"
    cp "$work"/*.diff(N) "$work"/*.log(N) "$work"/*.txt(N) "$artifacts_dir/" 2>/dev/null || true
    print -r -- "Diagnostics copied to $artifacts_dir" >&2
  fi

  if [[ "$completed" != "true" && "$keep_work" == "true" ]]; then
    print -r -- "Diagnostic scaffold kept at $work" >&2
  else
    rm -rf "$work"
  fi

  return "$exit_status"
}
trap cleanup EXIT

echo
echo "Packaged MSBuild integration"
for folder in build buildTransitive; do
  packaged_targets="$work/$folder.targets"
  if unzip -p "$nupkg" "$folder/$package_id.targets" > "$packaged_targets" 2>/dev/null; then
    if cmp -s "$source_targets" "$packaged_targets"; then
      pass "$folder/$package_id.targets matches source"
    else
      fail "$folder/$package_id.targets differs from source/Google/Maps/Maps.targets"
    fi
  else
    fail "$folder/$package_id.targets is missing from the package"
  fi
done

if (( failures > 0 )); then
  echo "$failures package integration check(s) failed." >&2
  exit 1
fi

cat > "$work/NuGet.config" <<EOF
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

app_properties='
    <TargetFramework>net10.0-ios</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <SupportedOSPlatformVersion>15.0</SupportedOSPlatformVersion>
    <RuntimeIdentifier>iossimulator-arm64</RuntimeIdentifier>
    <Platform>iPhoneSimulator</Platform>
    <IsPackable>false</IsPackable>
    <ProvisioningType>manual</ProvisioningType>'

msbuild_args=("-p:XamarinBuildDownloadDir=$xbd_dir")
[[ "$allow_xcode_mismatch" == "true" ]] && msbuild_args+=("-p:ValidateXcodeVersion=false")

write_app_sources() {
  local directory="$1"
  local distance_expression="$2"
  mkdir -p "$directory"

  cat > "$directory/Main.cs" <<'EOF'
using UIKit;
UIApplication.Main(args, null, typeof(MapsConsumer.AppDelegate));
EOF

  cat > "$directory/AppDelegate.cs" <<EOF
using Foundation;
using UIKit;

namespace MapsConsumer;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        double distance = $distance_expression;
        System.Console.WriteLine(distance);
        return true;
    }
}
EOF

  cat > "$directory/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key>
  <string>com.googleapisforioscomponents.tests.mapsconsumer</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
</dict>
</plist>
EOF
}

expected_manifest="$work/expected-bundle-files.txt"

locate_upstream_bundle() {
  local matches count
  matches="$(find "$xbd_dir" -type d -path '*/Maps/Resources/GoogleMapsResources/GoogleMaps.bundle' -print)"
  count="$(print -r -- "$matches" | sed '/^$/d' | wc -l | tr -d ' ')"
  if [[ "$count" != "1" ]]; then
    print -r -- "expected one extracted upstream GoogleMaps.bundle, found $count" >&2
    return 1
  fi
  print -r -- "$matches"
}

write_file_manifest() {
  local root="$1"
  local destination="$2"
  (cd "$root" && find . -type f -print | sed 's|^\./||' | LC_ALL=C sort) > "$destination"
}

assert_app() {
  local shape="$1"
  local app_path="$2"
  local upstream_bundle bundle_count app_bundle actual_manifest content_mismatches
  local binary_name binary symbol_count relative_path

  if [[ -z "$app_path" || ! -d "$app_path" ]]; then
    fail "$shape: app bundle was not produced"
    return
  fi

  if ! upstream_bundle="$(locate_upstream_bundle)"; then
    fail "$shape: could not resolve the extracted upstream resource bundle"
    return
  fi
  if [[ ! -s "$expected_manifest" ]]; then
    write_file_manifest "$upstream_bundle" "$expected_manifest"
  fi

  bundle_count="$(find "$app_path" -type d -name "$resource_bundle" -print | wc -l | tr -d ' ')"
  if [[ "$bundle_count" != "1" ]]; then
    fail "$shape: expected exactly one $resource_bundle, found $bundle_count"
    return
  fi

  app_bundle="$(find "$app_path" -type d -name "$resource_bundle" -print -quit)"
  if [[ "$app_bundle" != "$app_path/$resource_bundle" ]]; then
    fail "$shape: $resource_bundle is not at the app root ($app_bundle)"
  else
    pass "$shape: exactly one root $resource_bundle is present"
  fi

  actual_manifest="$work/$shape-bundle-files.txt"
  write_file_manifest "$app_bundle" "$actual_manifest"
  if diff -u "$expected_manifest" "$actual_manifest" > "$work/$shape-bundle.diff"; then
    pass "$shape: bundle file set matches upstream ($(wc -l < "$actual_manifest" | tr -d ' ') files)"

    content_mismatches="$work/$shape-content-mismatches.txt"
    : > "$content_mismatches"
    while IFS= read -r relative_path; do
      if ! cmp -s "$upstream_bundle/$relative_path" "$app_bundle/$relative_path"; then
        print -r -- "$relative_path" >> "$content_mismatches"
      fi
    done < "$expected_manifest"

    if [[ -s "$content_mismatches" ]]; then
      fail "$shape: bundle contents differ from upstream (see $content_mismatches)"
    else
      pass "$shape: bundle contents are byte-for-byte identical to upstream"
    fi
  else
    fail "$shape: bundle file set differs from upstream (see $work/$shape-bundle.diff)"
  fi

  binary_name="$(/usr/bin/plutil -extract CFBundleExecutable raw -o - "$app_path/Info.plist" 2>/dev/null || true)"
  [[ -z "$binary_name" ]] && binary_name="${${app_path:t}%.app}"
  binary="$app_path/$binary_name"
  if [[ ! -f "$binary" ]]; then
    fail "$shape: app executable is missing at $binary"
    return
  fi

  symbol_count="$(nm -U "$binary" 2>/dev/null | awk '$NF == "_GMSGeometryDistance" { count++ } END { print count + 0 }')"
  if [[ "${symbol_count:-0}" -gt 0 ]]; then
    pass "$shape: GMSGeometryDistance is linked into the app"
  else
    fail "$shape: GMSGeometryDistance is absent from the app binary"
  fi

  if find "$app_path/Frameworks" -type d -name 'GoogleMaps.framework' -print -quit 2>/dev/null | grep -c . >/dev/null; then
    fail "$shape: static GoogleMaps.framework was unexpectedly copied into App.app/Frameworks"
  else
    pass "$shape: no dynamic GoogleMaps.framework copy is present"
  fi
}

build_and_assert() {
  local shape="$1"
  local project="$2"
  local assembly_name="$3"
  local project_directory="${project:h}"
  local log="$work/$shape.log"
  local restored_nupkg="$packages_dir/${package_id:l}/$package_version/${package_id:l}.$package_version.nupkg"

  if ! dotnet restore "$project" \
      --configfile "$work/NuGet.config" \
      --packages "$packages_dir" \
      "${msbuild_args[@]}" > "$log" 2>&1; then
    fail "$shape: restore failed (see $log)"
    tail -25 "$log" >&2
    return
  fi

  if [[ ! -f "$restored_nupkg" ]]; then
    fail "$shape: the restored package is missing at $restored_nupkg"
    return
  elif ! cmp -s "$nupkg" "$restored_nupkg"; then
    fail "$shape: restore did not consume the locally packed Maps package"
    return
  else
    pass "$shape: restore consumed the locally packed Maps package"
  fi

  if ! dotnet build "$project" \
      --configuration Debug \
      --no-restore \
      "${msbuild_args[@]}" >> "$log" 2>&1; then
    fail "$shape: build failed (see $log)"
    tail -25 "$log" >&2
    return
  fi

  assert_app "$shape" "$(find "$project_directory/bin" -type d -name "$assembly_name.app" -print -quit)"
}

echo
echo "Shape: direct (app -> package)"
direct="$work/direct"
write_app_sources "$direct" 'Google.Maps.GeometryUtils.Distance(new CoreLocation.CLLocationCoordinate2D(0, 0), new CoreLocation.CLLocationCoordinate2D(1, 1))'
cat > "$direct/DirectApp.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>$app_properties
    <AssemblyName>DirectApp</AssemblyName>
    <RootNamespace>MapsConsumer</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$package_id" Version="$package_version" />
  </ItemGroup>
</Project>
EOF
build_and_assert "direct" "$direct/DirectApp.csproj" "DirectApp"

echo
echo "Shape: library (app -> class library -> package)"
library_root="$work/library"
mkdir -p "$library_root/Lib"
cat > "$library_root/Lib/MapsLib.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-ios</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <SupportedOSPlatformVersion>15.0</SupportedOSPlatformVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$package_id" Version="$package_version" />
  </ItemGroup>
</Project>
EOF
cat > "$library_root/Lib/Probe.cs" <<'EOF'
namespace MapsConsumer.Library;

public static class Probe
{
    public static double Distance() => Google.Maps.GeometryUtils.Distance(
        new CoreLocation.CLLocationCoordinate2D(0, 0),
        new CoreLocation.CLLocationCoordinate2D(1, 1));
}
EOF

write_app_sources "$library_root/App" 'MapsConsumer.Library.Probe.Distance()'
cat > "$library_root/App/LibraryApp.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>$app_properties
    <AssemblyName>LibraryApp</AssemblyName>
    <RootNamespace>MapsConsumer</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Lib/MapsLib.csproj" />
  </ItemGroup>
</Project>
EOF
build_and_assert "library" "$library_root/App/LibraryApp.csproj" "LibraryApp"

echo
if (( failures > 0 )); then
  echo "$failures Maps consumer check(s) failed." >&2
  exit 1
fi

completed="true"
echo "All Maps consumer checks passed."
