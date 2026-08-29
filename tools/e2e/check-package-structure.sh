#!/bin/zsh
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/e2e/check-package-structure.sh --target <Maps|Places> [options]

  --package-dir <dir>        Local NuGet feed (default: output)
  --package-version <ver>    Exact package version (required when the feed contains multiple)

Checks a packed binding package without building a consumer. It verifies that every managed TFM
contains the native XCFramework and linker metadata, that the resource bundle exactly matches the
embedded SDK, that packaged MSBuild files match source, and that Xamarin.Build.Download is absent.
EOF
}

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
target="Places"
package_version=""
package_dir="$repo_root/output"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      [[ $# -ge 2 ]] || { echo "--target requires a value" >&2; exit 1; }
      target="$2"; shift 2
      ;;
    --package-dir)
      [[ $# -ge 2 ]] || { echo "--package-dir requires a value" >&2; exit 1; }
      package_dir="$2"; shift 2
      ;;
    --package-version)
      [[ $# -ge 2 ]] || { echo "--package-version requires a value" >&2; exit 1; }
      package_version="$2"; shift 2
      ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 1 ;;
  esac
done

[[ "$package_dir" != /* ]] && package_dir="$repo_root/$package_dir"

typeset -A expected_slice_archs
case "$target" in
  Maps)
    package_id="AdamE.Google.iOS.Maps"
    assembly_name="Google.Maps"
    xcframework="GoogleMaps.xcframework"
    framework_binary="GoogleMaps"
    expected_slices=("ios-arm64" "ios-arm64_x86_64-simulator")
    expected_slice_archs=(
      "ios-arm64" "arm64"
      "ios-arm64_x86_64-simulator" "arm64 x86_64"
    )
    resource_bundle="GoogleMaps.bundle"
    expected_bundle_files=190
    expected_resource_mappings=190
    source_project="$repo_root/source/Google/Maps/Maps.csproj"
    source_build_targets="$repo_root/source/Google/Maps/Maps.targets"
    source_transitive_targets="$repo_root/source/Google/Maps/Maps.buildTransitive.targets"
    source_files=(
      "$source_project"
      "$source_build_targets"
      "$source_transitive_targets"
    )
    expected_kind="Framework"
    expected_smartlink="True"
    expected_forceload="True"
    expected_frameworks="Accelerate Contacts CoreData CoreGraphics CoreImage CoreLocation CoreTelephony CoreText GLKit ImageIO Metal OpenGLES QuartzCore Security SystemConfiguration UIKit"
    expected_linkerflags="-ObjC -lc++ -lz"
    upstream_bundle="$repo_root/externals/GoogleMaps.bundle"
    ;;
  Places)
    package_id="AdamE.Google.iOS.Places"
    assembly_name="Google.Places"
    xcframework="GooglePlaces.xcframework"
    framework_binary="GooglePlaces"
    expected_slices=("ios-arm64" "ios-arm64_x86_64-simulator")
    expected_slice_archs=(
      "ios-arm64" "arm64"
      "ios-arm64_x86_64-simulator" "arm64 x86_64"
    )
    resource_bundle="GooglePlaces.bundle"
    expected_bundle_files=59
    expected_resource_mappings=""
    source_project="$repo_root/source/Google/Places/Places.csproj"
    source_build_targets="$repo_root/source/Google/Places/Places.targets"
    source_transitive_targets="$repo_root/source/Google/Places/Places.buildTransitive.targets"
    source_files=(
      "$source_project"
      "$source_build_targets"
      "$source_transitive_targets"
    )
    expected_kind="Framework"
    expected_smartlink="True"
    expected_forceload="True"
    expected_frameworks="CoreGraphics CoreLocation QuartzCore Security UIKit"
    expected_linkerflags="-ObjC"
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

echo "Checking $package_id $package_version from $nupkg"

work="$(cd "$(mktemp -d)" && pwd -P)"
diagnostics="$work/diagnostics"
artifacts_dir="$repo_root/tests/E2E/Google.Foundation/artifacts/package-structure-$target"
mkdir -p "$diagnostics"
rm -rf "$artifacts_dir"

failures=0
completed="false"
pass() { print -r -- "  PASS  $1"; }
fail() { print -r -- "  FAIL  $1" >&2; failures=$((failures + 1)); }

cleanup() {
  local exit_status=$?

  if [[ "$completed" != "true" ]]; then
    mkdir -p "$artifacts_dir"
    cp -R "$diagnostics"/. "$artifacts_dir/" 2>/dev/null || true
    print -r -- "$nupkg" > "$artifacts_dir/selected-package.txt"
    print -r -- "Diagnostics copied to $artifacts_dir" >&2
  fi

  rm -rf "$work"
  return "$exit_status"
}
trap cleanup EXIT

if ! unzip -Z1 "$nupkg" > "$diagnostics/package-files.txt" 2> "$diagnostics/unzip-list.log"; then
  fail "could not list package contents"
  exit 1
fi
if ! unzip -q "$nupkg" -d "$work/pkg" 2> "$diagnostics/unzip.log"; then
  fail "could not extract package"
  exit 1
fi

write_file_manifest() {
  local root="$1" destination="$2"
  (cd "$root" && find . -type f -print | sed 's|^\./||' | LC_ALL=C sort) > "$destination"
}

read_xml_value() {
  local file="$1" field="$2"
  sed -nE "s|.*<$field>([^<]*)</$field>.*|\\1|p" "$file" | sed -n '1p'
}

check_manifest_value() {
  local manifest="$1" tfm="$2" field="$3" expected="$4" actual
  actual="$(read_xml_value "$manifest" "$field" || true)"
  if [[ "$actual" == "$expected" ]]; then
    pass "$tfm: manifest $field = $expected"
  else
    fail "$tfm: manifest $field = '$actual', expected '$expected'"
  fi
}

assembly_inspector=""
inspector_dir="$work/assembly-inspector"
mkdir -p "$inspector_dir"
cp "$repo_root/global.json" "$work/global.json"
cat > "$work/NuGet.config" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
EOF
cat > "$inspector_dir/AssemblyInspector.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
</Project>
EOF
cat > "$inspector_dir/Program.cs" <<'EOF'
using System.Diagnostics;
using System.Reflection;

var assemblyName = AssemblyName.GetAssemblyName(args[0]);
var fileVersion = FileVersionInfo.GetVersionInfo(args[0]).FileVersion ?? string.Empty;
Console.WriteLine($"{assemblyName.Name}|{assemblyName.Version}|{fileVersion}");
EOF
if dotnet restore "$inspector_dir/AssemblyInspector.csproj" \
    --configfile "$work/NuGet.config" \
    --packages "$work/inspector-packages" > "$diagnostics/inspector-restore.log" 2>&1 \
    && dotnet build "$inspector_dir/AssemblyInspector.csproj" \
      --configuration Release \
      --no-restore > "$diagnostics/inspector-build.log" 2>&1; then
  assembly_inspector="$inspector_dir/bin/Release/net10.0/AssemblyInspector.dll"
else
  fail "could not build the managed assembly identity inspector"
fi

echo
echo "Package identity"
nuspec="$work/pkg/$package_id.nuspec"
if [[ ! -f "$nuspec" ]]; then
  fail "$package_id.nuspec is missing"
else
  cp "$nuspec" "$diagnostics/package.nuspec"
  nuspec_id="$(read_xml_value "$nuspec" id || true)"
  nuspec_version="$(read_xml_value "$nuspec" version || true)"
  if [[ "$nuspec_id" == "$package_id" ]]; then
    pass "nuspec id = $package_id"
  else
    fail "nuspec id = '$nuspec_id', expected '$package_id'"
  fi
  if [[ "$nuspec_version" == "$package_version" ]]; then
    pass "nuspec version = $package_version"
  else
    fail "nuspec version = '$nuspec_version', expected '$package_version'"
  fi
fi

for version_field in AssemblyVersion FileVersion PackageVersion; do
  source_version="$(read_xml_value "$source_project" "$version_field" || true)"
  if [[ "$source_version" == "$package_version" ]]; then
    pass "source $version_field = $package_version"
  else
    fail "source $version_field = '$source_version', expected '$package_version'"
  fi
done

if [[ "$target" == "Maps" ]]; then
  if grep -Eq "Artifact GOOGLE_MAPS_ARTIFACT[[:space:]]*=.*\"$package_version\"" "$repo_root/components.cake"; then
    pass "components.cake Maps artifact version = $package_version"
  else
    fail "components.cake Maps artifact version does not match $package_version"
  fi
  if grep -Fq "| \`Maps\` | \`$package_version\` |" "$repo_root/Readme.md"; then
    pass "README Maps version = $package_version"
  else
    fail "README Maps version does not match $package_version"
  fi
fi

if [[ -n "$expected_resource_mappings" ]]; then
  include_count="$(grep -c '<BundleResource Include=' "$source_build_targets" || true)"
  logical_count="$(grep -c '<LogicalName>' "$source_build_targets" || true)"
  duplicate_count="$(grep -oE '<LogicalName>[^<]+' "$source_build_targets" | sed 's/<LogicalName>//' | LC_ALL=C sort | uniq -d | wc -l | tr -d ' ')"
  if [[ "$include_count" == "$expected_resource_mappings" && "$logical_count" == "$expected_resource_mappings" && "$duplicate_count" == "0" ]]; then
    pass "Maps targets contain $expected_resource_mappings unique resource mappings"
  else
    fail "Maps targets contain Include=$include_count LogicalName=$logical_count duplicate=$duplicate_count; expected $expected_resource_mappings unique mappings"
  fi
fi

echo
echo "Native payload per TFM"
lib_dirs=("$work/pkg"/lib/*(/N))
if [[ ${#lib_dirs[@]} -eq 0 ]]; then
  fail "package contains no lib/ folders"
fi

for lib_dir in "${lib_dirs[@]}"; do
  tfm="${lib_dir:t}"
  payload_root="$work/payload-$tfm"
  mkdir -p "$payload_root"

  if [[ ! -f "$lib_dir/$assembly_name.dll" ]]; then
    fail "$tfm: missing $assembly_name.dll"
    continue
  fi
  if [[ -n "$assembly_inspector" ]]; then
    assembly_identity="$(dotnet "$assembly_inspector" "$lib_dir/$assembly_name.dll" 2>> "$diagnostics/assembly-inspector.log" || true)"
    print -r -- "$tfm|$assembly_identity" >> "$diagnostics/assembly-identities.txt"
    if [[ "$assembly_identity" == "$assembly_name|$package_version|$package_version" ]]; then
      pass "$tfm: managed assembly identity and file version = $package_version"
    else
      fail "$tfm: managed assembly identity '$assembly_identity', expected '$assembly_name|$package_version|$package_version'"
    fi
  fi

  if [[ -f "$lib_dir/$assembly_name.resources.zip" ]]; then
    payload="$lib_dir/$assembly_name.resources.zip"
    if ! unzip -Z1 "$payload" > "$diagnostics/payload-$tfm-files.txt" 2> "$diagnostics/payload-$tfm-list.log"; then
      fail "$tfm: could not list $assembly_name.resources.zip"
      continue
    fi
    if ! unzip -q "$payload" -d "$payload_root" 2> "$diagnostics/payload-$tfm-unzip.log"; then
      fail "$tfm: could not extract $assembly_name.resources.zip"
      continue
    fi
    pass "$tfm: native payload present ($assembly_name.resources.zip)"
  elif [[ -d "$lib_dir/$assembly_name.resources" ]]; then
    payload="$lib_dir/$assembly_name.resources"
    cp -R "$payload"/. "$payload_root/"
    write_file_manifest "$payload_root" "$diagnostics/payload-$tfm-files.txt"
    pass "$tfm: native payload present ($assembly_name.resources/)"
  else
    fail "$tfm: no native payload (neither $assembly_name.resources.zip nor $assembly_name.resources/)"
    continue
  fi

  framework_root="$payload_root/$xcframework"
  framework_info="$framework_root/Info.plist"
  if [[ ! -d "$framework_root" ]]; then
    fail "$tfm: $xcframework missing from native payload"
    continue
  fi
  pass "$tfm: $xcframework present"

  if [[ -f "$framework_info" ]]; then
    /usr/bin/plutil -p "$framework_info" > "$diagnostics/xcframework-$tfm.txt" 2>&1 || true
    for slice in "${expected_slices[@]}"; do
      if [[ "$(grep -F -c "\"$slice\"" "$diagnostics/xcframework-$tfm.txt" || true)" -gt 0 ]]; then
        pass "$tfm: slice $slice declared"
      else
        fail "$tfm: slice $slice missing from $xcframework/Info.plist"
      fi

      slice_binary="$framework_root/$slice/$framework_binary.framework/$framework_binary"
      if [[ ! -s "$slice_binary" ]]; then
        fail "$tfm: slice $slice framework binary is missing or empty"
        continue
      fi

      file_description="$(file -b "$slice_binary" 2>/dev/null || true)"
      print -r -- "$file_description" > "$diagnostics/framework-$tfm-$slice-file.txt"
      if [[ "$file_description" == *"Mach-O"* ]]; then
        pass "$tfm: slice $slice contains a Mach-O framework binary"
      else
        fail "$tfm: slice $slice framework binary is not Mach-O"
      fi

      actual_archs="$(lipo -archs "$slice_binary" 2>/dev/null \
        | tr ' ' '\n' | sed '/^$/d' | LC_ALL=C sort | tr '\n' ' ' | sed 's/ $//' || true)"
      expected_archs="${expected_slice_archs[$slice]}"
      print -r -- "$actual_archs" > "$diagnostics/framework-$tfm-$slice-archs.txt"
      if [[ "$actual_archs" == "$expected_archs" ]]; then
        pass "$tfm: slice $slice architectures = $expected_archs"
      else
        fail "$tfm: slice $slice architectures = '$actual_archs', expected '$expected_archs'"
      fi
    done
  else
    fail "$tfm: $xcframework/Info.plist is missing"
  fi

  manifest="$payload_root/manifest"
  if [[ -f "$manifest" ]]; then
    cp "$manifest" "$diagnostics/native-manifest-$tfm.xml"
    check_manifest_value "$manifest" "$tfm" Kind "$expected_kind"
    check_manifest_value "$manifest" "$tfm" SmartLink "$expected_smartlink"
    check_manifest_value "$manifest" "$tfm" ForceLoad "$expected_forceload"
    check_manifest_value "$manifest" "$tfm" Frameworks "$expected_frameworks"
    check_manifest_value "$manifest" "$tfm" LinkerFlags "$expected_linkerflags"
  else
    fail "$tfm: native reference manifest is missing"
  fi

  if [[ "$target" == "Maps" ]]; then
    upstream_bundle="$repo_root/externals/GoogleMaps.bundle"
  else
    upstream_bundle="$framework_root/ios-arm64/GooglePlaces.framework/Resources/$resource_bundle"
  fi
  packaged_bundle="$work/pkg/build/$resource_bundle"
  if [[ ! -d "$upstream_bundle" || ! -d "$packaged_bundle" ]]; then
    [[ -d "$upstream_bundle" ]] || fail "$tfm: verified upstream $resource_bundle is missing"
    [[ -d "$packaged_bundle" ]] || fail "build/$resource_bundle is missing from the package"
    continue
  fi

  upstream_manifest="$diagnostics/upstream-bundle-$tfm.txt"
  packaged_manifest="$diagnostics/packaged-bundle-$tfm.txt"
  bundle_diff="$diagnostics/bundle-files-$tfm.diff"
  content_mismatches="$diagnostics/bundle-content-$tfm.txt"
  write_file_manifest "$upstream_bundle" "$upstream_manifest"
  write_file_manifest "$packaged_bundle" "$packaged_manifest"

  if diff -u "$upstream_manifest" "$packaged_manifest" > "$bundle_diff"; then
    pass "$tfm: packaged bundle file set matches the embedded SDK"
  else
    fail "$tfm: packaged bundle file set differs from the embedded SDK"
  fi

  : > "$content_mismatches"
  while IFS= read -r relative_path; do
    if [[ ! -f "$packaged_bundle/$relative_path" ]] || ! cmp -s "$upstream_bundle/$relative_path" "$packaged_bundle/$relative_path"; then
      print -r -- "$relative_path" >> "$content_mismatches"
    fi
  done < "$upstream_manifest"
  if [[ -s "$content_mismatches" ]]; then
    fail "$tfm: packaged bundle contents differ from the embedded SDK"
  else
    pass "$tfm: packaged bundle contents match the embedded SDK byte-for-byte"
  fi
done

echo
echo "Resource bundle"
packaged_bundle="$work/pkg/build/$resource_bundle"
if [[ -d "$packaged_bundle" ]]; then
  actual_count="$(find "$packaged_bundle" -type f | wc -l | tr -d ' ')"
  if [[ "$actual_count" == "$expected_bundle_files" ]]; then
    pass "build/$resource_bundle contains $actual_count files"
  else
    fail "build/$resource_bundle contains $actual_count files, expected $expected_bundle_files"
  fi
else
  fail "build/$resource_bundle is missing from the package"
fi
if [[ -e "$work/pkg/buildTransitive/$resource_bundle" ]]; then
  fail "buildTransitive unexpectedly contains a second $resource_bundle"
else
  pass "buildTransitive does not duplicate $resource_bundle"
fi

echo
echo "MSBuild integration"
check_packaged_targets() {
  local folder="$1" source="$2" packaged
  packaged="$work/pkg/$folder/$package_id.targets"
  if [[ ! -f "$packaged" ]]; then
    fail "$folder/$package_id.targets is missing"
    return
  fi
  if cmp -s "$source" "$packaged"; then
    pass "$folder/$package_id.targets matches source"
  else
    diff -u "$source" "$packaged" > "$diagnostics/$folder-targets.diff" || true
    fail "$folder/$package_id.targets differs from source"
  fi
}
check_packaged_targets build "$source_build_targets"
check_packaged_targets buildTransitive "$source_transitive_targets"

transitive_targets="$work/pkg/buildTransitive/$package_id.targets"
expected_import="<Import Project=\"\$(MSBuildThisFileDirectory)../build/AdamE.Google.iOS.$target.targets\" />"
if [[ -f "$transitive_targets" ]] && grep -Fq "$expected_import" "$transitive_targets"; then
  pass "buildTransitive imports the primary build target"
else
  fail "buildTransitive does not import the primary build target"
fi

echo
echo "XamarinBuildDownload removal"
if [[ -f "$nuspec" ]]; then
  if grep -Eq "Xamarin\.Build\.Download|XamarinBuildDownload" "$nuspec"; then
    fail "nuspec still references Xamarin.Build.Download"
  else
    pass "nuspec has no Xamarin.Build.Download dependency"
  fi
fi

packaged_build_assets="$diagnostics/packaged-msbuild-assets.txt"
find "$work/pkg" -type f \( -name '*.props' -o -name '*.targets' \) -print \
  | LC_ALL=C sort > "$packaged_build_assets"

xbd_hits="$diagnostics/packaged-xbd-references.txt"
: > "$xbd_hits"
while IFS= read -r packaged_asset; do
  asset_matches="$(grep -En "Xamarin\.Build\.Download|XamarinBuildDownload" "$packaged_asset" || true)"
  if [[ -n "$asset_matches" ]]; then
    print -r -- "${packaged_asset#$work/pkg/}" >> "$xbd_hits"
    print -r -- "$asset_matches" >> "$xbd_hits"
  fi
done < "$packaged_build_assets"
if [[ -s "$xbd_hits" ]]; then
  fail "packaged MSBuild assets still reference Xamarin.Build.Download"
else
  pass "all packaged MSBuild assets are free of Xamarin.Build.Download"
fi

for source_file in "${source_files[@]}"; do
  if grep -Eq "Xamarin\.Build\.Download|XamarinBuildDownload" "$source_file"; then
    fail "${source_file#$repo_root/} still references Xamarin.Build.Download"
  else
    pass "${source_file#$repo_root/} is free of Xamarin.Build.Download"
  fi
done

echo
if (( failures > 0 )); then
  echo "$failures structural check(s) failed for $package_id." >&2
  exit 1
fi

completed="true"
echo "All structural checks passed for $package_id $package_version."
