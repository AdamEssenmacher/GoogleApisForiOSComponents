#!/bin/zsh
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/e2e/check-offline-build.sh --target <Maps|Places> [options]

  --package-dir <dir>        Local NuGet feed (default: output)
  --package-version <ver>    Exact package version (required when the feed contains multiple)
  --allow-xcode-mismatch     Pass ValidateXcodeVersion=false (local escape hatch, not for CI)

Restores a throwaway consumer with network access, then builds it with --no-restore and HTTP(S)
egress pointed at a dead proxy. The selected local package must be restored byte-for-byte, the
isolated XamarinBuildDownload directory must remain empty, and the isolated NuGet cache must remain
unchanged throughout the offline build.
EOF
}

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
target="Places"
package_version=""
package_dir="$repo_root/output"
allow_xcode_mismatch="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      [[ $# -ge 2 ]] || { echo "--target requires a value" >&2; exit 1; }
      target="$2"; shift 2
      ;;
    --package-version)
      [[ $# -ge 2 ]] || { echo "--package-version requires a value" >&2; exit 1; }
      package_version="$2"; shift 2
      ;;
    --package-dir)
      [[ $# -ge 2 ]] || { echo "--package-dir requires a value" >&2; exit 1; }
      package_dir="$2"; shift 2
      ;;
    --allow-xcode-mismatch) allow_xcode_mismatch="true"; shift ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 1 ;;
  esac
done

[[ "$package_dir" != /* ]] && package_dir="$repo_root/$package_dir"

case "$target" in
  Maps)
    package_id="AdamE.Google.iOS.Maps"
    probe_expr="typeof(Google.Maps.MapView).FullName!"
    ;;
  Places)
    package_id="AdamE.Google.iOS.Places"
    probe_expr="typeof(Google.Places.AutocompleteFilter).FullName!"
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

echo "Offline build check: $package_id $package_version from $nupkg"

work="$(cd "$(mktemp -d)" && pwd -P)"
xbd_dir="$work/xbd/"
export NUGET_PACKAGES="$work/nuget/packages"
export NUGET_HTTP_CACHE_PATH="$work/nuget/http-cache"
export NUGET_PLUGINS_CACHE_PATH="$work/nuget/plugins-cache"
diagnostics="$work/diagnostics"
artifacts_dir="$repo_root/tests/E2E/Google.Foundation/artifacts/offline-$target"
mkdir -p "$xbd_dir" "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH" "$NUGET_PLUGINS_CACHE_PATH" "$diagnostics"
rm -rf "$artifacts_dir"
cp "$repo_root/global.json" "$work/global.json"

failures=0
completed="false"
pass() { print -r -- "  PASS  $1"; }
fail() { print -r -- "  FAIL  $1" >&2; failures=$((failures + 1)); }

cleanup() {
  local exit_status=$?

  if [[ "$completed" != "true" ]]; then
    cp "$work"/*.log(N) "$diagnostics/" 2>/dev/null || true
    cp "$work"/NuGet.config "$work"/global.json "$diagnostics/" 2>/dev/null || true
    cp "$work/app"/*.cs(N) "$work/app"/*.csproj(N) "$work/app"/Info.plist(N) "$diagnostics/" 2>/dev/null || true
    cp "$work/app/obj/project.assets.json" "$diagnostics/" 2>/dev/null || true
    mkdir -p "$artifacts_dir"
    cp -R "$diagnostics"/. "$artifacts_dir/" 2>/dev/null || true
    print -r -- "$nupkg" > "$artifacts_dir/selected-package.txt"
    print -r -- "Diagnostics copied to $artifacts_dir" >&2
  fi

  rm -rf "$work"
  return "$exit_status"
}
trap cleanup EXIT

snapshot_files() {
  local root="$1" destination="$2" relative_path
  : > "$destination"
  [[ -d "$root" ]] || return
  while IFS= read -r relative_path; do
    print -r -- "$(shasum -a 256 "$root/$relative_path" | awk '{ print $1 }')  $relative_path" >> "$destination"
  done < <(cd "$root" && find . -type f -print | sed 's|^\./||' | LC_ALL=C sort)
}

directory_is_empty() {
  [[ -z "$(find "$1" -mindepth 1 -print -quit 2>/dev/null)" ]]
}

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

mkdir -p "$work/app"
cat > "$work/app/OfflineApp.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-ios</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <SupportedOSPlatformVersion>15.0</SupportedOSPlatformVersion>
    <RuntimeIdentifier>iossimulator-arm64</RuntimeIdentifier>
    <Platform>iPhoneSimulator</Platform>
    <IsPackable>false</IsPackable>
    <ProvisioningType>manual</ProvisioningType>
    <AssemblyName>OfflineApp</AssemblyName>
    <RootNamespace>OfflineProbe</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$package_id" Version="$package_version" />
  </ItemGroup>
</Project>
EOF

cat > "$work/app/Main.cs" <<'EOF'
using UIKit;
UIApplication.Main(args, null, typeof(OfflineProbe.AppDelegate));
EOF

cat > "$work/app/AppDelegate.cs" <<EOF
using Foundation;
using UIKit;

namespace OfflineProbe;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        System.Console.WriteLine($probe_expr);
        return true;
    }
}
EOF

cat > "$work/app/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key>
  <string>com.googleapisforioscomponents.tests.offlineprobe</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
</dict>
</plist>
EOF

msbuild_args=("-p:XamarinBuildDownloadDir=$xbd_dir")
[[ "$allow_xcode_mismatch" == "true" ]] && msbuild_args+=("-p:ValidateXcodeVersion=false")

echo
echo "Restoring (network allowed)"
if ! dotnet restore "$work/app/OfflineApp.csproj" \
    --configfile "$work/NuGet.config" \
    --packages "$NUGET_PACKAGES" \
    --force-evaluate \
    "${msbuild_args[@]}" > "$work/restore.log" 2>&1; then
  fail "restore failed"
  tail -25 "$work/restore.log" >&2
  exit 1
fi
pass "consumer restore completed"

restored_nupkg="$NUGET_PACKAGES/${package_id:l}/$package_version/${package_id:l}.$package_version.nupkg"
if [[ ! -f "$restored_nupkg" ]]; then
  fail "restored package is missing at $restored_nupkg"
elif cmp -s "$nupkg" "$restored_nupkg"; then
  pass "restore consumed the selected local package byte-for-byte"
else
  fail "restored package differs from the selected local package"
fi

snapshot_files "$xbd_dir" "$diagnostics/xbd-before.txt"
snapshot_files "$NUGET_PACKAGES" "$diagnostics/nuget-before.txt"
if directory_is_empty "$xbd_dir"; then
  pass "isolated XamarinBuildDownload directory is empty after restore"
else
  fail "isolated XamarinBuildDownload directory was populated during restore"
fi

rm -rf "$NUGET_HTTP_CACHE_PATH"
mkdir -p "$NUGET_HTTP_CACHE_PATH"

echo
echo "Building with HTTP(S) egress blackholed"
if env \
    http_proxy="http://127.0.0.1:9" \
    https_proxy="http://127.0.0.1:9" \
    all_proxy="http://127.0.0.1:9" \
    HTTP_PROXY="http://127.0.0.1:9" \
    HTTPS_PROXY="http://127.0.0.1:9" \
    ALL_PROXY="http://127.0.0.1:9" \
    no_proxy="" \
    NO_PROXY="" \
    dotnet build "$work/app/OfflineApp.csproj" \
      --configuration Debug \
      --no-restore \
      "${msbuild_args[@]}" > "$work/build.log" 2>&1; then
  pass "app built with no network egress available"
else
  fail "app build failed without network egress"
  tail -25 "$work/build.log" >&2
fi

snapshot_files "$xbd_dir" "$diagnostics/xbd-after.txt"
snapshot_files "$NUGET_PACKAGES" "$diagnostics/nuget-after.txt"
diff -u "$diagnostics/xbd-before.txt" "$diagnostics/xbd-after.txt" > "$diagnostics/xbd.diff" || true
diff -u "$diagnostics/nuget-before.txt" "$diagnostics/nuget-after.txt" > "$diagnostics/nuget.diff" || true

if directory_is_empty "$xbd_dir"; then
  pass "isolated XamarinBuildDownload directory remained empty"
else
  fail "isolated XamarinBuildDownload directory was populated during the build"
fi
if cmp -s "$diagnostics/xbd-before.txt" "$diagnostics/xbd-after.txt"; then
  pass "isolated XamarinBuildDownload state did not change"
else
  fail "isolated XamarinBuildDownload state changed during the build"
fi
if cmp -s "$diagnostics/nuget-before.txt" "$diagnostics/nuget-after.txt"; then
  pass "isolated NuGet package cache did not change during the build"
else
  fail "isolated NuGet package cache changed during the build"
fi
if directory_is_empty "$NUGET_HTTP_CACHE_PATH"; then
  pass "isolated NuGet HTTP cache remained empty during the build"
else
  fail "isolated NuGet HTTP cache was populated during the build"
fi

echo
if (( failures > 0 )); then
  echo "$failures offline check(s) failed for $package_id." >&2
  exit 1
fi

completed="true"
echo "Offline build checks passed for $package_id $package_version."
