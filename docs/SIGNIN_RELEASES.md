# Google Sign-In incremental releases

The existing `firebase-release.yml` workflow has independent Firebase and Google Sign-In channels.
Both use the protected `firebase-release` deployment environment, NuGet Trusted Publishing, and the
same publication concurrency group. PR validation never publishes. A maintainer merges each PR and
separately approves its deployment; merging alone does not authorize publication.

## Setup and sequence

The automation setup starts with `eng/signin-release.json` set to `releaseVersion: null`. This
validates the existing 9.0 package but cannot publish it, even on a manual main-branch run. The
initial `eng/signin-release-baseline.json` records immutable input fingerprints, not a publication.

After the setup PR is merged, prepare these PRs individually from refreshed main:

| Branch | Package version | Native SDK | AppAuth | GTMAppAuth | Required previous release |
| --- | --- | --- | --- | --- | --- |
| `codex/signin-9.1.0` | `9.1.0.0` | `9.1.0` | `2.0.0` | `5.0.0` | Bootstrap |
| `codex/signin-9.2.0` | `9.2.0.0` | `9.2.0` | `2.1.0` | `5.0.0` | `9.1.0` |
| `codex/signin-10.0.0` | `10.0.0.0` | `10.0.0` | `3.0.0` | `6.0.0` | `9.2.0` |
| `codex/signin-10.1.0` | `10.1.0.0` | `10.1.0` | `3.0.0` | `6.0.0` | `10.0.0` |

Wait for each preceding release to complete before marking the next version PR ready for merge. Refresh shared
dependency versions from main, preserve upstream-compatible bounds, and rerun validation if Firebase
12.x advances in between. The publisher rechecks the preceding Sign-In release after deployment
approval. A new binding revision of the current native version is permitted without skipping the
native release sequence. Tags use `google-signin-v<releaseVersion>`; Firebase tags remain separate.

Draft validation for the next version can proceed while its predecessor's publication is being
repaired, but keep the draft explicitly blocked. In particular, 9.2.0 must not be merged or deployed
until 9.1.0 is verified on both feeds and its `google-signin-v9.1.0` release is complete. Do not
bypass the publisher's predecessor check or use package presence on only one feed as completion.

Each release updates native pins, managed/package/file versions, binding APIs, notices, fixtures,
and `releaseVersion` together. Include the applicable claims APIs, refresh-token expiration APIs,
and wrapper identifier. Preserve existing managed signatures, package IDs, namespaces and TFMs.
There is no new SwiftUI binding product. Do not publish an existing version with different bytes.

## Native delivery and publication scope

### 9.1.0 binding update

The `9.1.0.0` package binds Google Sign-In 9.1.0 with AppAuth 2.0.0 and GTMAppAuth 5.0.0.
It retains the Firebase 12 baseline: AppCheckCore 11.2.0, GTMSessionFetcher 3.5.0,
PromisesObjC 2.4.0 and GoogleUtilities 8.1.0 in their existing owning packages.

`Claim.CreateAuthTimeClaim()` and `Claim.CreateEssentialAuthTimeClaim()` construct the
supported `auth_time` claims. `Claim.Name`, `Claim.Essential`, `Constants.AuthTimeClaimName`,
and the claims-related error codes expose the corresponding native API. The four
`SignIn.SignInWithClaims(...)` overloads accept `NSSet<Claim>` along with the optional hint,
scopes and nonce. Their distinct managed name preserves existing calls that pass a null hint.
The existing sign-in methods remain available, including the nonce overload. Token expiration
and wrapper-identifier changes belong to subsequent native releases.

The secret-free simulator probe constructs both claim variants, reads their properties and
constant, checks native classes/selectors, and compiles existing null-hint call shapes.
Package inspection separately compares every managed error-code value with the enum declarations
in each packed native framework's `GIDSignIn.h`, including EMM and both claims errors. Constructing
an `NSError` with a caller-supplied code would not establish the native enum's declared value.
It does not initiate sign-in or request an ID token. Credentialed claim delivery remains an
application integration check. The tagged upstream Apache license is included in the package
notices; the separate Firebase Analytics/App Measurement licensing review remains open.

### 9.2.0 binding update

The prepared `9.2.0.0` package binds Google Sign-In 9.2.0 with AppAuth 2.1.0 and GTMAppAuth 5.0.0.
Shared packages and their compatible version bounds remain at the Firebase 12 baseline above.

`Claim.CreateAmrClaim()` and `Claim.CreateEssentialAmrClaim()` request the voluntary or essential
`amr` (Authentication Methods References) claim. `Constants.AmrClaimName` exposes its native name;
these objects use the existing `SignInWithClaims(...)` methods. Existing `auth_time` APIs remain.

Refresh-token expiration is available through the already-bound nullable
`GoogleUser.RefreshToken.ExpirationDate`; this release needs no duplicate expiration property.
When an expired refresh token is detected, the native SDK reports `ErrorCode.RefreshTokenExpired`
(-11), which is included in the native-header enum comparison. Reading an actual issued token's
expiration and observing its refresh failure requires credentialed integration testing.

The release also binds Sign-In's iOS-only App Check setup, which earlier packages omitted:
`SignIn.ConfigureWithCompletion(...)`, `SignIn.ConfigureDebugProvider(apiKey, ...)`,
`Constants.AppCheckErrorDomain` and `AppCheckErrorCode`. These are unavailable on Mac Catalyst, matching
the native headers. Package inspection compares `AppCheckErrorCode` with `GIDAppCheckError.h` in each
packed framework slice, as it does for `ErrorCode`.

The secret-free probe constructs both `amr` variants, checks their names/essential flags and native
constant, verifies all eight bound SDK classes and at least 73 selectors, and compiles access to the
existing nullable expiration property. On iOS it also compiles the App Check setup calls and reads the
error-domain constant, without calling setup, so no App Attest keys are generated. Evidence for 9.2 must include both `auth_time` and `amr`;
9.1-only evidence is rejected. No credentialed sign-in or refresh is initiated by these probes.

All four releases use CocoaPods at package-production time and retain packed native frameworks and
resources. The producer's CocoaPods/Xcode pins are recorded in the Sign-In configuration. Exact
resolved native versions and Podfile.lock are retained as build evidence. Neither CocoaPods nor
native SDK downloads are required by consumers.

The candidate contains exactly Sign-In, AppCheckCore, GTMSessionFetcher, PromisesObjC, and
GoogleUtilities. Shared native frameworks keep their existing owning NuGets. Existing matching
packages are reused from the feeds; missing destinations can receive the same verified bytes.
Feed disagreements fail validation. Only sealed, hash-verified candidates can be published; the
publish job never rebuilds packages.

Firebase Auth/Analytics/Firestore packages are built separately as integration-test fixtures and
cannot enter the Sign-In publication candidate. No Firebase 13 packages, archive materialization,
static-link migration, or producer-only SwiftPM recipe is introduced here. That work remains on
its deferred feature branch. A later packaging/dependency transition needs a new binding revision
if Sign-In 10.1 has already been published.

## Validation and evidence

The required **Google Sign-In validation** check includes release-tool tests, native package/slice
inspection, direct/transitive simulator/device/Catalyst consumers, first-build HTTP(S) blocking,
unchanged NuGet and empty XBD caches, resource uniqueness, and a secret-free simulator probe of
configuration, singleton, button, and bound native classes/selectors. The same matrix also runs
with the current Firebase 12 Auth/Analytics/Firestore fixtures.

The candidate manifest includes versions, input fingerprints, hashes, source commit, reused feed
packages, destinations and validation results. Approval summaries identify the release channel.
GitHub Releases are created only after matching payloads are verified on GitHub Packages and
NuGet.org. Interrupted releases use the existing retry/conflict rules described in
[Firebase releases](FIREBASE_RELEASES.md).

```sh
dotnet tool restore
dotnet tool run dotnet-cake -- signin_publish.cake
dotnet tool run dotnet-cake -- --target=firebase-publish-metadata
python3 scripts/firebase-release/release.py --channel signin plan --online
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
python3 scripts/firebase-release/release.py --channel signin prepare
python3 scripts/signin-release/validate.py
python3 scripts/signin-release/consumers.py
```

The local `prepare` command uses local build output. Eligible CI releases use `prepare --online`
to select existing feed payloads where required, then validate those exact effective packages.
Do not regenerate the immutable bootstrap baseline as part of version-update PRs.

Credentialed sign-in/token refresh, real Windows/Pair-to-Mac, and signed-device distribution remain
separate integration checks, not secret-free release gates. These tests do not establish SDK use
or redistribution license clearance, and do not change the Firebase measurement licensing review.

### Binding drift audit

The consumer probe proves that every bound selector exists natively, but it cannot see native API
that was never bound. Each Sign-In version update should also run the binding audit, which compares
`source/Google/SignIn` with bindings freshly generated from the built `GoogleSignIn.xcframework`:

```sh
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
./scripts/compare-firebase-bindings.sh --targets SignIn
```

The audit needs the Sign-In build's `externals/` output, including `GTMSessionFetcher.xcframework`,
because Sign-In headers import it. Review `output/firebase-binding-audit/details/SignIn.md`. Expected
differences are suppressed with header evidence in `scripts/firebase-binding-audit-suppressions.json`:
unavailable `+new`, the externally typed `fetcherAuthorizer`, and the existing managed enum names.
Bind any other `missing-baseline-binding` finding, or add a suppression that explains why it stays
unbound. Like the Firebase targets, the audit is a maintainer check rather than a CI gate.

## Maintainer configuration

Before the first Sign-In deployment, add `AdamE.Google.iOS.SignIn` to the NuGet Trusted Publishing
scope for the existing repository, `firebase-release.yml` workflow and `firebase-release`
environment. Its four support package IDs are already in the Firebase dependency closure; verify
that the current publishing scope includes them. Retain the environment's maintainer approval,
main-only restriction, `NUGET_USER`, and no-administrator-bypass policy. No long-lived API key is
needed. Add **Google Sign-In validation** to main's required checks alongside **Firebase validation**.

## Native validation triggers

Sign-In planning watches its five owned packages and the Firebase Auth, Analytics, and
CloudFirestore integration fixture dependency closure. The planner and combined-feed
stager use the same traversal. Changes to a fixture dependency rerun both affected
release lanes; Crashlytics-only changes do not rerun Sign-In. Sign-In configuration and
consumer tooling activate only Sign-In, while shared build inputs and common release
validation tooling activate both channels. The Firebase metadata and release Cake
entrypoints also activate both channels because Sign-In consumes them for its fixtures.
Closure discovery does not enforce the Firebase 12 version policy: unrelated changes
can still produce a no-op plan with newer fixture metadata, while affected changes run
validation and fixture staging still rejects unsupported major versions. Incomplete
dependency metadata fails planning rather than silently omitting validation inputs.
Every affected Sign-In PR still runs standalone,
transitive, offline, and combined Firebase consumers.

Both plans run on every PR and explain native scheduling through `reasons` in the plan
JSON and log. See [CI lane ownership and scheduling](FIREBASE_RELEASES.md#ci-lane-ownership-and-scheduling)
for cancellation, required checks, and manual component packing.
