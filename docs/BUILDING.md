# Building locally (macOS)

This repo builds .NET iOS/macOS bindings and NuGet packages using Cake.

## Prerequisites

- .NET SDK (see `global.json`)
- Xcode (matching the installed iOS workload requirements)
- CocoaPods (`pod`)

Install Cake (any version `< 1.0` should work):

```sh
dotnet tool install -g cake.tool --version 0.38.5
export PATH="$PATH:$HOME/.dotnet/tools"
```

## Build + pack a component

Build and produce `.nupkg` files into `./output`:

```sh
dotnet cake --target=nuget --names=Google.SignIn
```

Clean generated folders:

```sh
dotnet cake --target=clean
```
