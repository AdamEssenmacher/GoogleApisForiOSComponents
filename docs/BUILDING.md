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

### Configure the Google Sign-In sample

For a runnable sign-in flow on iOS, the app must register an URL scheme callback. When you provide your own `GoogleService-Info.plist` (or fill in `samples/Google/SignIn/SignInExample/GoogleService-Info.plist.template`), also update `samples/Google/SignIn/SignInExample/Info.plist` so `CFBundleURLSchemes` matches your `REVERSED_CLIENT_ID`.

## Clean-up

To clean generated folders:

```sh
dotnet tool run dotnet-cake -- --target=clean
```
