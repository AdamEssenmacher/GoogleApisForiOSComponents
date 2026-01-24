# Building locally (macOS)

This repo builds .NET iOS/macOS bindings and NuGet packages using Cake.

## Prerequisites

- .NET SDK (see `global.json`)
- Xcode (matching the installed iOS workload requirements)
- CocoaPods (`pod`)

Restore the local Cake tool (any version `< 1.0` should work):

```sh
dotnet tool restore
```

## Build + pack a component

Build and produce `.nupkg` files into `./output`:

```sh
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
```

## Build samples (using source)

Build sample projects (without publishing packages):

```sh
dotnet tool run dotnet-cake -- --target=samples --names=Google.SignIn
```

Clean generated folders:

```sh
dotnet tool run dotnet-cake -- --target=clean
```
