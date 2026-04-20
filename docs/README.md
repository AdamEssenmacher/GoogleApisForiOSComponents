# Documentation

This repository contains thin .NET bindings for native Google, Firebase, and ML Kit Apple SDKs. The native SDK documentation is the source of truth for product behavior, platform setup, console workflows, quotas, policy requirements, and feature examples.

The docs in this repository should cover only the binding layer: package IDs, managed namespaces, supported target frameworks, NuGet consumption, version alignment, build and release workflows, dependency conflict notes, and maintainer decisions that cannot live in the native SDK docs.

## What belongs here

- NuGet package names and target framework guidance.
- Binding-specific initialization or packaging caveats.
- Native dependency and version-alignment rules for these packages.
- Repository build, validation, CI, and publishing workflows.
- Maintainer notes for binding API shape, audit policy, and runtime-drift triage.

## What does not belong here

- Rewritten Firebase, Google, or ML Kit product walkthroughs.
- Console setup flows, quotas, billing, policy, or dashboard behavior.
- App-specific examples, secrets, private configuration, or private business context.
- Broad native SDK tutorials that are already maintained by Google or Firebase.

## Current package docs

Firebase package READMEs are in [Firebase/NuGet](Firebase/NuGet). These files are packed as `NUGET_README.md` for the Firebase packages and should stay short.

Shared package README text for Google/support packages is in [NUGET_README.md](NUGET_README.md). Package-specific Google and ML Kit docs should be added only when they document binding-specific details that the native docs cannot cover.

## Build and release docs

- [Building locally](BUILDING.md)
- [Publishing to GitHub Packages](PUBLISHING_GITHUB_PACKAGES.md)

## Maintainer notes

- [Firebase Binding API Shape Policy](firebase-binding-api-shape-policy.md)
- [Firebase Runtime Failure Backlog](firebase-runtime-failure-backlog.md)

## Legacy component paths

Some older component paths are intentionally retained so existing links do not break. Those files now point readers to the official native documentation and to the relevant package README instead of duplicating native SDK docs.
