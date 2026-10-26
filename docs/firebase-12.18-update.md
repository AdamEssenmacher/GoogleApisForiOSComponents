# Firebase 12.18.0 preparation

This draft prepares the next incremental release after 12.17.0. It depends on that predecessor’s source PR and publication. The current production release candidate is unaffected. Follow [the existing release workflow](FIREBASE_RELEASES.md); source merge and deployment approval remain maintainer actions.

## Native review

The complete [12.17.0 to 12.18.0 source comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.17.0...12.18.0) was checked from downloaded tag archives, avoiding the GitHub compare API’s 300-file cutoff. It changes 225 files overall. Supported-product public-header changes:

- `FirebaseMessaging/Sources/Public/FirebaseMessaging/FIRMessaging.h`
- `Firestore/Source/Public/FirebaseFirestore/FIRVectorValue.h`

Messaging deprecates token-based APIs in favor of FID registration. Preserve compatible managed methods and review deprecation annotations after the generated-header audit. Firestore vector header change is a documentation typo. No production podspec dependency/system-framework change beyond Firebase versions was found.

Analytics and measurement archives were downloaded from their CocoaPods source URLs and SHA-256 hashed. All shipped headers, module maps, and Swift interfaces were compared with the preceding archives; these comparisons are preliminary evidence and do not replace the complete generated native audit.

| Archive | Key | SHA-256 |
| --- | --- | --- |
| FirebaseAnalytics | `66446d79d168c90e` | `66446d79d168c90e11668dd656279144ec07219a582ae47b90b9d9adfc8ae216` |
| GoogleAppMeasurement | `33b40644e79959e4` | `33b40644e79959e40502fe31a619b487229cec30f7e9552c756dcb9c55866409` |

Preserve package IDs, .NET 10 targets, deployment targets, native framework ownership and the selected conversion-framework omission except for requirements explicitly identified above. Review support-package API/header drift whenever its native pin changes. No new audit suppression is justified by this preparation.

## Validation status and merge gates

Local shared audit/updater tests (84) and release-tooling tests (26) passed during stack preparation. These do not establish each version’s build or runtime compatibility. Complete unrestricted and repository-pinned CocoaPods resolution remains required for this version. The draft remains blocked on the full 16-target native audit against fresh frameworks, triage/implementation of the public drift described above, dependency-first build/pack, staged package inspection, six consumer lanes including the two separate Firestore emulator cases, Mac Catalyst and unsigned device Release/LLVM-AOT checks, and passing hosted CI. No skipped generator or inherited predecessor result counts as a pass.

Retarget each stacked PR to main only after its predecessor merges; publish releases in order so planning uses the correct published manifest. The exact merged commit must be rebuilt and validated before deployment approval. Do not reuse preparation artifacts for publication.
