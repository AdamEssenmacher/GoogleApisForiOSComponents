# Firebase 12.19.0 preparation

This draft prepares the next incremental release after 12.18.0. It depends on that predecessor’s source PR and publication. The current production release candidate is unaffected. Follow [the existing release workflow](FIREBASE_RELEASES.md); source merge and deployment approval remain maintainer actions.

## Native review

The complete [12.18.0 to 12.19.0 source comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.18.0...12.19.0) was checked from downloaded tag archives, avoiding the GitHub compare API’s 300-file cutoff. It changes 340 files overall. Supported-product public-header changes:

- `Firestore/Source/Public/FirebaseFirestore/FIRFieldValue.h`

Firestore adds double/integer minimum and maximum field transforms. Performance raises GoogleUtilities to `>= 8.1.3, < 9.0`; advance the maintained native pin and support package to compatible 8.1.5. Default measurement conversion requirement advances to `~> 3.7.0`, with the existing omission retained. Analytics real header changes are documentation only; the archives also contain AppleDouble metadata files, which are not API declarations.

Analytics and measurement archives were downloaded from their CocoaPods source URLs and SHA-256 hashed. All shipped headers, module maps, and Swift interfaces were compared with the preceding archives; these comparisons are preliminary evidence and do not replace the complete generated native audit.

| Archive | Key | SHA-256 |
| --- | --- | --- |
| FirebaseAnalytics | `f02e7923f9b32c38` | `f02e7923f9b32c381d309badd6b9366c09d1eb50bd4e9650a91ffa1eea53fc4e` |
| GoogleAppMeasurement | `aa72db15c887f5f9` | `aa72db15c887f5f9f2c6169885a7d205fa49ee1c7099ee800e6dbf9dc47cbaee` |

Preserve package IDs, .NET 10 targets, deployment targets, native framework ownership and the selected conversion-framework omission except for requirements explicitly identified above. Review support-package API/header drift whenever its native pin changes. No new audit suppression is justified by this preparation.

## Validation status and merge gates

Local shared audit/updater tests (84) and release-tooling tests (26) passed during stack preparation. These do not establish each version’s build or runtime compatibility. Complete unrestricted and repository-pinned CocoaPods resolution remains required for this version. The draft remains blocked on the full 16-target native audit against fresh frameworks, triage/implementation of the public drift described above, dependency-first build/pack, staged package inspection, six consumer lanes including the two separate Firestore emulator cases, Mac Catalyst and unsigned device Release/LLVM-AOT checks, and passing hosted CI. No skipped generator or inherited predecessor result counts as a pass.

Retarget each stacked PR to main only after its predecessor merges; publish releases in order so planning uses the correct published manifest. The exact merged commit must be rebuilt and validated before deployment approval. Do not reuse preparation artifacts for publication.
