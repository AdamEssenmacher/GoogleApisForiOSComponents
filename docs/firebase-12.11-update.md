# Firebase 12.11.0 update

This checkout prepares the 16 active Firebase packages and GoogleAppMeasurement at `12.11.0`. It does not establish that these packages have been published.

## Native review

The baseline is `12.10.0`, including the release metadata patterns from PRs #183, #185, and #189. Preserve the native ownership and linker fixes from #138 and #149–#152: FirebaseSessions belongs to Installations, Database needs `icucore`, and Firestore and its C++ dependencies need their existing framework and linker metadata.

The [upstream comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.10.0...12.11.0) contains 23 commits and 124 changed files. It changes no `.h` files and no production Swift sources for the components bound here. The new Firebase AI Logic surface is outside this package set. The [release notes](https://firebase.google.com/support/release-notes/ios#version_12110_-_march_16_2026) describe Analytics optimizations, a Crashlytics initialization fix, and an internal Firestore refactor.

The shipped `12.10.0` and `12.11.0` Analytics and measurement archives were also compared, with versioned archive roots removed from paths:

| Archive | Header/module/interface files compared | Result |
| --- | --- | --- |
| FirebaseAnalytics | 86 | Identical contents; no added or removed files |
| GoogleAppMeasurement, including IdentitySupport | 12 | Identical contents; no added or removed files |

The upstream binary archives' XCFramework platform/architecture sets are unchanged, including iOS device, simulator, and Mac Catalyst. Their framework bundle versions advance to `12.11.0`; the inspected SDK/toolchain and minimum-platform metadata is unchanged. The upstream podspec changes add no system frameworks, weak frameworks, libraries, compiler flags, or linker flags for the supported products.

### Dependencies and delivery

The initial unrestricted resolution selected newer compatible support releases: AppCheckCore `11.3.1`, GTMSessionFetcher `5.3.1`, PromisesObjC/PromisesSwift `2.4.1`, GoogleUtilities `8.1.3`, and GoogleDataTransport `10.1.1`. Unrestricted selections can advance as compatible upstream packages are published. A second resolution succeeds with this repository's existing pins (`11.2.0`, `3.5.0`, `2.4.0`, `8.1.0`, and `10.1.0`, respectively). These newer selections are not required Firebase 12.11 dependency upgrades, so the shipped support packages are unchanged.

The dependency-checker prerequisite [#215](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/215) checks unrestricted upstream pod membership before resolving with repository pins. This preserves added/removed dependency detection while making pin compatibility depend on CocoaPods satisfying the actual shipped constraints. Both lockfiles remain under `output/firebase-release-check/{upstream,pinned}/` for inspection; incompatible pins still fail resolution.

GoogleAppMeasurement's default CocoaPods subspec raises its iOS `GoogleAdsOnDeviceConversion` requirement from `~> 3.3.0` to `~> 3.4.0`. This repository continues to deliver GoogleAppMeasurement and IdentitySupport without the conversion framework. The native binary detects its absence and disables that feature; invoking the managed conversion entry points does not establish that conversion measurement is available. No new package or native framework is added for it in this update.

FirebaseAnalytics uses archive key `6bc1249dd7c18a16`; GoogleAppMeasurement uses `f529849317f9fdf5`. Both archive versions are `12.11.0`. Crashlytics `upload-symbols` is pinned to the [12.11.0 tag commit](https://github.com/firebase/firebase-ios-sdk/commit/52548902007e7beda99fcdca31f62eccc4befe38), rather than a moving branch.

## Validation

Existing Analytics and measurement XCFramework directories must be removed or isolated before rebuilding because their download tasks reuse directories that already exist. This validation started without source-built XCFrameworks and staged the inspected `12.11.0` Analytics and measurement archives.

- `firebase-release-update --firebase-version=12.11.0 --from-version=12.10.0`: passed.
- `firebase-release-check --firebase-version=12.11.0 --from-version=12.10.0`: passed, including both dependency resolutions and native metadata comparison.
- `dotnet test scripts/FirebaseBindingAudit.Tests/FirebaseBindingAudit.Tests.csproj`: 62 passed after the audit prerequisite's regression additions, none failed or skipped.
- An isolated resolution with GoogleUtilities pinned to incompatible `7.13.3` failed as expected against Firebase's `~> 8.1` requirement.
- The full dependency-first Cake build/pack passed for iOS and Mac Catalyst, including native device, simulator, and Mac Catalyst archives.
- Package inspection passed for all 17 updated NuGets: 34 managed assemblies and 68 native references, including versions, dependency closure, platform/architecture slices, privacy manifests, linker metadata, conversion-framework omission, and packaged Crashlytics targets/props.
- Consumer checks restored these generated NuGets from the isolated local feed, with dedicated package caches; they did not use project references to the binding source. The native header audit separately inspected fresh native frameworks and binding declarations.
- Foundation smoke, nullability validation, and all-target binding-surface coverage passed. The surface run exercised all 1,714 surfaces with none waived.
- The consumer built successfully with all 16 Firebase package references for Mac Catalyst (`Debug`, `maccatalyst-arm64`) and unsigned iOS device (`Release`, `ios-arm64`, including LLVM/AOT). Both used `BindingSurfaceCoverageTarget=all` and `EnableCodeSigning=false`. The unchanged reflection-based harness emitted trim-analysis warnings; both builds finished with zero errors. These are build/link checks, not physical-device execution.
- All 21 runtime-drift cases now have passing results across the recorded runs, including Analytics conversion entry points with the conversion framework absent. The initial hosted run passed 19 cases and blocked `cloudfirestore-aggregate-query` and `cloudfirestore-query-filters` on seed-write permission errors (code 7). Both blocked cases subsequently passed in separate invocations against fresh local Firestore emulators, with their existing assertions unchanged. The all-mode runner's original failure is retained in the artifacts; this is a combined result, not a passing all-mode invocation.

### Firestore emulator follow-up

The consumer runner from [#213](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/213) accepts `--firestore-emulator` with either `cloudfirestore-aggregate-query` or `cloudfirestore-query-filters`. It starts and stops an isolated Standard-edition emulator, defaulting to `127.0.0.1:18080`, under the demo project `demo-firebase-bindings`. The app uses synthetic configuration, excludes the hosted plist, calls the bound emulator selector, explicitly disables Firestore TLS, and uses an in-memory cache. A separate connection result verifies the configured endpoint and disabled TLS before the query assertions. Local rules permit only top-level fixture documents. No hosted project permissions or binding declarations changed.

Validation used Firebase CLI `15.28.2`, Firestore emulator `1.22.0`, Java 21, and the local `12.11.0` NuGets on the iOS simulator:

- `cloudfirestore-aggregate-query`: passed; seed write completed successfully and server count aggregation returned `1`.
- `cloudfirestore-query-filters`: passed; successful seed writes and exact server result counts of `2`, `2`, `1`, `1`, and `1` for string inequality, field-path inequality, string exclusion, field-path exclusion, and `Filter` exclusion.
- Both cases passed again after the harness review corrections; the filter rerun used an alternate config-selected port to verify endpoint propagation. The emulator app inspection confirmed the hosted plist was absent.
- The ordinary Foundation smoke lane passed again without the emulator flag and reported Firebase `12.11.0`; the normal hosted configuration remains usable.
- Unsupported emulator lanes and a non-local endpoint were rejected before build/launch. Shell syntax and `git diff --check` passed.

The new results and simulator/emulator logs are saved as `output/firebase-12.11-validation/runtime-emulator-cloudfirestore-{aggregate-query,query-filters}*`. `runtime-case-summary.json` incorporates these successful reruns and identifies their emulator backend; `runtime-case-summary-before-emulator.json` preserves the initial summary. These tests exercise native bindings and successful query behavior, but do not validate hosted security rules or production index requirements.

### Audit corrections and results

The full audit passes all 16 targets with zero actionable failures and no invalid suppressions. The audit-correctness prerequisite [#214](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/214) ensures Firestore's empty primary surface requires a real Sharpie fallback comparison, stages its `FirebaseFirestoreInternal` companion, and fails when no usable fallback exists. The full audit passed again with these fixes, and the negative audit with Sharpie disabled correctly exits unsuccessfully.

Two narrow, header-backed suppressions cover Sharpie's `NSObject<Protocol, NSObject>` spellings for the existing managed cache-settings protocol interfaces. The cache-settings runtime case passes for these interfaces. No managed binding declarations changed. The successful fallback leaves 73 historical Firestore wrapper-omission suppressions unused; they are reported as stale informational rules, rather than removed as part of this version update.

The release-branch audit rerun is written to `output/firebase-12.11-pr-audit/report.md`; the earlier aggregate report remains at `output/firebase-binding-audit/report.md`. Package inspection and separate consumer results are under `output/firebase-12.11-validation/`. These generated artifacts are not committed.

## Publication gate

The validated candidate NuGets were packed while the version changes were uncommitted and identify the earlier base commit in repository metadata. They are preparation artifacts. After the release PR is merged, rebuild and validate the publication set from that exact merge commit, verify the embedded commit and package hashes, and publish only the explicit 17-package manifest. Tagging, the GitHub release, and nuget.org publication follow that final validation; this preparation does not claim publication.
