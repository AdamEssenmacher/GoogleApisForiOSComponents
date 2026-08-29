# Google NuGet runtime harness

This consumer-style iOS simulator app validates the runtime delivery behavior of Google binding
NuGet packages produced by this repository. It answers whether the managed binding loads, its native
SDK links and loads, and required resources reach the built app.

The checks are keyless. Native service, backend, or configuration failures may be acceptable, while
binding-layer failures such as `EntryPointNotFoundException`, `DllNotFoundException`,
`TypeLoadException`, or `ObjCException` indicate a delivery problem.

## Current scope and baseline

The sole runtime adapter is `AdamE.Google.iOS.Places` 7.4.0.3.

The behavioral expectations were established against the 7.4.0.2 pre-migration package, which used
`Xamarin.Build.Download` to fetch the Google Places SDK during the consumer build. The harness does
not assert how the native SDK arrives, so the same checks validate the self-contained package.

The Places adapter verifies:

- `Google.Places.AutocompleteFilter` loads from the restored binding assembly.
- `GMSPlaceRectangularLocationOption` resolves from the linked app binary.
- `GMSPlacesClient`, `GMSPlace`, and `GMSAutocompleteFilter` are present in the Objective-C runtime.
- `GooglePlaces.bundle` is present at the app root.
- The bundle contains the baseline's 59 files, including representative data, localized string, and
  image files.

The companion `check-package-structure.sh` and `check-offline-build.sh` scripts inspect the package
and prove the consumer build no longer downloads native content. The harness does not compare stored
baselines, exercise a physical device, or test the Places backend.

## Run it

Pack Places, then launch the runtime checks on an available iPhone simulator:

```sh
dotnet tool restore
dotnet tool run dotnet-cake -- --target=nuget --names=Google.Places
tools/e2e/run-google-foundation.sh --target Places --package-dir output
```

If the package directory contains exactly one `AdamE.Google.iOS.Places.*.nupkg`, the runner derives
and records its version. Zero or multiple matching packages are rejected so a run cannot silently
exercise the wrong artifact.

When the directory contains multiple versions, select one explicitly:

```sh
tools/e2e/run-google-foundation.sh \
  --target Places \
  --package-dir output \
  --package-version 7.4.0.3
```

`NuGet.config` maps `AdamE.*` packages to the repository's local `output/` feed. For another package
directory, the runner generates an equivalent restore configuration under the ignored `artifacts/`
directory.

Set `E2E_SIMULATOR_UDID` to choose a simulator. Otherwise, the runner uses the first available iPhone
simulator. `E2E_TIMEOUT_SECONDS` changes the default 90-second result timeout.

## Results and diagnostics

The app writes a structured result that includes the selected target and requested NuGet version.
The runner copies it to:

```text
tests/E2E/Google.Foundation/artifacts/result.json
```

The same ignored directory contains the simulator log and target-neutral build diagnostics:

- `simulator.log`
- `manifest-iossimulator-arm64/app-files.txt`
- `manifest-iossimulator-arm64/native-symbols.txt`
- `manifest-iossimulator-arm64/linked-libraries.txt`
- `manifest-iossimulator-arm64/llvm-segment-count.txt`

These files aid failure investigation; they are not golden baselines.

Failed structure and offline checks copy their diagnostics to `package-structure-Places/` and
`offline-Places/` beneath the same artifacts directory.

## Adding another Google package

The host app is reusable, while each binding needs an explicit runtime adapter. Add a target by:

1. Adding its version property, conditional compilation symbol, `PackageReference`, and
   `TargetPackageVersion` metadata to `GoogleFoundationE2E.csproj`.
2. Adding a case class with keyless, package-specific checks and routing it from
   `GoogleSelfTestRunner`.
3. Mapping the target to its package ID and version property in `run-google-foundation.sh`.

A useful adapter normally includes a managed type reference so trimming cannot discard the binding,
then proves native delivery with an exported symbol or Objective-C class lookup. Resource checks must
match the SDK's actual form: static frameworks may need an app-root bundle, while dynamic frameworks
can carry bundles inside `App.app/Frameworks`.

## Target framework and Xcode

The harness targets `net10.0-ios`, matching the repository's current packages. `global.json` pins the
workload set used by the repository, and .NET for iOS enforces the compatible Xcode major/minor
version.

For a deliberate local toolchain experiment, `--allow-xcode-mismatch` passes
`ValidateXcodeVersion=false`. CI and trusted runtime validation should use the matching Xcode instead.

## Bundle identifier

The test app uses the generic public identifier
`com.googleapisforioscomponents.tests.googlefoundatione2e`. The runner reads it from `Info.plist` for
simulator install, launch, and result collection, so it is not duplicated in shell code.
