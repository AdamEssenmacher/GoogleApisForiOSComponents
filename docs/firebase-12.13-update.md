# Firebase 12.13.0 update

This checkout prepares the 16 active Firebase packages and GoogleAppMeasurement at `12.13.0`. The published baseline is [Firebase 12.12.0](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/releases/tag/firebase-v12.12.0). Publication remains pending and follows the [approved CI publishing workflow](FIREBASE_RELEASES.md).

## Native API review

The complete [12.12-to-12.13 upstream comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.12.0...12.13.0) contains 60 commits and 190 changed files. No supported product's public Objective-C source header changed. Header changes concern internal Crashlytics error logging, Performance instrumentation/screen tracking, Remote Config update delivery, and tests.

The supported-product Swift changes add Firestore array filter/transform/slice helpers and adjust Swift pipeline search parameters. These do not add Objective-C bridge declarations. Storage fixes instance caching for named apps sharing a bucket. Firebase AI Logic remains outside the supported package set. See the [upstream release notes](https://firebase.google.com/support/release-notes/ios#version_12130_-_may_5_2026).

Direct comparison of the downloaded binary archives found identical GoogleAppMeasurement and IdentitySupport headers and module maps. Analytics retains the same API declarations; its generated compatibility-header preamble and Swift interface compiler flags change from Swift 6.0.3 to 6.2.3. All three frameworks retain their device, simulator, Mac Catalyst, macOS, and tvOS slice sets. Existing Xcode 26.6 satisfies the upstream toolchain requirement; no shared toolchain pin change is needed.

Fresh generated Objective-C compatibility headers and the complete 16-target native audit confirm no actionable API drift. Managed binding declarations are unchanged; package and assembly versions advance to 12.13.0. No new audit suppressions or API corrections are required.

## Dependencies and build settings

The complete podspec diff changes Firebase versions, binary download coordinates, and the omitted conversion dependency. It adds no supported-product deployment target, public-header, vendored-framework ownership, system/weak-framework, library, Swift/C++ setting, or linker-flag changes.

The authoritative CocoaPods podspecs select new binary archives. Both archives have checked-in SHA-256 pins; the release updater/checker refreshes and verifies the coordinate/hash pair together. Analytics declares an exact managed GoogleAppMeasurement dependency (`[12.13.0]`), maintained by the updater. Newer measurement versions are outside that declared constraint.

Archive coordinates:

- [FirebaseAnalytics](https://github.com/CocoaPods/Specs/blob/master/Specs/e/2/1/FirebaseAnalytics/12.13.0/FirebaseAnalytics.podspec.json): version `12.13.0`, key `baa3df53e2800418`.
- [GoogleAppMeasurement](https://github.com/CocoaPods/Specs/blob/master/Specs/e/3/b/GoogleAppMeasurement/12.13.0/GoogleAppMeasurement.podspec.json): version `12.13.0`, key `1e01f67cfc95cf6a`.
- Crashlytics `upload-symbols`: upstream tag commit `d10045cace0b4c335c4efa8f7df7e9a9fc5a7c60`, with assembly identity and download-cache version updated.

Both unrestricted and repository-pinned resolutions retain the same 41-pod membership. The maintained native pins resolve successfully. The unrestricted resolver selects newer compatible AppCheckCore (11.3.2), GTMSessionFetcher (5.3.1), GoogleUtilities (8.1.5), GoogleDataTransport (10.1.1), and Promises (2.4.1); those selections are not new requirements.

Preserve the six support binding versions published with 12.12, including the four revisions that resolved historical cross-feed conflicts. Their CocoaPods pins and AppCheckCore's compatible dependency ranges remain unchanged. Preserve package IDs, .NET 10 targets, native ownership, deployment targets, ad-ID selection, Database `icucore`, and Firestore C++ linkage.

The GoogleAdsOnDeviceConversion omission remains intentional. Upstream's default measurement subspec now requires `~> 3.5.0` instead of `~> 3.4.0`; this framework is not shipped, and the corresponding native conversion feature remains disabled when absent. Conversion-related undefined binary symbols are unchanged, and the packaged Analytics consumer and conversion entry-point regression pass without this framework.

## Validation and publication

Prior native outputs and generated packages were isolated before the fresh build. Generated evidence is retained under `output/firebase-12.13-validation/` and is not committed.

Local preparation validation passed:

- Release updater/check, 84 audit/fingerprint/metadata tests, 26 release-tooling tests, and six consumer-harness regression tests.
- Fresh dependency-first Cake native build and managed build/pack of all 23 closure packages for .NET 10 iOS and Mac Catalyst.
- All 16 native audit targets, including the real Sharpie 26.3.0.11 Firestore fallback, with zero actionable failures and no new suppressions.
- Workflow candidate inspection for all 23 packages, with the six unchanged support payloads reused from NuGet.org. Detailed inspection verifies 17 updated NuGets, 34 assemblies at version 12.13.0.0 targeting .NET 10, and 72 native references across both target frameworks. Native reference ownership matches the prior packages; native versions, slices, privacy resources, linker metadata, and Crashlytics assets pass inspection.
- All six CI consumer lanes from the staged packages: smoke, all 1,726 binding surfaces (zero waivers/failures), nullability, pipeline constructors, and the two Firestore emulator cases separately.
- All 22 runtime cases have passing results across recorded runs. Hosted all-mode stops on the known Firestore seed-write permission denial; the two backend cases pass separately against the disposable emulator, and the remaining runtime cases pass in all-mode or individually. This does not claim a single all-mode pass or verify hosted backend security rules and production indexes.
- Mac Catalyst consumer build. Supplementary unsigned iOS device Release/LLVM-AOT results are tracked in [PR #220’s validation report](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/220); no physical-device execution is claimed.

Local consumer restore uses an isolated feed and cache. The local CLI cannot read GitHub Packages, so the PR workflow must independently verify both feeds and validate the effective staged candidate using its repository token. Preparation packages identify [commit `6637a1b8`](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/commit/6637a1b8611cee9fb7f8d34a5dfb0ff047b39253); subsequent documentation-only changes do not make these publication artifacts.

`eng/firebase-release.json` selects release `12.13.0`; the minimum release floor and immutable initial baseline remain unchanged. The planner must use the published 12.12 release manifest as its previous-release baseline. Only the 17 release artifacts are intended to change version; the six support packages should be reused after feed comparison.

The source PR is merged by the maintainer. The main workflow then builds, validates, and seals packages from the exact merged commit before requesting `firebase-release` deployment approval. Trusted publishing verifies both NuGet.org and GitHub Packages before creating `firebase-v12.13.0` and the GitHub release. Local preparation artifacts are not publication artifacts.

## Tracked nonblocking follow-ups

- [Extracted-download cache identity (#221)](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues/221): directory existence is still insufficient to prove a cache matches the requested source. This preparation isolated prior outputs; release CI uses a clean workspace. Do not rely on retained extracted outputs for release caching until this is corrected.
- [Crashlytics script content verification (#222)](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues/222): the correct immutable commit is pinned, but opt-in consumer symbol-upload execution still lacks content hashing. This separate consumer-build hardening is not implemented here.
- [Harness version defaults (#223)](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues/223): reduce repeated manifest versions while retaining explicit overrides and mismatch validation. The release updater/checker keeps the current manifests aligned.
