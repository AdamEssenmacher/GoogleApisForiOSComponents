# Overview

This package is part of a set of .NET bindings for Google and Firebase native iOS SDKs.

## Target frameworks

These packages are intended for iOS and Mac Catalyst TFMs (for example `net8.0-ios`, `net9.0-ios`, `net10.0-ios`).

When multi-targeting, condition the reference so it only restores for iOS:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0-ios'">
  <PackageReference Include="AdamE.Google.iOS.SignIn" Version="9.0.0.0" />
</ItemGroup>
```

## Notes on native dependency conflicts

Google/Firebase iOS SDKs share native dependencies (xcframeworks). Avoid mixing multiple independent bindings that ship overlapping Google/Firebase native SDK binaries in the same app, as it can lead to duplicate symbols or runtime issues.

