#!/bin/zsh
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/e2e/check-consumer-shapes.sh --target <Places|SignIn> [options]

  --package-dir <dir>        Local NuGet feed (default: output)
  --package-version <ver>    Exact package version (required when the feed contains multiple)
  --allow-xcode-mismatch     Pass ValidateXcodeVersion=false (local escape hatch, not for CI)

Scaffolds throwaway consumer projects in a temp directory and builds them against the packed
package, to check that native payload and resources reach the app through more than the simplest
project shape.

Shapes covered:
  direct    app -> package
  library   app -> class library -> package   (exercises buildTransitive/)

A binding package's .targets historically gated its items on OutputType != 'Library', so the
library shape is where transitive delivery quietly breaks.

Targets differ in how the native payload arrives, and the assertions follow that:
  Places  static xcframework  -- bundle unpacked to the app root, symbol linked into the app binary
  SignIn  dynamic xcframework -- .framework copied to App.app/Frameworks with its bundle inside
EOF
}

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
target="Places"
package_version=""
package_dir="$repo_root/output"
allow_xcode_mismatch="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target) target="$2"; shift 2 ;;
    --package-version) package_version="$2"; shift 2 ;;
    --package-dir) package_dir="$2"; shift 2 ;;
    --allow-xcode-mismatch) allow_xcode_mismatch="true"; shift ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 1 ;;
  esac
done

[[ "$package_dir" != /* ]] && package_dir="$repo_root/$package_dir"

case "$target" in
  Places)
    package_id="AdamE.Google.iOS.Places"
    # GooglePlaces.xcframework is a static framework: the SDK links it into the app binary and
    # does not copy any .framework directory, so the package has to place the bundle at the app root.
    delivery="static"
    resource_bundle="GooglePlaces.bundle"
    managed_probe_type="Google.Places.AutocompleteFilter"
    lib_project="PlacesLib"
    probe_symbol="GMSPlaceRectangularLocationOption"
    expected_resource_file_count=59
    # A trivial app that never reaches a native entry point lets the linker drop the static
    # library, which is correct behaviour and not a delivery failure. Call into the native SDK so
    # the symbol assertion below actually measures whether the package delivered it.
    native_probe_decl='[System.Runtime.InteropServices.DllImport("__Internal", EntryPoint = "GMSPlaceRectangularLocationOption")]
    static extern System.IntPtr NativeProbe(CoreLocation.CLLocationCoordinate2D ne, CoreLocation.CLLocationCoordinate2D sw);'
    native_probe_call='System.Console.WriteLine(NativeProbe(new CoreLocation.CLLocationCoordinate2D(1, 1), new CoreLocation.CLLocationCoordinate2D(0, 0)));'
    ;;
  SignIn)
    package_id="AdamE.Google.iOS.SignIn"
    # GoogleSignIn.xcframework is a dynamic framework (MH_DYLIB, install name
    # @rpath/GoogleSignIn.framework/GoogleSignIn). The SDK copies the whole .framework into
    # App.app/Frameworks, and GoogleSignIn.bundle rides along inside it -- so the bundle is never
    # expected at the app root, and the native code is never linked into the app binary.
    delivery="dynamic"
    framework_dir="GoogleSignIn.framework"
    framework_binary="GoogleSignIn"
    resource_bundle="GoogleSignIn.bundle"
    managed_probe_type="Google.SignIn.SignIn"
    lib_project="SignInLib"
    # Touching the managed type is enough here: the framework is a native reference, so the SDK
    # copies it whenever the binding assembly survives the linker. There is no DllImport probe
    # because a dynamic framework's symbols live in the dylib, not in "__Internal".
    native_probe_decl=""
    native_probe_call=""
    expected_resource_file_count=""
    ;;
  *) echo "Unknown target: $target" >&2; exit 1 ;;
esac

if [[ -n "$package_version" ]]; then
  nupkg="$package_dir/$package_id.$package_version.nupkg"
  if [[ ! -f "$nupkg" ]]; then
    echo "No $package_id $package_version package found at $nupkg" >&2
    exit 1
  fi
else
  candidates=("$package_dir"/"$package_id".*.nupkg(N))
  nupkgs=()
  for candidate in "${candidates[@]}"; do
    [[ "$candidate" == *.symbols.nupkg ]] && continue
    nupkgs+=("$candidate")
  done

  case ${#nupkgs[@]} in
    0)
      echo "No $package_id package found in $package_dir" >&2
      exit 1
      ;;
    1)
      nupkg="${nupkgs[1]}"
      package_version="${${nupkg:t}#$package_id.}"
      package_version="${package_version%.nupkg}"
      ;;
    *)
      echo "Multiple $package_id packages found in $package_dir; pass --package-version:" >&2
      printf '  %s\n' "${nupkgs[@]:t}" >&2
      exit 1
      ;;
  esac
fi
echo "Testing $package_id $package_version from $nupkg"

msbuild_args=()
[[ "$allow_xcode_mismatch" == "true" ]] && msbuild_args+=("-p:ValidateXcodeVersion=false")

failures=0
pass() { print -r -- "  PASS  $1"; }
fail() { print -r -- "  FAIL  $1" >&2; failures=$((failures + 1)); }

# Resolve the symlink: macOS mktemp -d returns /var/folders/... which is a symlink to
# /private/var/folders/.... NuGet compares the two, silently drops the ProjectReference edge,
# and the library shape below fails for a reason that has nothing to do with packaging.
work="$(cd "$(mktemp -d)" && pwd -P)"
# Keep the selected local package isolated from any same-version copy in the user's global cache.
# This makes the check prove the nupkg named above rather than whichever copy NuGet restored first.
export NUGET_PACKAGES="$work/packages"
mkdir -p "$NUGET_PACKAGES"
# The scaffold lives outside the repository tree, so give its projects the same pinned SDK and
# workload set that packed the selected artifact. Without this, a fresh CI host can install the
# pinned iOS workload successfully and then resolve the consumer against a different workload set.
cp "$repo_root/global.json" "$work/global.json"
# Keep the scaffold when something fails -- these are throwaway projects, but a failure is not
# diagnosable without the build logs and the produced .app.
cleanup() {
  if (( failures > 0 )); then
    print -r -- "Scaffold kept for inspection: $work" >&2
  else
    rm -rf "$work"
  fi
}
trap cleanup EXIT

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

app_props='
    <TargetFramework>net10.0-ios</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <SupportedOSPlatformVersion>15.0</SupportedOSPlatformVersion>
    <RuntimeIdentifier>iossimulator-arm64</RuntimeIdentifier>
    <Platform>iPhoneSimulator</Platform>
    <IsPackable>false</IsPackable>
    <ProvisioningType>manual</ProvisioningType>'

# $2 is an expression that touches the binding's managed surface. Without it the app never
# references the binding assembly, the linker is free to drop it, and the native payload never has
# to be linked -- which would make the symbol assertion below fail for reasons unrelated to
# packaging.
write_app_sources() {
  local dir="$1" probe_expr="$2"
  mkdir -p "$dir"
  cat > "$dir/Main.cs" <<'EOF'
using UIKit;
UIApplication.Main(args, null, typeof(ShapeProbe.AppDelegate));
EOF
  cat > "$dir/AppDelegate.cs" <<EOF
using Foundation;
using UIKit;

namespace ShapeProbe;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    $native_probe_decl

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        System.Console.WriteLine($probe_expr);
        $native_probe_call
        return true;
    }
}
EOF
  cat > "$dir/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key>
  <string>com.googleapisforioscomponents.tests.shapeprobe</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
</dict>
</plist>
EOF
}

# Reports a check that is expected to fail today because of a defect that predates this harness.
# Visible in the output, but does not fail the run -- otherwise the only way to keep CI green would
# be to delete the check, and the gap would stop being visible at all.
known_gaps=0
known_gap() { print -r -- "  KNOWN  $1"; known_gaps=$((known_gaps + 1)); }

# $3: pass "known-gap" to downgrade this shape's failures to warnings.
assert_app() {
  local shape="$1" app_path="$2" mode="${3:-strict}"
  local report=fail
  [[ "$mode" == "known-gap" ]] && report=known_gap

  if [[ -z "$app_path" || ! -d "$app_path" ]]; then
    $report "$shape: app bundle was not produced"
    return
  fi

  # Read the executable name from Info.plist rather than deriving it from the bundle name.
  local exe_name binary
  exe_name="$(/usr/bin/plutil -extract CFBundleExecutable raw -o - "$app_path/Info.plist" 2>/dev/null || true)"
  [[ -z "$exe_name" ]] && exe_name="$(basename "${app_path%.app}")"
  binary="$app_path/$exe_name"

  if [[ ! -f "$binary" ]]; then
    $report "$shape: app binary not found at $binary (bundle contains: $(ls "$app_path" | tr '\n' ' '))"
    return
  fi

  if [[ "$delivery" == "dynamic" ]]; then
    # The framework is copied whole into App.app/Frameworks, so the bundle is expected inside it
    # rather than at the app root, and the native symbols stay in the dylib.
    local fw="$app_path/Frameworks/$framework_dir"

    if [[ -d "$fw" ]]; then
      pass "$shape: $framework_dir present in App.app/Frameworks"
    else
      $report "$shape: $framework_dir missing from App.app/Frameworks"
      return
    fi

    if [[ -f "$fw/$framework_binary" ]]; then
      pass "$shape: $framework_dir/$framework_binary delivered ($(file -b "$fw/$framework_binary" | head -1))"
    else
      $report "$shape: $framework_dir/$framework_binary missing"
    fi

    if [[ -d "$fw/$resource_bundle" ]]; then
      local count
      count="$(find "$fw/$resource_bundle" -type f | wc -l | tr -d ' ')"
      pass "$shape: $resource_bundle present inside $framework_dir ($count files)"
    else
      $report "$shape: $resource_bundle missing from $framework_dir"
    fi

    # A copied framework that the app never links is still a delivery failure at runtime.
    local link_count
    link_count="$(otool -L "$binary" 2>/dev/null | grep -c "$framework_dir/$framework_binary" || true)"
    if [[ "${link_count:-0}" -gt 0 ]]; then
      pass "$shape: app binary links @rpath/$framework_dir/$framework_binary"
    else
      $report "$shape: app binary has no load command for $framework_dir/$framework_binary"
    fi

    # The bundle must not also be dropped at the app root -- that would mean something is copying
    # it a second time, which is what the dead SignIn.targets used to attempt.
    if [[ -d "$app_path/$resource_bundle" ]]; then
      $report "$shape: $resource_bundle unexpectedly duplicated at the app root"
    fi
    return
  fi

  if [[ -d "$app_path/$resource_bundle" ]]; then
    local count
    count="$(find "$app_path/$resource_bundle" -type f | wc -l | tr -d ' ')"
    if [[ -n "$expected_resource_file_count" && "$count" -ne "$expected_resource_file_count" ]]; then
      $report "$shape: $resource_bundle contains $count files; expected $expected_resource_file_count"
    else
      pass "$shape: $resource_bundle present in app bundle ($count files)"
    fi
  else
    $report "$shape: $resource_bundle missing from app bundle"
  fi

  # Do not use `grep -q` here: it exits on the first match, nm takes SIGPIPE, and with pipefail the
  # successful match is reported as a failed pipeline. grep -c consumes all input, so no SIGPIPE.
  local symbol_count
  symbol_count="$(nm "$binary" 2>/dev/null | grep -c "$probe_symbol" || true)"

  if [[ "${symbol_count:-0}" -gt 0 ]]; then
    pass "$shape: $probe_symbol linked into the app binary ($symbol_count symbol(s))"
  else
    $report "$shape: $probe_symbol not found in $exe_name"
  fi
}

# ------------------------------------------------------------------ shape: direct
echo
echo "Shape: direct (app -> package)"
direct="$work/direct"
write_app_sources "$direct" "typeof($managed_probe_type).FullName!"
cat > "$direct/DirectApp.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>$app_props
    <AssemblyName>DirectApp</AssemblyName>
    <RootNamespace>ShapeProbe</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$package_id" Version="$package_version" />
  </ItemGroup>
</Project>
EOF

if dotnet build "$direct/DirectApp.csproj" -c Debug "${msbuild_args[@]}" > "$work/direct.log" 2>&1; then
  assert_app "direct" "$(find "$direct/bin" -name "DirectApp.app" -print -quit)"
else
  fail "direct: build failed (see $work/direct.log)"
  tail -20 "$work/direct.log" >&2
fi

# ------------------------------------------------------------------ shape: via class library
echo
echo "Shape: library (app -> class library -> package)"
lib_root="$work/library"
mkdir -p "$lib_root/Lib"
cat > "$lib_root/Lib/$lib_project.csproj" <<EOF
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
cat > "$lib_root/Lib/Probe.cs" <<EOF
namespace ShapeProbe.Lib;

public static class Probe
{
    public static string Describe() => typeof($managed_probe_type).FullName!;
}
EOF

write_app_sources "$lib_root/App" "ShapeProbe.Lib.Probe.Describe()"
cat > "$lib_root/App/LibraryApp.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>$app_props
    <AssemblyName>LibraryApp</AssemblyName>
    <RootNamespace>ShapeProbe</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Lib/$lib_project.csproj" />
  </ItemGroup>
</Project>
EOF

# This shape was previously reported as a known gap, on the theory that a package's MSBuild
# integration could not reach an app consuming it through a ProjectReference. That was an artefact
# of the scaffold rather than a packaging defect: mktemp -d handed back a symlinked /var path, NuGet
# dropped the ProjectReference edge, and the app silently never referenced the package at all. With
# the scaffold path canonicalised above, Places and SignIn both deliver correctly through a class
# library, so this is a strict check.
if dotnet build "$lib_root/App/LibraryApp.csproj" -c Debug "${msbuild_args[@]}" > "$work/library.log" 2>&1; then
  assert_app "library" "$(find "$lib_root/App/bin" -name "LibraryApp.app" -print -quit)"
else
  fail "library: build failed (see $work/library.log)"
  grep -m3 "Undefined symbols\|error :" "$work/library.log" | sed 's/^/         /' || true
fi

echo
if (( failures > 0 )); then
  echo "$failures consumer-shape check(s) failed." >&2
  diagnostics_dir="$repo_root/tests/E2E/Google.Foundation/artifacts/consumer-shapes-$target"
  mkdir -p "$diagnostics_dir"
  cp "$work"/*.log "$diagnostics_dir/" 2>/dev/null || true
  exit 1
fi

if (( known_gaps > 0 )); then
  echo "Consumer-shape checks passed, with $known_gaps known gap(s) reported above."
else
  echo "All consumer-shape checks passed."
fi
