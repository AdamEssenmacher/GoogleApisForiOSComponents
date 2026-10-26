# Firebase 12.16.0 preparation

This draft prepares the next incremental release after 12.15.0. It depends on that predecessor’s source PR and publication. The current production release candidate is unaffected. Follow [the existing release workflow](FIREBASE_RELEASES.md); source merge and deployment approval remain maintainer actions.

## Native review

The complete [12.15.0 to 12.16.0 source comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.15.0...12.16.0) was checked from downloaded tag archives, avoiding the GitHub compare API’s 300-file cutoff. It changes 233 files overall. Supported-product public-header changes:

- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRAppCheck.h`
- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRAppCheckAvailability.h`
- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRDeviceCheckProviderFactory.h`
- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRRecaptchaProvider.h`
- `FirebaseMessaging/Sources/Public/FirebaseMessaging/FIRMessaging.h`

FirestoreInternal adds the system `Network` framework; include it in NativeReference linker metadata. Messaging introduces FID registration/unregistration APIs, callbacks, a notification, and installation-ID mode state. App Check changes default-provider behavior and simplifies availability annotations. These public surfaces and generated Swift compatibility headers need the complete native audit before readiness. Analytics header changes are consent wording only.

Analytics and measurement archives were downloaded from their CocoaPods source URLs and SHA-256 hashed. All shipped headers, module maps, and Swift interfaces were compared with the preceding archives; these comparisons are preliminary evidence and do not replace the complete generated native audit.

| Archive | Key | SHA-256 |
| --- | --- | --- |
| FirebaseAnalytics | `fde230ef9469d037` | `fde230ef9469d037bbbc0861efa7c715ed193c94fcbc407c7bbdc344f5619a70` |
| GoogleAppMeasurement | `55d011d9e0f71e0a` | `55d011d9e0f71e0a89b100129a3d1ba5b0838ca452250cb4766cfe17fbc81c6c` |

Preserve package IDs, .NET 10 targets, deployment targets, native framework ownership and the selected conversion-framework omission except for requirements explicitly identified above. Review support-package API/header drift whenever its native pin changes. No new audit suppression is justified by this preparation.

## Validation status and merge gates

Local shared audit/updater tests (84) and release-tooling tests (26) passed during stack preparation. These do not establish each version’s build or runtime compatibility. Complete unrestricted and repository-pinned CocoaPods resolution remains required for this version. The draft remains blocked on the full 16-target native audit against fresh frameworks, triage/implementation of the public drift described above, dependency-first build/pack, staged package inspection, six consumer lanes including the two separate Firestore emulator cases, Mac Catalyst and unsigned device Release/LLVM-AOT checks, and passing hosted CI. No skipped generator or inherited predecessor result counts as a pass.

Retarget each stacked PR to main only after its predecessor merges; publish releases in order so planning uses the correct published manifest. The exact merged commit must be rebuilt and validated before deployment approval. Do not reuse preparation artifacts for publication.
