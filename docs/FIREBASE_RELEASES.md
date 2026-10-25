# Firebase releases

Firebase packages and their repository-owned Google dependencies are validated and
published by `firebase-release.yml`. Package IDs and versions remain defined by the
existing projects and Cake metadata. Third-party packages are consumed, not republished.

## Preparing a release

1. Open a version-update PR. Update affected package versions and native metadata,
   plus the consumer harness fixtures. For SDK upgrades, use the existing
   `firebase-release-update` / `firebase-release-check` targets.
2. Set `releaseVersion` in `eng/firebase-release.json` to a new stable release version.
   The first supported release is `12.12.0`. The initial null value disables publication.
   A binding-only release can use a revision such as `12.12.0.1`; individual package
   versions may differ from the release version.
3. Wait for **Firebase validation** before merging. Relevant changes run full build/pack,
   unit tests, package inspection, simulator smoke/surface/nullability checks, and two
   local Firestore emulator checks. Unrelated changes report an explicit successful no-op.
4. After merge, CI validates the exact main commit. Review the workflow summary's package
   versions, reused dependencies, upload destinations, and commit. Select **Review
   deployments**, approve `firebase-release`, and the protected publish job continues.
5. After both feeds are verified, automation publishes `firebase-v<releaseVersion>` and
   a GitHub Release with package assets, manifest, checksums, and generated notes.

Merging alone does not authorize a deployment. PRs cannot publish. A main-branch run
is release-eligible when the release version changes; a manual run on main can rebuild
and revalidate an incomplete candidate. No ordinary tag push publishes packages.

## What needs publishing

The metadata-only command exports the Firebase dependency closure without downloading
SDKs or editing project files:

```sh
dotnet tool restore
dotnet tool run dotnet-cake -- --target=firebase-publish-metadata
python3 scripts/firebase-release/release.py plan
```

The initial `eng/firebase-release-baseline.json` records input fingerprints, not a
previous publication. It is immutable after setup. Successful releases supply future
baselines through their `manifest.json` asset. Use `plan --online` to include that baseline.

Package inputs include tracked binding/project files, explicitly packed assets, linked
source, dependency requirements, per-component native definitions and download sources,
and shared build/toolchain files. Shared packaging changes conservatively require bumps
across the closure. Changing a single Cake component value does not change unrelated
components' fingerprints. Project-reference version changes affect consumers' fingerprints.
Development-only documentation and test edits do not require package version bumps.

The native runner and Xcode pins live in `nativeBuild` in `eng/firebase-release.json`;
the workflow reads those pins, and they are part of every package's fingerprint.
The .NET SDK and tool pins remain in the existing repository configuration. Advancing
a shared build/toolchain pin requires explicit package-version bumps across the closure.

An unchanged dependency keeps its version. Existing packages are downloaded and reused
when a feed needs that version; the consumer tests restore the staged effective package
set. A feed disagreement fails validation. NuGet repository signatures and ZIP timestamps
are excluded from payload comparison; all actual payload entries must match.

The setup does not backfill Firebase 12.11 or older. An approved release can copy older
support-library versions to a missing feed when they are required by its dependency closure.

## Account setup

Create a `firebase-release` GitHub environment restricted to the `main` branch. Require
the maintainer's approval, permit self-review for a solo-maintainer workflow, and disable
administrator bypass. Set the environment variable `NUGET_USER` to the NuGet profile name.

On NuGet.org, configure Trusted Publishing for the repository owner/name, workflow filename
`firebase-release.yml`, and environment `firebase-release`. Scope it to the exact IDs
exported by the metadata target. Enable pushing packages/versions, with no unlist permission.
If the dependency closure expands, review and update this scope before releasing it.

The protected publish job requests a short-lived NuGet key with `NuGet/login`. GitHub
Packages and release creation use `GITHUB_TOKEN`. No long-lived publishing key or application
secret is needed. Protect main with the required **Firebase validation** check, require an
up-to-date PR, and retain force-push/deletion protections. A solo maintainer can use zero
required PR reviewers while keeping the separate deployment approval.

## Failed or interrupted releases

Two package registries cannot be updated atomically. If one upload fails, the workflow fails
and reports the package/feed. Choose **Re-run failed jobs** to reuse the retained, validated
candidate. The environment requires approval again; existing matching versions are skipped.
An existing version with different contents is an error, never an overwrite.

Candidates and diagnostics are retained for 90 days. Once artifacts expire, manually run
the workflow on main to rebuild and revalidate, then review the new candidate. If rebuilding
produces different payloads from a partially published version, create a new version PR.
Do not overwrite the published version or reset the baseline.

Publication is serialized. A newer completed release prevents an older candidate from
publishing afterward. Existing tags must match the tested commit. Release assets are first
uploaded to a draft, allowing interrupted uploads to resume before the release becomes
public. No Firebase version update or package publication is part of the initial setup PR.

## Local tooling tests

```sh
python3 -m unittest discover -s tests/firebase-release -v
```

The tests cover version impact, dependency constraints, package scope, signed payloads,
feed states/errors, artifact tampering, incomplete validation, and retry behavior.
