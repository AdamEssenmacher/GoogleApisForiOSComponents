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

## Troubleshooting

### MSB4057: The target "source/..." does not exist

This error can occur when using MSBuild solution-level targets with certain .NET SDK versions. The `build.cake` script avoids this by building each `.csproj` directly in dependency order, which is more explicit and reliable.

### NU1101: Unable to find package AdamE.Google.iOS.AppCheckCore

Some packages (like `SignIn`) depend on `AppCheckCore` which is built from this repo. The Cake script handles this automatically by building dependencies first. If you still encounter this:

1. Ensure you're using the latest `build.cake` (it should iterate `ARTIFACTS_TO_BUILD`)
2. Run the full build: `dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn`

### Code signing errors during xcframework build

The Cake scripts disable code signing by default (`CODE_SIGNING_ALLOWED=NO`) for CI compatibility. If you need signed frameworks, override the build settings in `common.cake`.
