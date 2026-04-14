# AdamE.Firebase.iOS.InAppMessaging

.NET bindings for Firebase In-App Messaging on iOS, for use from .NET iOS apps.

## What this package provides

This package binds the Firebase In-App Messaging Apple SDK surface exposed in the `Firebase.InAppMessaging` namespace. It provides access to message display suppression, automatic data collection, custom trigger events, display delegates, display component protocols, and message model types for banner, card, modal, and image-only messages.

Use this package when you need:

- Firebase In-App Messaging campaign display control from C#
- custom trigger events with `InAppMessaging.TriggerEvent`
- display delegate callbacks for impressions, clicks, dismissals, and display errors
- custom message display component integration

Most apps using this package also reference `AdamE.Firebase.iOS.Core` for Firebase app initialization.

## Official Firebase documentation comes first

These packages are **thin .NET bindings over the official Firebase Apple SDKs**.

Use the official Firebase documentation as the starting point for:

- Firebase configuration and platform setup
- feature usage and behavioral guidance
- troubleshooting and best practices

These bindings primarily:

- expose the native Firebase Apple SDK APIs to .NET through C#
- deliver the packaged native Firebase SDK artifacts through NuGet

- Firebase documentation: https://firebase.google.com/docs
- Firebase Apple platform setup: https://firebase.google.com/docs/ios/setup
- Firebase In-App Messaging documentation: https://firebase.google.com/docs/in-app-messaging

## Supported target frameworks

This package is intended for iOS TFMs such as:

- `net9.0-ios`
- `net10.0-ios`

When multi-targeting, condition the package reference so it only restores for iOS targets.

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <PackageReference Include="AdamE.Firebase.iOS.InAppMessaging" Version="12.6.0" />
</ItemGroup>
```

## Installation

```sh
dotnet add package AdamE.Firebase.iOS.InAppMessaging
```

## Basic usage

This package does not itself perform Firebase app initialization; call `Firebase.Core.App.Configure()` from the app before using Firebase feature APIs.

```csharp
using Firebase.Core;

App.Configure();

var inAppMessaging = Firebase.InAppMessaging.InAppMessaging.SharedInstance;
inAppMessaging.AutomaticDataCollectionEnabled = true;
inAppMessaging.MessageDisplaySuppressed = false;
inAppMessaging.TriggerEvent("purchase_complete");
```

## Common companion packages

- `AdamE.Firebase.iOS.Core` - Firebase app initialization.
- `AdamE.Firebase.iOS.Installations` - package metadata references Firebase Installations for underlying Firebase identity support.
- `AdamE.Firebase.iOS.ABTesting` - package metadata references Firebase A/B Testing for campaign experiment handling.
- `AdamE.Firebase.iOS.Analytics` - commonly used with In-App Messaging campaign triggers and measurement.

This package is part of the Firebase `12.6.0` package line in this repository. Firebase's aggregate `Firebase/InAppMessaging` `12.6.0` CocoaPods subspec resolves the native `FirebaseInAppMessaging` `12.6.0-beta` pod. The native SDK is iOS-only in this package; Mac Catalyst is intentionally not targeted because the Firebase 12.6 public headers mark In-App Messaging unavailable on Mac Catalyst.

## Firebase app configuration

Firebase apps commonly require app-specific configuration from your own Firebase project, such as `GoogleService-Info.plist`.

Keep app-specific Firebase configuration in the application project or sample app, not in reusable library projects.

If the official Firebase docs for this feature require additional setup, follow those docs first.

## Package versioning rules (important)

Because Firebase Apple SDKs are packaged as native xcframeworks and distributed here through NuGet, consumers should explicitly pin package versions.

Due to packaging differences between CocoaPods and NuGet, it is highly recommended that applications follow these rules:

1. Keep the MAJOR.MINOR version aligned across all Firebase packages in the app, for example `12.6.*.*`.
2. Then use the latest available PATCH.REVISION for each individual package.

Example:

```xml
<ItemGroup>
  <PackageReference Include="AdamE.Firebase.iOS.Core" Version="12.6.0.3" />
  <PackageReference Include="AdamE.Firebase.iOS.Auth" Version="12.6.0.2" />
  <PackageReference Include="AdamE.Firebase.iOS.CloudFirestore" Version="12.6.0.5" />
</ItemGroup>
```

Avoid mixing mismatched Firebase package lines such as `12.6.x.x` with `12.5.x.x`, or `12.x.x.x` with `11.x.x.x`. Doing so can lead to native dependency conflicts, duplicate symbols, runtime failures, or other undefined behavior.

## Notes on native dependency conflicts

Google and Firebase Apple SDKs share native dependencies. Avoid mixing multiple unrelated binding packages that embed overlapping Google/Firebase native SDK binaries in the same app unless you are certain they are compatible.

## API surface notes

The public namespace is `Firebase.InAppMessaging`. API names closely mirror the native Firebase In-App Messaging SDK surface and expose Apple-native concepts such as `NSObject`, `NSDictionary`, `NSError`, `NSUrl`, and UIKit display model types.

## Repository / support

- Repository: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents
- Issues: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues

## Support the project

Keeping Firebase Apple bindings current for .NET requires ongoing work across SDK updates, native dependency changes, and API surface maintenance.

If this package is valuable in your app or organization, sponsorship helps support continued maintenance and updates.

- GitHub Sponsors: https://github.com/sponsors/AdamEssenmacher
