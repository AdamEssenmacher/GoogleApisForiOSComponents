# AdamE.Firebase.iOS.CloudMessaging

.NET bindings for Firebase Cloud Messaging on Apple platforms, for use from .NET iOS and Mac Catalyst apps.

## What this package provides

This package binds the Firebase Cloud Messaging Apple SDK surface exposed in the `Firebase.CloudMessaging` namespace. It provides access to `Messaging`, FCM token APIs, APNs token assignment, topic subscription APIs, message delivery metadata, and notification service extension helper APIs.

Use this package when you need:

- FCM registration token retrieval from C#
- APNs token registration through `Messaging.SetApnsToken`
- topic subscribe and unsubscribe operations
- notification extension helper access for rich notification handling

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
- Firebase Cloud Messaging documentation: https://firebase.google.com/docs/cloud-messaging/ios/client

## Supported target frameworks

This package is intended for Apple platform TFMs such as:

- `net9.0-ios`
- `net10.0-ios`
- `net9.0-maccatalyst`
- `net10.0-maccatalyst`

When multi-targeting, condition the package reference so it only restores for Apple targets.

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' Or $([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">
  <PackageReference Include="AdamE.Firebase.iOS.CloudMessaging" Version="12.6.0" />
</ItemGroup>
```

## Installation

```sh
dotnet add package AdamE.Firebase.iOS.CloudMessaging
```

## Basic usage

This package does not itself perform Firebase app initialization; call `Firebase.Core.App.Configure()` from the app before using Firebase feature APIs. Follow the official Firebase and Apple documentation for APNs registration and notification permissions.

```csharp
using System;
using Firebase.CloudMessaging;
using Firebase.Core;

App.Configure();

var messaging = Messaging.SharedInstance;
messaging.AutoInitEnabled = true;

messaging.FetchToken((fcmToken, error) =>
{
    if (error is not null)
    {
        Console.WriteLine(error.LocalizedDescription);
        return;
    }

    Console.WriteLine(fcmToken);
});

messaging.Subscribe("news");
```

## Common companion packages

- `AdamE.Firebase.iOS.Core` - Firebase app initialization.
- `AdamE.Firebase.iOS.Installations` - package metadata references Firebase Installations for underlying Firebase identity support.
- `AdamE.Firebase.iOS.Analytics` - commonly used when Cloud Messaging analytics and notification engagement reporting are needed.

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
  <PackageReference Include="AdamE.Firebase.iOS.Core" Version="12.6.0" />
  <PackageReference Include="AdamE.Firebase.iOS.Auth" Version="12.6.0" />
  <PackageReference Include="AdamE.Firebase.iOS.CloudFirestore" Version="12.6.0" />
</ItemGroup>
```

Avoid mixing mismatched Firebase package lines such as `12.6.x.x` with `12.5.x.x`, or `12.x.x.x` with `11.x.x.x`. Doing so can lead to native dependency conflicts, duplicate symbols, runtime failures, or other undefined behavior.

## Notes on native dependency conflicts

Google and Firebase Apple SDKs share native dependencies. Avoid mixing multiple unrelated binding packages that embed overlapping Google/Firebase native SDK binaries in the same app unless you are certain they are compatible.

## API surface notes

The public namespace is `Firebase.CloudMessaging`. API names closely mirror the native Firebase Cloud Messaging SDK surface and expose Apple-native concepts such as `NSData`, `NSDictionary`, `NSError`, and notification extension types.

## Repository / support

- Repository: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents
- Issues: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues

## Support the project

Keeping Firebase Apple bindings current for .NET requires ongoing work across SDK updates, native dependency changes, and API surface maintenance.

If this package is valuable in your app or organization, sponsorship helps support continued maintenance and updates.

- GitHub Sponsors: https://github.com/sponsors/AdamEssenmacher
