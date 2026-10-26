# Firebase 12.17.0 preparation

This draft prepares the next incremental release after 12.16.0. It depends on that predecessor’s source PR and publication. The current production release candidate is unaffected. Follow [the existing release workflow](FIREBASE_RELEASES.md); source merge and deployment approval remain maintainer actions.

## Native review

The complete [12.16.0 to 12.17.0 source comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.16.0...12.17.0) was checked from downloaded tag archives, avoiding the GitHub compare API’s 300-file cutoff. It changes 244 files overall. Supported-product public-header changes:

- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRRecaptchaProvider.h`
- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRRecaptchaProviderFactory.h`
- `FirebaseCore/Sources/Public/FirebaseCore/FIROptions.h`
- `FirebaseInAppMessaging/Sources/Public/FirebaseInAppMessaging/FIRInAppMessaging.h`

App Check reCAPTCHA changes to explicit site-key initializers and removes Core `recaptchaSiteKey`; review these together, preserving safe managed compatibility only where native behavior supports it. In-App Messaging availability annotation changes do not lower this repository’s deployment targets. Production Swift behavior also changes; do not infer API parity solely from Objective-C source headers.

Analytics and measurement archives were downloaded from their CocoaPods source URLs and SHA-256 hashed. All shipped headers, module maps, and Swift interfaces were compared with the preceding archives; these comparisons are preliminary evidence and do not replace the complete generated native audit.

| Archive | Key | SHA-256 |
| --- | --- | --- |
| FirebaseAnalytics | `5b4ffbefbb018b03` | `5b4ffbefbb018b034c6351126f53381aa63d340bafaf7f47a8e9bc096fb1fcbb` |
| GoogleAppMeasurement | `5e80294989c75a36` | `5e80294989c75a365daaa9f5d0573bc13f9584e3523ce4fced57aad848f5fb3d` |

Preserve package IDs, .NET 10 targets, deployment targets, native framework ownership and the selected conversion-framework omission except for requirements explicitly identified above. Review support-package API/header drift whenever its native pin changes. No new audit suppression is justified by this preparation.

## Validation status and merge gates

Local shared audit/updater tests (84) and release-tooling tests (26) passed during stack preparation. These do not establish each version’s build or runtime compatibility. Complete unrestricted and repository-pinned CocoaPods resolution remains required for this version. The draft remains blocked on the full 16-target native audit against fresh frameworks, triage/implementation of the public drift described above, dependency-first build/pack, staged package inspection, six consumer lanes including the two separate Firestore emulator cases, Mac Catalyst and unsigned device Release/LLVM-AOT checks, and passing hosted CI. No skipped generator or inherited predecessor result counts as a pass.

Retarget each stacked PR to main only after its predecessor merges; publish releases in order so planning uses the correct published manifest. The exact merged commit must be rebuilt and validated before deployment approval. Do not reuse preparation artifacts for publication.
