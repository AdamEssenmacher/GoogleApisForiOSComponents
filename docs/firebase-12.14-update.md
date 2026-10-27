# Firebase 12.14.0 preparation

This draft prepares the next incremental release after 12.13.0. It depends on that predecessor’s source PR and publication. The current production release candidate is unaffected. Follow [the existing release workflow](FIREBASE_RELEASES.md); source merge and deployment approval remain maintainer actions.

## Native review

The complete [12.13.0 to 12.14.0 source comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.13.0...12.14.0) was checked from downloaded tag archives, avoiding the GitHub compare API’s 300-file cutoff. It changes 322 files overall. Supported-product public-header changes:

- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRAppAttestProviderFactory.h`
- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FirebaseAppCheck.h`
- `Firestore/Source/Public/FirebaseFirestore/FIRPipelineBridge.h`

Fix the removed Firestore selectors `initWithRef:firestore:` and `initWithCollectionId:` using their nullable `forceIndex:` replacements. Preserve both existing managed overloads with forwarding constructors and extend the pipeline runtime regression to exercise null/non-null index arguments. Add AppAttestProviderFactory from its new public header. Default measurement conversion requirement advances to `~> 3.6.0`; the existing framework omission remains intentional.

Analytics and measurement archives were downloaded from their CocoaPods source URLs and SHA-256 hashed. All shipped headers, module maps, and Swift interfaces were compared with the preceding archives; these comparisons are preliminary evidence and do not replace the complete generated native audit.

| Archive | Key | SHA-256 |
| --- | --- | --- |
| FirebaseAnalytics | `8219fca148758cd8` | `8219fca148758cd8b2e7d0576e99c51d8520046ca44003e0a534e0d4d18f5b74` |
| GoogleAppMeasurement | `ca71c0624b0be6d4` | `ca71c0624b0be6d441b0eb9aa8c3ba15bc3243aed8b694ddbf4828d652d1d646` |

Preserve package IDs, .NET 10 targets, deployment targets, native framework ownership and the selected conversion-framework omission except for requirements explicitly identified above. Review support-package API/header drift whenever its native pin changes. No new audit suppression is justified by this preparation.

## Validation status and merge gates

Local shared audit/updater tests (84) and release-tooling tests (26) passed during stack preparation. These do not establish each version’s build or runtime compatibility. The 12.14 updater resolved both unrestricted and repository-pinned 41-pod graphs; review its completed output before marking this draft ready. The draft remains blocked on the full 16-target native audit against fresh frameworks, triage/implementation of the public drift described above, dependency-first build/pack, staged package inspection, six consumer lanes including the two separate Firestore emulator cases, Mac Catalyst and unsigned device Release/LLVM-AOT checks, and passing hosted CI. No skipped generator or inherited predecessor result counts as a pass.

Retarget each stacked PR to main only after its predecessor merges; publish releases in order so planning uses the correct published manifest. The exact merged commit must be rebuilt and validated before deployment approval. Do not reuse preparation artifacts for publication.
