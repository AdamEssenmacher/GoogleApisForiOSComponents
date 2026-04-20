# Publishing to GitHub Packages

This repository can publish `.nupkg` files to GitHub Packages through GitHub Actions.

## How Publishing Works

- A tag push triggers the publish workflow.
- The workflow builds and packs NuGet packages.
- The workflow can also be run manually with `workflow_dispatch` and a custom Cake `--names` value.
- Packages are pushed to `https://nuget.pkg.github.com/<OWNER>/index.json`.

The publish workflow uses the built-in `GITHUB_TOKEN`; no app-specific publishing secret is required.

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
