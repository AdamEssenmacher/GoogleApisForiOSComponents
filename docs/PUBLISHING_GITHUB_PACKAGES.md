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
