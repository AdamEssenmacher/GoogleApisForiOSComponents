# Publishing to GitHub Packages (NuGet)

This repository can publish `.nupkg` files to GitHub Packages (NuGet feed) via GitHub Actions.

## How publishing works

- A tag push triggers the publish workflow.
- The workflow builds and packs NuGet packages.
- The workflow pushes packages to:
  - `https://nuget.pkg.github.com/<OWNER>/index.json`

## Consuming packages

Add GitHub Packages as a NuGet source and authenticate using a GitHub PAT (or workflow token in CI).

Create a local `NuGet.Config` (or update your existing one) and add:

```xml
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/<OWNER>/index.json" />
  </packageSources>
</configuration>
```

Then restore using credentials for the `<OWNER>` account.

