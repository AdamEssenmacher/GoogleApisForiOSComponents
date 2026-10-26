# Firebase 12.19.3 preparation

This draft prepares the next incremental release after 12.19.0. It depends on that predecessor’s source PR and publication. The current production release candidate is unaffected. Follow [the existing release workflow](FIREBASE_RELEASES.md); source merge and deployment approval remain maintainer actions.

## Native review

The complete [12.19.0 to 12.19.3 source comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.19.0...12.19.3) was checked from downloaded tag archives, avoiding the GitHub compare API’s 300-file cutoff. It changes 12 files overall. Supported-product public-header changes:

No supported-product public Objective-C source header changes.

This is the latest Firebase 12.x version listed by CocoaPods trunk at preparation time. Upstream changes only the Firebase umbrella, Core, Firestore and FirestoreInternal pod versions; all other native products, including Analytics and measurement, remain 12.19.0. The patch removes compromised certificate-authority roots from Firestore’s embedded trust store. Managed wrapper versions advance together, but native version selection must follow this mixed release rather than inventing nonexistent 12.19.3 archives. Add regression coverage for that explicit mapping.


Preserve package IDs, .NET 10 targets, deployment targets, native framework ownership and the selected conversion-framework omission except for requirements explicitly identified above. Review support-package API/header drift whenever its native pin changes. No new audit suppression is justified by this preparation.

## Validation status and merge gates

Local shared audit/updater tests (95) and release-tooling tests (26) passed during stack preparation. These do not establish each version’s build or runtime compatibility. Complete unrestricted and repository-pinned CocoaPods resolution remains required for this version. The draft remains blocked on the full 16-target native audit against fresh frameworks, triage/implementation of the public drift described above, dependency-first build/pack, staged package inspection, six consumer lanes including the two separate Firestore emulator cases, Mac Catalyst and unsigned device Release/LLVM-AOT checks, and passing hosted CI. No skipped generator or inherited predecessor result counts as a pass.

Retarget each stacked PR to main only after its predecessor merges; publish releases in order so planning uses the correct published manifest. The exact merged commit must be rebuilt and validated before deployment approval. Do not reuse preparation artifacts for publication.
