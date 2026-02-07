# Contributing Guidelines

* When opening a pull request, make sure that you've checked "Allow edits from maintainers".
* Each PR should be as focused as possible.
* Do not include unnecessary white space changes in your PR! If someone wants to make a pass standardizing white space project-wide, that can be its own PR. Otherwise, follow the convention of each individual file. If your IDE insists on changing formatting, just don't commit it!
* Do not update .NET targets/versions. I intend to keep the project backwards compatible with legacy Xamarin and .NET6 until April 2025 at least.
* For large changes, open an issue first to discuss it.

## Read this first

If you’re new to native bindings: please open a new discussion and don’t hesitate to create a draft PR early. It’s much easier to align on the approach before spending time iterating on build tooling, Pod dependency constraints, or API surface decisions.

For local build instructions and commands, see `docs/BUILDING.md`.

## What you’re contributing

This repo primarily contains:

* **Bindings** (C# API surface + native frameworks packaged into NuGet)
* **Build automation** (Cake scripts that fetch/build Pods and pack NuGets)
* **Samples** (small apps to validate the bindings work end-to-end)
* **Docs** (how to build, validate, publish)

Most contributions fall into one of these buckets:

* **Docs-only**: clarify setup, sample configuration, gotchas.
* **Dependency bumps**: update a Pod version (and any required transitive Pods), then ensure the binding still packs and the sample still builds.
* **Binding fixes**: API surface adjustments, linker/NativeReference fixes, or changes needed after a native SDK update.

## Repository orientation (quick map)

* `components.cake`: component definitions (Pod specs, sample names, dependency graph).
* `externals/`: generated/native artifacts produced from Pods (created by the build).
* `source/`: binding projects (what becomes NuGet packages).
* `samples/`: sample apps that reference the bindings.
* `.github/workflows/`: CI validation and publishing.

## Common pitfalls with iOS bindings

* **Pod constraints are not NuGet constraints**: if a Pod update pulls a new transitive dependency, you usually need to declare it explicitly in `components.cake` so the build remains deterministic.
* **Minimum deployment target**: some Pods require a higher iOS deployment target; this can surface as CocoaPods resolution errors or build failures.
* **Signing vs build-only**: device builds require provisioning; CI should validate “build-only” (no signing) where possible.
* **URL scheme callbacks**: OAuth-style flows often require `Info.plist` URL scheme changes in addition to config files.

## Versioning notes (Cake vs .csproj)

The Cake pipeline updates versions in binding `.csproj` files during the `externals` step:

* `FileVersion` is always set to `artifact.NugetVersion` (from `components.cake`).
* `PackageVersion` is set to `artifact.NugetVersion` **unless** the project already specifies a pre-release suffix (e.g. `12.5.0.4-fork`).

This allows forked builds to publish `-fork` (or similar) packages without changing the shared component version line in `components.cake`, while keeping deterministic, aligned build outputs.

## Fork testing suffix policy (`-local` vs `-fork`)

When validating a binding fix before upstream release:

* Use `-local` for packages built on a developer machine and consumed from a local NuGet source.
* Use `-fork` for packages built by your fork CI and published to GitHub Packages.

Suggested flow:

1. Set a temporary prerelease `PackageVersion` (for example `12.5.0.4-fork` or `12.5.0.4-local`) in the affected project(s).
2. Build/publish from the matching channel (local machine for `-local`, GitHub Actions for `-fork`).
3. Consume that package from downstream repos to validate the fix.
4. Before opening an upstream PR, revert temporary prerelease versions/references back to the canonical version line expected upstream.

Important: do not merge or submit upstream PRs with temporary fork-only or local-only package versions unless explicitly requested by maintainers.

## Validation checklist (before requesting review)

At minimum, validate the component(s) you touched:

* `dotnet tool restore`
* `dotnet tool run dotnet-cake -- --target=nuget --names=<ComponentName>`
* `dotnet tool run dotnet-cake -- --target=samples --names=<ComponentName>`

Also:

* Keep samples and docs **generic** (no app-specific context).
* Do not commit secrets or service configuration files; prefer `.template` files and document the required local steps.
