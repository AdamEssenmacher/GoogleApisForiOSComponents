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

The published `12.12.0` FirebaseAnalytics and GoogleAppMeasurement podspecs explicitly reuse the `12.11.0` binary archives. Keep their native download coordinates unchanged:

- FirebaseAnalytics: archive version `12.11.0`, key `6bc1249dd7c18a16`.
- GoogleAppMeasurement: archive version `12.11.0`, key `f529849317f9fdf5`.

Their managed package versions advance to `12.12.0`. Crashlytics `upload-symbols` is pinned to the [12.12.0 tag commit](https://github.com/firebase/firebase-ios-sdk/commit/d47760f97a853808be6a045d278fbd12abf546b6), with assembly identity and cache folder updated.

The existing GoogleAdsOnDeviceConversion omission is preserved. The native measurement default subspec still requires `~> 3.4.0`, but this framework is not shipped in these packages; native conversion measurement is disabled when it is absent.

## Validation and publication

Previous native outputs and NuGets were isolated before building this candidate. Generated local evidence is under `output/firebase-12.12-validation/` and is not committed. Local preparation packages are not publication artifacts: CI must build, validate, inspect, and seal the exact merged main commit before deployment approval.

Local preparation checks completed so far: release updater/check, 76 audit unit tests, 24 release-tooling tests, and six consumer-harness regression tests passed. The full native build, native header audit, package inspection, and consumer runs are still in progress. The local GitHub Packages feed-read probe returns HTTP 403 for the CLI login; the PR workflow has passed the unchanged authenticated feed probe using its repository token.

The release planner reports 17 changed package versions within the 23-package Firebase dependency closure. The six unchanged support packages retain their versions. The initial fingerprint baseline is unchanged. The workflow reuses existing verified package payloads and can copy required unchanged support versions to a missing feed.

`eng/firebase-release.json` sets `releaseVersion` to `12.12.0`. After this PR is merged, the main-branch workflow must pass **Firebase validation**, then wait for maintainer approval of the `firebase-release` environment. The publish job uses NuGet trusted publishing, verifies both NuGet.org and GitHub Packages, and only then creates `firebase-v12.12.0` and its GitHub release with package assets, manifest, and checksums. A manual tag or Chrome upload is not part of this release path.
