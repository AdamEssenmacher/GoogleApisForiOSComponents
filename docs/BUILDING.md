# Building Locally

This repository builds .NET iOS and Mac Catalyst bindings plus NuGet packages using Cake.

## Prerequisites

- .NET SDK from `global.json`.
- Xcode compatible with the installed .NET Apple workloads.
- CocoaPods (`pod`).

Restore the checked-in Cake tool manifest before building:

```sh
dotnet tool restore
```

## GitHub Packages Feed for Forks

If you are working from a fork and need to restore packages published from that fork, configure a GitHub Packages source:

```sh
./scripts/configure-github-feed.sh --gh
```

You can also use a local personal access token with `read:packages` scope:

```sh
export GITHUB_PACKAGES_PAT="your_github_pat_here"
./scripts/configure-github-feed.sh
```

Do not commit tokens or generated local NuGet credentials.

The script detects the fork owner from the git remote, adds a source named like `github-<YourUsername>`, and allows `dotnet restore` to resolve fork-published packages.

## Build and Pack One Component

Build and produce `.nupkg` files under `./output`:

```sh
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
```

## Clean Generated Output

```sh
dotnet tool run dotnet-cake -- --target=clean
```

## Troubleshooting

### MSB4057: The target "source/..." does not exist

This can happen when using MSBuild solution-level targets with some .NET SDK versions. The Cake script builds `.csproj` files directly in dependency order to avoid that failure mode.

### NU1101: Unable to find package AdamE.Google.iOS.AppCheckCore

Some packages depend on other packages built from this repository. The Cake script handles dependency-first packing. If restore still fails, run the component build through Cake instead of building the project directly:

```sh
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
```

### Code signing errors during xcframework build

The Cake scripts disable code signing by default (`CODE_SIGNING_ALLOWED=NO`) for CI compatibility. If you need signed frameworks, override the build settings locally instead of committing signing material.

### NuGet feed issues

If restore cannot find packages from a fork, verify the configured sources:

```sh
dotnet nuget list source
```

Then clear local NuGet caches if needed:

```sh
dotnet nuget locals all --clear
dotnet restore
```
