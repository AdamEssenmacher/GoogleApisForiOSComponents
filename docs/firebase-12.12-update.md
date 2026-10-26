# Firebase 12.12.0 update

This checkout prepares the 16 active Firebase packages and GoogleAppMeasurement at `12.12.0`. Publication remains pending. This is the first release configured for the [CI validation, deployment approval, and trusted NuGet publishing workflow](FIREBASE_RELEASES.md).

## Native API review

The [upstream 12.11-to-12.12 comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.11.0...12.12.0) contains 70 commits and 323 changed files. The full Git comparison was reviewed because the GitHub comparison API limits its file list to 300 entries. The supported products' public Objective-C header changes are in `Firestore/Source/Public/FirebaseFirestore/FIRPipelineBridge.h`.

- `FIRFunctionExprBridge` replaces `initWithName:Args:` with `initWithName:Args:Options:`. The binding now exports the three-argument initializer with nullable `NSDictionary<NSString, NSObject>` options (the existing binding representation for native expression dictionaries). The existing two-argument managed constructor forwards to the new initializer with null options, preserving managed callers without sending the removed selector.
- Add the five new public bridge types: `VariableBridge`, `SubcollectionSourceStageBridge`, `DefineStageBridge`, `SearchStageBridge`, and `PipelineExprBridge`.
- The `cloudfirestore-pipeline-bridges` runtime regression exercises the compatibility constructor, null and non-null options, and all five new bridge constructors without backend access. The release workflow requires this lane before sealing a candidate.

The [upstream release notes](https://firebase.google.com/support/release-notes/ios#version_12120_-_april_6_2026) also add Swift async sequences for Auth state/token changes and Remote Config updates, and expand the Swift Firestore pipeline surface. The fresh Auth Objective-C compatibility header changes `uid`, `email`, and `phoneNumber` to read-only; the managed binding already exposes getters only. Generated compatibility headers are reviewed separately from the Swift-only APIs. Firebase AI Logic remains outside this package set.

## Dependencies and build settings

Firebase 12.12 requires Xcode 26.2 or later and Swift 6.2.3 or later. The existing release workflow pins macOS 26 and Xcode 26.6; local validation uses Xcode 26.6 and Swift 6.3.3. No shared toolchain pin change is needed.

Both unrestricted and repository-pinned CocoaPods resolutions retain the same 41-pod membership. The pinned graph accepts the maintained support versions. The unrestricted graph can select newer compatible support releases; those selections are not new requirements. Both graphs select FirebaseCore `12.12.1` through the compatible `~> 12.12.0` constraint, while this repository explicitly builds FirebaseCore `12.12.0`.

The complete podspec diff adds no system frameworks, weak frameworks, native libraries, or compiler/linker flags for the supported products. Changes to Foundation Models Swift flags and macOS/watchOS deployment targets apply to AI Logic, which is excluded. Package identities, .NET 10 targets, deployment targets, ad-ID selection, and native framework ownership are preserved, including Database `icucore` and Firestore C++ linkage.

The published `12.12.0` [FirebaseAnalytics podspec](https://github.com/CocoaPods/Specs/blob/master/Specs/e/2/1/FirebaseAnalytics/12.12.0/FirebaseAnalytics.podspec.json) and [GoogleAppMeasurement podspec](https://github.com/CocoaPods/Specs/blob/master/Specs/e/3/b/GoogleAppMeasurement/12.12.0/GoogleAppMeasurement.podspec.json) explicitly reuse the `12.11.0` binary archives. Keep their native download coordinates unchanged:

- FirebaseAnalytics: archive version `12.11.0`, key `6bc1249dd7c18a16`.
- GoogleAppMeasurement: archive version `12.11.0`, key `f529849317f9fdf5`.

Their managed package versions advance to `12.12.0`. Crashlytics `upload-symbols` is pinned to the [12.12.0 tag commit](https://github.com/firebase/firebase-ios-sdk/commit/d47760f97a853808be6a045d278fbd12abf546b6), with assembly identity and cache folder updated.

The existing GoogleAdsOnDeviceConversion omission is preserved. The native measurement default subspec still requires `~> 3.4.0`, but this framework is not shipped in these packages; native conversion measurement is disabled when it is absent.

## Validation and publication

Previous native outputs and NuGets were isolated before building this candidate. Generated local evidence is under `output/firebase-12.12-validation/` and is not committed. Local preparation packages are not publication artifacts: CI must build, validate, inspect, and seal the exact merged main commit before deployment approval.

Local preparation validation passed:

- Release updater/check, 76 audit unit tests, 26 release-tooling tests, and six consumer-harness regression tests.
- Fresh native builds for the complete closure and dependency-ordered managed build/pack of all 23 packages for .NET 10 iOS and Mac Catalyst.
- All 16 native audit targets, including the real Sharpie 26.3.0.11 fallback for Firestore, with zero actionable failures and no new suppressions.
- The workflow package inspector staged the complete 23-package candidate. Detailed inspection of the 17 updated packages verified 34 managed assemblies and 68 native references, including versions, architectures, native resources, linker metadata, and Crashlytics assets.
- Consumer smoke and nullability; all 1,726 binding surfaces exercised, with no waivers or failures.
- All 22 runtime cases have passing results across recorded runs. Hosted all-mode stopped at the known Firestore seed-write permission denial; the two backend-dependent cases passed separately against the local emulator, and remaining cases passed individually. This does not claim a single all-mode pass or validate hosted security rules and production indexes.

Supplementary Mac Catalyst and unsigned iOS device Release consumer build results are reported on [PR #219](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/219). The local GitHub Packages feed-read probe returns HTTP 403 for the CLI login; the PR workflow has passed the unchanged authenticated feed probe using its repository token. CI remains responsible for validating the effective feed-reused package set and the exact merged publication commit.

CI found that four historical support versions have different payloads on NuGet.org and GitHub Packages, including different repository commits (`5d3d66d2` or `6498909d` on NuGet.org versus `edc4a483` on GitHub Packages). The strict feed comparison correctly blocked publication. Resolve these collisions with new binding revisions:

| Support package | Previous version | Prepared version |
| --- | --- | --- |
| PromisesObjC | 2.4.0.5 | 2.4.0.6 |
| GoogleUtilities | 8.1.0.3 | 8.1.0.4 |
| GTMSessionFetcher | 3.5.0.5 | 3.5.0.6 |
| AppCheckCore | 11.2.0 | 11.2.0.1 |

Their native CocoaPods versions and framework ownership remain unchanged. Revised Firebase package dependency minimums select the new binding revisions. AppCheckCore retains its compatible dependency ranges because its native requirements are unchanged; this also preserves the existing Google Sign-In build. Nanopb and GoogleDataTransport have matching payloads across the feeds and retain their versions. The release planner now reports 21 changed versions within the same 23-package closure. The initial fingerprint baseline is unchanged. An early feed preflight reports all existing payload/structure conflicts before native builds; staging and publication still independently enforce feed identity. The local consumer results above predate these support revisions; CI must validate the effective candidate with the revised dependencies.

`eng/firebase-release.json` sets `releaseVersion` to `12.12.0`. After this PR is merged, the main-branch workflow must pass **Firebase validation**, then wait for maintainer approval of the `firebase-release` environment. The publish job uses NuGet trusted publishing, verifies both NuGet.org and GitHub Packages, and only then creates `firebase-v12.12.0` and its GitHub release with package assets, manifest, and checksums. A manual tag or Chrome upload is not part of this release path.
