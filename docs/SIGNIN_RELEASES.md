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

Wait for each preceding release to complete before delivering the next version PR. Refresh shared
dependency versions from main, preserve upstream-compatible bounds, and rerun validation if Firebase
12.x advances in between. The publisher rechecks the preceding Sign-In release after deployment
approval. A new binding revision of the current native version is permitted without skipping the
native release sequence. Tags use `google-signin-v<releaseVersion>`; Firebase tags remain separate.

Each release updates native pins, managed/package/file versions, binding APIs, notices, fixtures,
and `releaseVersion` together. Include the applicable claims APIs, refresh-token expiration APIs,
and wrapper identifier. Preserve existing managed signatures, package IDs, namespaces and TFMs.
There is no new SwiftUI binding product. Do not publish an existing version with different bytes.

## Native delivery and publication scope

### 9.1.0 binding update

The `9.1.0.0` package binds Google Sign-In 9.1.0 with AppAuth 2.0.0 and GTMAppAuth 5.0.0.
The Firebase 12.15 preparation advances the shared AppCheckCore pin to 11.3.2,
which also owns PromisesSwift 2.4.0 and RecaptchaInterop 101.0.0. GTMSessionFetcher
3.5.0, PromisesObjC 2.4.0 and GoogleUtilities 8.1.0 retain their existing owners.
The exact Sign-In closure includes all nine native pods; shared frameworks have
one owner across standalone Sign-In and combined Firebase consumers.

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

## Maintainer configuration

Before the first Sign-In deployment, add `AdamE.Google.iOS.SignIn` to the NuGet Trusted Publishing
scope for the existing repository, `firebase-release.yml` workflow and `firebase-release`
environment. Its four support package IDs are already in the Firebase dependency closure; verify
that the current publishing scope includes them. Retain the environment's maintainer approval,
main-only restriction, `NUGET_USER`, and no-administrator-bypass policy. No long-lived API key is
needed. Add **Google Sign-In validation** to main's required checks alongside **Firebase validation**.
