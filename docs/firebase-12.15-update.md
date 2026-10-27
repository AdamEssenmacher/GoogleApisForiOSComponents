# Firebase 12.15.0 preparation

This draft prepares the next incremental release after 12.14.0. It depends on that predecessor’s source PR and publication. The current production release candidate is unaffected. Follow [the existing release workflow](FIREBASE_RELEASES.md); source merge and deployment approval remain maintainer actions.

## Native review

The complete [12.14.0 to 12.15.0 source comparison](https://github.com/firebase/firebase-ios-sdk/compare/12.14.0...12.15.0) was checked from downloaded tag archives, avoiding the GitHub compare API’s 300-file cutoff. It changes 163 files overall. Supported-product public-header changes:

- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRAppCheckDebugProvider.h`
- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRRecaptchaProvider.h`
- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FIRRecaptchaProviderFactory.h`
- `FirebaseAppCheck/Sources/Public/FirebaseAppCheck/FirebaseAppCheck.h`
- `FirebaseCore/Sources/Public/FirebaseCore/FIROptions.h`

AppCheckCore now requires `~> 11.3`; advance the native pin and support package to 11.3.2. New public headers introduce iOS-only reCAPTCHA providers/factories, and Core adds `recaptchaSiteKey`. Add availability-aware bindings for these declarations; generated-header audit and runtime verification remain required before the draft is ready. Measurement adds generated umbrella version-symbol declarations; Analytics API declarations are unchanged.

Analytics and measurement archives were downloaded from their CocoaPods source URLs and SHA-256 hashed. All shipped headers, module maps, and Swift interfaces were compared with the preceding archives; these comparisons are preliminary evidence and do not replace the complete generated native audit.

| Archive | Key | SHA-256 |
| --- | --- | --- |
| FirebaseAnalytics | `4157b0f8016e6ec3` | `4157b0f8016e6ec3e3bfb5cd0a1de45b07afad69df64c8913f8553a3602b489f` |
| GoogleAppMeasurement | `59fe326eebd1a79c` | `59fe326eebd1a79c8a22ed36f28c71a97060fccbf69d6b8a9de5ded571479af9` |

Preserve package IDs, .NET 10 targets, deployment targets, native framework ownership and the selected conversion-framework omission except for requirements explicitly identified above. Review support-package API/header drift whenever its native pin changes. Any suppression must be supported by observed generator output and native-header evidence.

## Validation status and merge gates

Local shared audit/updater tests (84) and release-tooling tests (26) passed during stack preparation. These do not establish each version’s build or runtime compatibility. Complete unrestricted and repository-pinned CocoaPods resolution remains required for this version. The draft remains blocked on the full 16-target native audit against fresh frameworks, triage/implementation of the public drift described above, dependency-first build/pack, staged package inspection, six consumer lanes including the two separate Firestore emulator cases, Mac Catalyst and unsigned device Release/LLVM-AOT checks, and passing hosted CI. No skipped generator or inherited predecessor result counts as a pass.

Retarget each stacked PR to main only after its predecessor merges; publish releases in order so planning uses the correct published manifest. The exact merged commit must be rebuilt and validated before deployment approval. Do not reuse preparation artifacts for publication.

## Shared support framework ownership

AppCheckCore 11.3.2 adds PromisesSwift and RecaptchaInterop. The AppCheckCore
artifact now explicitly builds and owns `Promises.xcframework` and
`RecaptchaInterop.xcframework`, pinned at 2.4.0 and 101.0.0. Firebase.Core depends
on AppCheckCore 11.3.2 for Promises; Firebase.Auth receives RecaptchaInterop
through Core. The former Core/Auth native references are removed, so standalone
Sign-In, AppCheck-without-Auth and combined consumers have exactly one owner.
No new package identity or duplicate native payload is introduced. Consumers must
use the matching Firebase 12.15 package set when upgrading this support package.

`RecaptchaProvider.Create(app)` wraps the existing native nullable
`RecaptchaProviderFactory.CreateProviderWithApp(app)` path. The generated
constructor is internal because a C# constructor cannot return null. A missing or empty site key
raises an upstream configuration exception; incomplete exchange options can
return nil. The factory preserves both behaviors without inventing a native
`providerWithApp:` selector.

### Review correction checks

The standalone AppCheckCore build began with no externals directory and completed
native build/pack. The Sign-In, Firebase AppCheck and Firebase Auth dependency
closure then built and packed successfully. Packed framework ownership was
checked on both iOS and Mac Catalyst, and the five Sign-In packages passed native
slice, version, binary and resource inspection. The fresh Sign-In lock matches
all nine explicit native pins.

Three isolated consumer graphs (AppCheckCore alone, AppCheck without Auth, and
Sign-In plus AppCheck/Auth) each built for iOS simulator and Mac Catalyst. All
three iOS simulator runtime probes passed on a disposable simulator. The latter
two exercised nullable reCAPTCHA creation with ProjectId absent but an API key
present for Auth's eager initialization. No real credentials or service calls
are needed by these probes.

The fresh AppCheck audit passed with two selector-specific inherited-protocol
suppressions, zero failures, and no stale or invalid rules. No constructor
suppression was added. Surface-coverage generation, 84 audit/updater tests,
49 release-tooling tests, and strict unrestricted/pinned release checks passed.
These focused checks do not replace full per-version validation or current-head
hosted CI; the release gates above remain in force.
