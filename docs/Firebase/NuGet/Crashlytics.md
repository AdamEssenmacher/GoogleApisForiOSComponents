# AdamE.Firebase.iOS.Crashlytics

.NET bindings for Firebase Crashlytics on Apple platforms.

## Scope

Native crash reporting, custom key, log, user identifier, and non-fatal error APIs exposed by the Firebase Apple SDK.

These packages are thin bindings over the native Firebase Apple SDK. The native documentation is the source of truth for product behavior, Firebase console setup, quotas, policy requirements, and feature workflows.

## Native Documentation

- Firebase Apple setup: https://firebase.google.com/docs/ios/setup
- Firebase Crashlytics documentation: https://firebase.google.com/docs/crashlytics/get-started

## Package

- Package ID: `AdamE.Firebase.iOS.Crashlytics`
- Managed namespace: `Firebase.Crashlytics`

Supported target frameworks include:

- `net9.0-ios`
- `net10.0-ios`
- `net9.0-maccatalyst`
- `net10.0-maccatalyst`

When multi-targeting, condition package references so they restore only for Apple targets:

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' Or $([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">
  <PackageReference Include="AdamE.Firebase.iOS.Crashlytics" Version="x.y.z" />
</ItemGroup>
```

## Installation

```sh
dotnet add package AdamE.Firebase.iOS.Crashlytics
```

## Binding Notes

Use the official Firebase Apple docs for setup and usage. In .NET, call the equivalent APIs from the managed namespace listed above. Keep app-specific Firebase configuration, such as `GoogleService-Info.plist`, in the application project.

Most Firebase feature packages require `AdamE.Firebase.iOS.Core` and app startup should call `Firebase.Core.App.Configure()` before feature APIs are used. This is the .NET binding for native `FirebaseApp.configure()`.

Crashlytics is native crash reporting, not a complete managed .NET exception-reporting replacement. On .NET 8 iOS projects, the top-level README includes an `_ExportSymbolsExplicitly` workaround for missing symbol/export issues.

There is no separate `Crashlytics.Configure()` call in the current binding surface. After Firebase is configured, use `Firebase.Crashlytics.Crashlytics.SharedInstance` for Crashlytics APIs such as `Log(...)`, `SetCustomValue(...)`, `SetUserId(...)`, `RecordError(...)`, and `RecordExceptionModel(...)`.

Runtime collection controls are exposed through `Crashlytics.SharedInstance.SetCrashlyticsCollectionEnabled(...)`, `IsCrashlyticsCollectionEnabled`, `CheckForUnsentReports(...)`, `SendUnsentReports()`, and `DeleteUnsentReports()`. Use the native Crashlytics docs as the source of truth for the matching plist keys and consent behavior.

The NuGet package also ships MSBuild targets for Firebase dSYM symbol upload. Release app builds enable symbol upload by default. Set these properties in the consuming app project file, or in `Directory.Build.targets` for a shared repo policy.

To disable symbol upload:

```xml
<PropertyGroup>
  <FirebaseCrashlyticsUploadSymbolsEnabled>false</FirebaseCrashlyticsUploadSymbolsEnabled>
</PropertyGroup>
```

To keep upload enabled but fail the build if upload fails:

```xml
<PropertyGroup>
  <FirebaseCrashlyticsUploadSymbolsContinueOnError>false</FirebaseCrashlyticsUploadSymbolsContinueOnError>
</PropertyGroup>
```

For one-off builds, pass the same values as command-line MSBuild properties, such as `dotnet build -c Release /p:FirebaseCrashlyticsUploadSymbolsEnabled=false`. Prefer the app `.csproj`, `Directory.Build.targets`, or command-line properties over `Directory.Build.props` so the value is applied after NuGet package props set their defaults. The upload target expects the app bundle to contain `GoogleService-Info.plist`.

## Version Alignment

Firebase Apple SDKs are packaged as native xcframeworks. Applications should pin package versions intentionally and keep all `AdamE.Firebase.iOS.*` packages on the same major/minor Firebase line.

Avoid mixing unrelated Firebase binding package sets or mismatched Firebase native SDK lines in one application. That can cause duplicate symbols, linker failures, runtime loading failures, or undefined native SDK behavior.

## Repository

- Repository: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents
- Issues: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues
