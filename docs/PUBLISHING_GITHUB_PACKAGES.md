# Publishing to GitHub Packages

This repository publishes Firebase `.nupkg` files and their repository-owned dependencies
to GitHub Packages and NuGet.org through the approved [Firebase release workflow](FIREBASE_RELEASES.md).

## How Publishing Works

- A version-update PR passes CI and is merged to main.
- CI builds and validates the exact merged commit, then waits for deployment approval.
- The approved artifacts are published to both feeds, followed by an automatic tag and release.
- The workflow can be run manually on main to rebuild and validate an incomplete release.
- Packages are pushed to `https://nuget.pkg.github.com/<OWNER>/index.json`.

GitHub publishing uses the built-in `GITHUB_TOKEN`; NuGet.org uses Trusted Publishing.
No app-specific publishing secret is required. Tag pushes and arbitrary component names
no longer trigger publication. See the Firebase release guide for scope and recovery details.

## Components outside the Firebase closure

SignIn, Maps, Places, Cast, and other unrelated components have no automatic publisher.
Tag pushes do not publish them. Build and validate their explicitly versioned packages
with the existing Cake entry point, for example:

```sh
dotnet tool restore
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
```

For a deliberate manual release, inspect the resulting packages in `output/`, validate
consumers, and authenticate a local NuGet source using a GitHub personal access token
(classic) with `write:packages` and access to this repository. Follow
[GitHub's NuGet registry instructions](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).
Push each reviewed package by its exact filename, in dependency order:

```sh
dotnet nuget push output/<PACKAGE-ID>.<VERSION>.nupkg --source github --api-key "$GITHUB_PACKAGES_TOKEN"
```

Keep the token in local configuration or the process environment, never in the repository.
Verify the published payload and version before creating a manual release. The Firebase
Trusted Publishing policy does not grant NuGet.org publishing rights for these packages;
any NuGet.org release needs separate package-owner authorization and credentials.

## Consuming Packages

Add GitHub Packages as a NuGet source and authenticate using a GitHub token that can read packages for the owner account. Local development usually needs a PAT with `read:packages`; private repositories can also require repository access.

Example local `NuGet.Config` source:

```xml
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/<OWNER>/index.json" />
  </packageSources>
</configuration>
```

Keep credentials in local user-level NuGet configuration or local environment variables. Do not commit package-feed credentials.
