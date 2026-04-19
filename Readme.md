# Overview

This repository is a community-supported continuation of the Xamarin Google/Firebase iOS binding libraries. Microsoft stopped updating the original `Xamarin.Firebase.iOS.*` and `Xamarin.Google.iOS.*` packages, so I maintain and publish compatible packages under these prefixes:

- `AdamE.Firebase.iOS.*`
- `AdamE.Google.iOS.*`
- `AdamE.MLKit.iOS.*`

The goal is to give .NET for iOS and Mac Catalyst developers a common binding baseline for Google, Firebase, and ML Kit native SDKs. That helps downstream libraries share the same native dependencies instead of each shipping incompatible copies of overlapping xcframeworks.

This repo contains the binding projects, Cake build automation, and documentation used to build and validate those packages.

## Firebase 12.6+ modernization notes

Starting with the Firebase `12.6.0` release, this project has been undergoing a Codex-assisted modernization wave. The goals are to make these binding packages easier to maintain, automate more of the native SDK upgrade process, detect and fix API drift across native version bumps, expand binding-focused E2E validation, improve documentation, and publish active Firebase products that were missing from the current package set.

That created a lot of repository churn around the `12.6.0` line. Much of that churn was tooling, validation, package metadata, documentation, and removal of stale repository content rather than changes to user-authored binding definitions. When binding definitions did change, it was usually for one of these reasons:

- an existing binding was proven to be broken at runtime, so current consumers could not reliably use that surface;
- a native API was missing from the managed binding, so the change was additive;
- missing enum values or native support enums were added;
- nullability was corrected to match the native Firebase headers;
- return or parameter types were corrected to match native declarations, such as native integer widths or protocol-typed return values.

Nullability updates are generally compile-time metadata changes, but they can surface new warnings in consumer code. Type corrections are more visible and can require source changes when the previous managed signature did not match the native SDK. In those cases, the pre-`12.6.0` binding was either stale, too broad, missing, or already capable of dispatching an invalid selector or marshaling the wrong native object.

<details>
<summary>Notable Modernization PRs:</summary>

| PR | Summary |
| --- | --- |
| [#111](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/111) | Aligned nullability for multiple Firebase bindings and introduced Firebase Foundation E2E validation. |
| [#112](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/112) | Fixed Firestore `GetQueryNamed` type drift and added reusable targeted runtime-drift E2E mode and backlog tracking. |
| [#113](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/113) | Replaced stale CloudFunctions emulator selector usage with the current native emulator API and added a regression case. |
| [#114](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/114) | Typed ABTesting `ActivateExperiment` to `ABTExperimentPayload`, replacing a runtime-broken `NSObject` parameter. |
| [#115](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/115) | Typed ABTesting `ValidateRunningExperiments` payload arrays and added failure-proof/regression coverage. |
| [#116](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/116) | Typed ABTesting update overflow policy as the native enum and hardened targeted E2E restore behavior. |
| [#117](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/117) | Added Database `ServerValue.Increment` and targeted E2E coverage. |
| [#118](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/118) | Added Analytics `SessionIdWithCompletion` and targeted E2E coverage. |
| [#119](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/119) | Corrected the Crashlytics `StackFrame.Create` selector and added runtime-drift coverage. |
| [#120](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/120) | Added Analytics on-device conversion APIs and selector E2E coverage. |
| [#121](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/121) | Added RemoteConfig realtime update and custom signal APIs, related enums/domains, and E2E coverage. |
| [#122](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/122) | Added Firestore vector value binding and targeted E2E coverage. |
| [#124](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/124) | Added Firestore aggregate/count query surface and targeted E2E coverage. |
| [#125](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/125) | Added Firestore filter APIs, `notIn`/`isNotEqualTo` overloads, and backend-backed E2E coverage. |
| [#126](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/126) | Added Firestore snapshot listen options, listen source enum, listener overloads, and E2E coverage. |
| [#127](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/127) | Added Firestore named-database factories and E2E coverage. |
| [#128](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/128) | Added Firestore cache settings and persistent cache index manager surface with E2E coverage. |
| [#129](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/129) | Added AppCheck limited-use token APIs and targeted E2E coverage. |
| [#130](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/130) | Refactored E2E drift helpers and centralized Objective-C exception/reflection checks. |
| [#131](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/131) | Added Crashlytics `RecordError` user-info overload and selector coverage. |
| [#132](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/132) | Made AppCheck debug provider conform to the provider protocol, exposing the standard token methods. |
| [#133](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/133) | Added Firestore transaction options and matching `RunTransaction` overloads. |
| [#134](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/134) | Added Firestore index configuration APIs and E2E coverage. |
| [#135](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/135) | Added Realtime Database query-level `GetData` and E2E coverage. |
| [#136](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/136) | Added Core `Configuration.LoggerLevel` getter and E2E coverage. |
| [#137](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/137) | Removed stale GoogleAppMeasurement podspec metadata from Firebase Core. |
| [#138](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/138) | Moved FirebaseSessions packaging from Core to Installations to match the native dependency graph. |
| [#139](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/139) | Added NuGet READMEs for all 16 Firebase packages and wired package readme metadata. |
| [#140](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/140) | Typed Firestore cache settings through native protocol interfaces and updated E2E checks. |
| [#141](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/141) | Added Firebase binding audit runner/config, suppression support, comparer policy, and docs. |
| [#142](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/142) | Added all-target binding-surface coverage that checks managed members and native runtime metadata. |
| [#143](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/143) | Fixed AppCheck error enum packaging so `Firebase.AppCheck.ErrorCode` ships in the binding assembly. |
| [#144](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/144) | Bound Auth action-code URL as an initializer to match the native designated initializer. |
| [#145](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/145) | Made Auth APNS token readonly to match the native property. |
| [#146](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/146) | Made Storage MD5 hash readonly to match the native property. |
| [#147](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/147) | Removed stale Storage metadata reference property that is not present in Firebase Storage `12.6.0`. |
| [#148](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/148) | Cleaned generated header comments without changing APIs. |
| [#149](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/149) | Added FirebaseDatabase `icucore` linker metadata. |
| [#150](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/150) | Added FirebaseAppCheck DeviceCheck weak-framework metadata. |
| [#151](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/151) | Added `leveldb` C++ linker metadata. |
| [#152](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/152) | Added CloudFirestore UIKit framework and gRPC C++ linker metadata. |
| [#153](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/153) | Restored Firebase AppDistribution `12.6.0` package metadata, bindings, and docs. |
| [#154](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/154) | Fixed binding-surface coverage false positives for shorthand types and helper exports. |
| [#155](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/155) | Restored Firebase InAppMessaging `12.6.0`, including Mac Catalyst assets, bindings, audit, and coverage metadata. |
| [#156](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/156) | Corrected PerformanceMonitoring payload size properties to native `long`. |
| [#157](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/157) | Corrected Installations error enum backing type to signed `long`. |
| [#158](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/158) | Corrected AppCheck provider factory return type to `IAppCheckProvider`. |
| [#159](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/159) | Corrected CloudMessaging error enum backing type to signed `long`. |
| [#160](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/160) | Corrected Database emulator port type to native `nint`. |
| [#161](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/161) | Corrected Storage emulator port type, added upload chunk size, and completed Storage error enum values. |
| [#162](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/162) | Corrected CloudFirestore emulator port type to native `nint`. |
| [#163](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/163) | Added Auth tenant, custom-domain, reCAPTCHA, and revoke-token APIs, and refreshed Auth errors/nullability. |
| [#164](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/164) | Added CloudFunctions callable options/URL overloads and corrected emulator API typing. |
| [#165](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/165) | Made audit staged umbrella imports configurable so CloudFunctions generates a real primary surface. |
| [#166](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/166) | Committed curated Firebase `12.6.0` audit suppressions after a full 16-target audit passed. |
| [#167](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/167) | Fixed Database nullable value helper flow and nullable getter annotations. |
| [#168](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/168) | Aligned Firestore transaction result/error flow with native nullable contracts. |
| [#169](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/169) | Hardened Firebase version string caches with explicit failure paths and `dlopen` cleanup. |
| [#170](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/170) | Added validation for Analytics consent dictionaries and enum values. |
| [#171](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/171) | Hardened Storage task snapshot access while keeping public APIs non-null. |
| [#172](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/172) | Suppressed remaining non-actionable Firebase build warnings after source-level fixes. |
| [#173](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/173) | Fixed Auth and CloudFunctions native version symbol lookups caught by E2E coverage. |
| [#174](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/174) | Updated README and NuGet docs to reflect the published Firebase `12.6.0` package set. |
| [#175](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/175) | Removed the retired deprecated component subtree from the active repository. |
| [#176](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/176) | Removed stale NuGet sample solution/projects. |
| [#177](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/177) | Removed retired product docs not represented by active packages. |
| [#178](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/178) | Removed obsolete Azure/manifest CI files, leaving GitHub Actions as the active CI surface. |
| [#179](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/179) | Updated Cake from `0.38.5` to `6.1.0` and modernized build wrappers/docs. |
| [#180](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/180) | Removed the obsolete Firebase AdMob sample and solution entries. |
| [#181](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/181) | Added deterministic Firebase release check/update Cake tooling and dependency-first packing. |

</details>

## Quick links

- Build locally: [docs/BUILDING.md](docs/BUILDING.md)
- Publish to GitHub Packages: [docs/PUBLISHING_GITHUB_PACKAGES.md](docs/PUBLISHING_GITHUB_PACKAGES.md)
- Contributing guidelines: [CONTRIBUTING.md](CONTRIBUTING.md)
- Repository license: [License.md](License.md)

Local builds use the checked-in Cake tool manifest:

```sh
dotnet tool restore
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
```

## Installation and usage notes

These packages are intended for Apple TFMs such as `net9.0-ios`, `net10.0-ios`, and their Mac Catalyst equivalents.

### `Firebase.Installations` may need an explicit reference

Some package combinations still work more reliably when `AdamE.Firebase.iOS.Installations` is referenced explicitly, even if it looks like it should be brought in transitively.

### Crashlytics is native crash reporting, not full .NET exception reporting

The underlying Crashlytics SDK does not understand managed .NET exceptions the same way it understands native crashes. Treat it as native crash reporting support, not a complete .NET error-reporting solution.

On .NET 8 iOS projects, set this property to avoid missing symbol/export issues:

```xml
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <_ExportSymbolsExplicitly>false</_ExportSymbolsExplicitly>
</PropertyGroup>
```

### Condition package references for Apple targets only

If your project targets multiple platforms, keep these packages inside Apple-only `ItemGroup` conditions. Unconditional references in multi-targeted projects can lead to restore/build hangs.

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <PackageReference Include="AdamE.Firebase.iOS.Core" Version="12.7.0" />
</ItemGroup>
```

Use the equivalent `maccatalyst` condition for Mac Catalyst targets.

### Windows and Visual Studio users should expect long-path limitations

These packages include native xcframework contents, which can create very long file paths once NuGet expands them locally. On Windows and in Visual Studio, that can surface as restore failures, build failures, or archive failures.

If you hit errors such as `Could not find a part of the path...` or `Could not find or use auto-linked framework...`, the usual recovery steps are:

1. Enable Windows long-path support.
2. Close Visual Studio.
3. Clear the NuGet cache and the local Xamarin build download cache.
4. Run `dotnet restore` and `dotnet build` from the CLI first.
5. Reopen Visual Studio.

Archiving can still be more reliable on macOS or from the command line.

## Package status

The lists below reflect what is currently published on [nuget.org](https://www.nuget.org/profiles/adamessenmacher) under the `adamessenmacher` owner profile. That is intentionally different from the repo state in [`components.cake`](components.cake): the repo can contain projects or version bumps that have not been published yet.

Firebase `12.7.0` is the current published Firebase package line.

### Currently published on nuget.org

#### Firebase packages (`AdamE.Firebase.iOS.*`)

| Package | Version |
| --- | --- |
| `ABTesting` | `12.7.0` |
| `Analytics` | `12.7.0` |
| `AppCheck` | `12.7.0` |
| `AppDistribution` | `12.7.0` |
| `Auth` | `12.7.0` |
| `CloudFirestore` | `12.7.0` |
| `CloudFunctions` | `12.7.0` |
| `CloudMessaging` | `12.7.0` |
| `Core` | `12.7.0` |
| `Crashlytics` | `12.7.0` |
| `Database` | `12.7.0` |
| `InAppMessaging` | `12.7.0` |
| `Installations` | `12.7.0` |
| `PerformanceMonitoring` | `12.7.0` |
| `RemoteConfig` | `12.7.0` |
| `Storage` | `12.7.0` |

#### Google packages (`AdamE.Google.iOS.*`)

| Package | Version |
| --- | --- |
| `Maps` | `9.2.0.8` |
| `Places` | `7.4.0.2` |
| `SignIn` | `9.0.0` |

#### Google support packages (`AdamE.Google.iOS.*`)

These packages are usually consumed transitively rather than referenced directly:

| Package | Version |
| --- | --- |
| `AppCheckCore` | `11.2.0` |
| `GoogleAppMeasurement` | `12.7.0` |
| `GoogleDataTransport` | `10.1.0.5` |
| `GoogleUtilities` | `8.1.0.3` |
| `Nanopb` | `3.30910.0.5` |
| `PromisesObjC` | `2.4.0.5` |

#### ML Kit packages (`AdamE.MLKit.iOS.*`)

| Package | Version |
| --- | --- |
| `BarcodeScanning` | `6.0.0.3` |
| `Core` | `12.0.0.3` |

### Published on nuget.org, but older / legacy package lines

These packages are still published on nuget.org, but they are not part of the current .NET 9 / .NET 10 publishing wave:

- `AdamE.Firebase.iOS.DynamicLinks` `11.15.0`
- `AdamE.Google.iOS.GTMSessionFetcher` `4.3.0`

### Present in the repo, but not currently published on nuget.org

These projects exist in this repository, but I could not confirm current nuget.org packages for them under the `AdamE.*` package names:

- `AdamE.Google.iOS.Analytics`, `Cast`, `TagManager`, and `UserMessagingPlatform` are present in `source/`, but are not listed in the current nuget.org package set.
- `AdamE.MLKit.iOS.DigitalInkRecognition`, `FaceDetection`, `ImageLabeling`, and `ObjectDetection` are present in `source/` at `1.5.0`, but are not currently published on nuget.org.
- `AdamE.MLKit.iOS.TextRecognition`, `TextRecognition.Chinese`, `TextRecognition.Devanagari`, `TextRecognition.Japanese`, and `TextRecognition.Korean` are present in `source/` at `1.0.0.3`, but are not currently published on nuget.org.
- `AdamE.MLKit.iOS.TextRecognition.Latin` is present in `source/` at `1.4.0.3`, but is not currently published on nuget.org.
- `AdamE.MLKit.iOS.Vision` is present in `source/` at `3.0.0`, but is not currently published on nuget.org.

## Troubleshooting

### Duplicate native dependency conflicts

Google, Firebase, and ML Kit SDKs share native xcframework dependencies. Avoid mixing these packages with other independent binding libraries that ship their own copies of the same native Google/Firebase frameworks, or you can end up with duplicate symbols, linker failures, or runtime issues.

In practice, it is safest to make sure all Google/Firebase/ML Kit dependencies in an app come from one compatible binding set.

### Windows / Visual Studio restore and archive failures

If you see errors like `Could not find a part of the path...`, `Could not find or use auto-linked framework...`, or inconsistent archive behavior on Windows, start with the long-path recovery steps in the installation notes above. In most cases, the issue is not the package itself; it is the path length limit in the Windows/Visual Studio toolchain.

### Builds hanging in multi-targeted projects

If a project targets Android, Windows, or other non-Apple TFMs, do not place these package references in an unconditional `ItemGroup`. Condition them so they only apply to the iOS or Mac Catalyst targets.

### 7-zip extraction issues

If package extraction appears to be failing on Windows, see [this issue comment](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues/21#issuecomment-2172949175).

## Contributing and support

Pull requests are welcome, especially for stale bindings, dependency updates, docs improvements, and build fixes. For anything non-trivial, please open an issue or discussion first so we can align on the approach before you spend time on build tooling or binding surface changes.

For contributor workflow details, see [CONTRIBUTING.md](CONTRIBUTING.md) and [docs/BUILDING.md](docs/BUILDING.md).

If this project saves you time, or if you want to help prioritize maintenance work, one-time and recurring sponsorship options are available from my GitHub profile.

## License

See [License.md](License.md).
