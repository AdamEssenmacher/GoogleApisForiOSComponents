# Isolated non-Firebase package production

The Firebase 13 experiment and the existing non-Firebase products require different
shared native dependencies. The compatibility producer keeps the pre-migration
source baseline pinned by commit and tree in `eng/legacy-producer.json`. Sign-In
stays at native 10.1.0 and package 10.1.0.0, with its existing dependency bounds,
managed APIs, target frameworks, native ownership and packed-framework delivery.
Maps/Places and deferred products retain their original delivery behavior too.

## Commands and outputs

Existing commands continue to select the appropriate producer:

```sh
dotnet tool restore
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
dotnet tool run dotnet-cake -- --target=libs --names=Google.Maps
dotnet tool run dotnet-cake -- --target=nuget --names=Firebase.Core
```

Non-Firebase selections build in an isolated copy of the locked repository source,
using the original CocoaPods pins, Ruby hooks, project files and pack commands.
Firebase 13 selections use the verified XBD producer. An unfiltered or mixed build
partitions those requests; an unsupported Firebase 13 acquisition never falls back
to CocoaPods. The lock distinguishes Cake registry aliases from artifact IDs.

Legacy NuGets are copied to `output/`; modern NuGets remain in `output/firebase13/`.
The copy rejects missing, extra, duplicate or wrong-version packages. Legacy native
outputs owned by non-Firebase projects and Podfile resolution evidence retain their
established `externals/` paths. Shared Firebase 13 framework aliases are preserved.

The explicit legacy entry point is also available:

```sh
dotnet tool run dotnet-cake legacy.cake --target=nuget --names=Google.SignIn
dotnet tool run dotnet-cake legacy.cake --target=plan --names=Google.TagManager
```

The Sign-In/Firebase integration fixture build explicitly requests its Firebase 12
baseline with `--producer=legacy`. That option selects historical production inputs;
it is not a Firebase 13 fallback. Existing Sign-In NuGet bounds still constrain
consumer combinations with Firebase 13. This producer isolation does not change
those published declarations.

## Source isolation and reproducibility

The producer materializes only the pinned Git tree into `output/legacy-sources/`.
It does not reset the checkout, move a branch, or copy untracked files or local
credentials. A shallow checkout may need one Git fetch of the pinned source commit.
CocoaPods retains its existing producer-time networking requirements. Consumer
native acquisition remains specific to each package's original delivery model.

Metadata-only source copies are discarded automatically. Build workspaces remain
for inspection and are removed by the existing `clean` target. Current non-Firebase
source, package readmes/icons, shared compiler inputs and Cake declarations are
checked against the locked inputs. A later edit fails with guidance instead of
silently building stale source. Advancing a legacy baseline requires a reviewed
compatible source commit and package/version validation; the published 10.1.0
fingerprint fixture is retained as regression evidence.

Release fingerprints and native-reference inspection use the same locked source
that actually builds these packages. The producer commit is recorded separately in
validation manifests. The frozen legacy metadata and source fingerprints match the
published Sign-In 10.1.0 release, allowing reuse without a binding revision. No
existing package ID/version may be overwritten with a different payload.

## Validation

The read-only Firebase experiment workflow validates producer partitioning, pinned
source materialization, output identity checks, mutation rejection and the published
Sign-In fingerprint fixture. Its separate `legacy-signin` job runs the original
production command, validates the exact CocoaPods resolution and packed native
references, and builds direct/transitive simulator, unsigned-device and Catalyst
consumers with native HTTP(S) acquisition blocked. Combined consumers use matching
Firebase 12 fixtures. The existing Sign-In release pipeline uses that same baseline.

No publication job or deployment approval is added by this validation lane. Real
OAuth/account interaction and Windows/Pair-to-Mac execution remain outside these
secret-free checks. Deferred products retain their original source and pins; the
new native execution gate exercises Sign-In rather than every deferred SDK.
