# AdamE.Firebase.iOS.AppDistribution

.NET bindings for Firebase App Distribution on Apple platforms.

## Scope

Tester sign-in, sign-out, release lookup, and update-check APIs exposed by the Firebase Apple SDK.

These packages are thin bindings over the native Firebase Apple SDK. The native documentation is the source of truth for product behavior, Firebase console setup, quotas, policy requirements, and feature workflows.

## Native Documentation

- Firebase Apple setup: https://firebase.google.com/docs/ios/setup
- Firebase App Distribution documentation: https://firebase.google.com/docs/app-distribution

## Package

- Package ID: `AdamE.Firebase.iOS.AppDistribution`
- Managed namespace: `Firebase.AppDistribution`

Supported target frameworks include:

- `net9.0-ios`
- `net10.0-ios`
- `net9.0-maccatalyst`
- `net10.0-maccatalyst`

When multi-targeting, condition package references so they restore only for Apple targets:

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' Or $([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">
  <PackageReference Include="AdamE.Firebase.iOS.AppDistribution" Version="x.y.z" />
</ItemGroup>
```

## Installation

```sh
dotnet add package AdamE.Firebase.iOS.AppDistribution
```

## Binding Notes

Use the official Firebase Apple docs for setup and usage. In .NET, call the equivalent APIs from the managed namespace listed above. Keep app-specific Firebase configuration, such as `GoogleService-Info.plist`, in the application project.

Most Firebase feature packages require `AdamE.Firebase.iOS.Core` and app startup should call `Firebase.Core.App.Configure()` before feature APIs are used. This is the .NET binding for native `FirebaseApp.configure()`.

The native Firebase App Distribution Apple SDK can use beta native framework versions behind the aggregate Firebase package line. Keep the NuGet package line aligned with the rest of Firebase in the application.

## Version Alignment

Firebase Apple SDKs are packaged as native xcframeworks. Applications should pin package versions intentionally and keep all `AdamE.Firebase.iOS.*` packages on the same major/minor Firebase line.

Avoid mixing unrelated Firebase binding package sets or mismatched Firebase native SDK lines in one application. That can cause duplicate symbols, linker failures, runtime loading failures, or undefined native SDK behavior.

## Repository

- Repository: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents
- Issues: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues
