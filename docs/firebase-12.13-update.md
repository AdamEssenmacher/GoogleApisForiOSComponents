# Firebase 12.13.0 update

This checkout prepares the 16 active Firebase packages and GoogleAppMeasurement at `12.13.0`. The published baseline is [Firebase 12.12.0](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/releases/tag/firebase-v12.12.0). Publication remains pending and follows the [approved CI publishing workflow](FIREBASE_RELEASES.md).

## Native API review

The complete [12.12-to-12.13 upstream comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.12.0...12.13.0) contains 60 commits and 190 changed files. No supported product's public Objective-C source header changed. Header changes concern internal Crashlytics error logging, Performance instrumentation/screen tracking, Remote Config update delivery, and tests.

The supported-product Swift changes add Firestore array filter/transform/slice helpers and adjust Swift pipeline search parameters. These do not add Objective-C bridge declarations. Storage fixes instance caching for named apps sharing a bucket. Firebase AI Logic remains outside the supported package set. See the [upstream release notes](https://firebase.google.com/support/release-notes/ios#version_12130_-_may_5_2026).

Direct comparison of the downloaded binary archives found identical GoogleAppMeasurement and IdentitySupport headers and module maps. Analytics retains the same API declarations; its generated compatibility-header preamble and Swift interface compiler flags change from Swift 6.0.3 to 6.2.3. All three frameworks retain their device, simulator, Mac Catalyst, macOS, and tvOS slice sets. Existing Xcode 26.6 satisfies the upstream toolchain requirement; no shared toolchain pin change is needed.

Expected managed API change: package and assembly versions only. Fresh generated Objective-C compatibility headers and the full native binding audit must confirm this before handoff.

## Dependencies and build settings

The complete podspec diff changes Firebase versions, binary download coordinates, and the omitted conversion dependency. It adds no supported-product deployment target, public-header, vendored-framework ownership, system/weak-framework, library, Swift/C++ setting, or linker-flag changes.

The authoritative CocoaPods podspecs select new binary archives:

- [FirebaseAnalytics](https://github.com/CocoaPods/Specs/blob/master/Specs/e/2/1/FirebaseAnalytics/12.13.0/FirebaseAnalytics.podspec.json): version `12.13.0`, key `baa3df53e2800418`.
- [GoogleAppMeasurement](https://github.com/CocoaPods/Specs/blob/master/Specs/e/3/b/GoogleAppMeasurement/12.13.0/GoogleAppMeasurement.podspec.json): version `12.13.0`, key `1e01f67cfc95cf6a`.
- Crashlytics `upload-symbols`: upstream tag commit `d10045cace0b4c335c4efa8f7df7e9a9fc5a7c60`, with assembly identity and download-cache version updated.

Both unrestricted and repository-pinned resolutions retain the same 41-pod membership. The maintained native pins resolve successfully. The unrestricted resolver selects newer compatible AppCheckCore (11.3.2), GTMSessionFetcher (5.3.1), GoogleUtilities (8.1.5), GoogleDataTransport (10.1.1), and Promises (2.4.1); those selections are not new requirements.

Preserve the six support binding versions published with 12.12, including the four revisions that resolved historical cross-feed conflicts. Their CocoaPods pins and AppCheckCore's compatible dependency ranges remain unchanged. Preserve package IDs, .NET 10 targets, native ownership, deployment targets, ad-ID selection, Database `icucore`, and Firestore C++ linkage.

The GoogleAdsOnDeviceConversion omission remains intentional. Upstream's default measurement subspec now requires `~> 3.5.0` instead of `~> 3.4.0`; this framework is not shipped, and the corresponding native conversion feature remains disabled when absent.

## Validation and publication

Validation is in progress. Prior native outputs and generated packages were isolated before the fresh build. Generated evidence is retained under `output/firebase-12.13-validation/` and is not committed.

`eng/firebase-release.json` selects release `12.13.0`; the minimum release floor and immutable initial baseline remain unchanged. The planner must use the published 12.12 release manifest as its previous-release baseline. Only the 17 release artifacts are intended to change version; the six support packages should be reused after feed comparison.

The source PR is merged by the maintainer. The main workflow then builds, validates, and seals packages from the exact merged commit before requesting `firebase-release` deployment approval. Trusted publishing verifies both NuGet.org and GitHub Packages before creating `firebase-v12.13.0` and the GitHub release. Local preparation artifacts are not publication artifacts.
