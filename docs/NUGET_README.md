# Package Notes

This package is part of a set of .NET bindings for native Google, Firebase, and ML Kit Apple SDKs.

## Native documentation is authoritative

These packages expose native Apple SDK APIs to .NET. Use the official native SDK documentation for product setup, feature behavior, console configuration, quotas, and troubleshooting.

This package README covers only binding and NuGet concerns.

## Target frameworks

These packages are intended for iOS and Mac Catalyst TFMs, for example:

- `net9.0-ios`
- `net10.0-ios`
- `net9.0-maccatalyst`
- `net10.0-maccatalyst`

When multi-targeting, condition the reference so it restores only for Apple targets:

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' Or $([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">
  <PackageReference Include="AdamE.Google.iOS.PackageName" Version="x.y.z" />
</ItemGroup>
```

Replace the package ID and version with the package you are consuming.

## Native dependency conflicts

Google, Firebase, and ML Kit Apple SDKs share native xcframework dependencies. Avoid mixing independent binding package sets that embed overlapping native Google/Firebase binaries in the same app unless you have verified that the native dependency versions are compatible.

For application projects, pin package versions intentionally and keep related Google/Firebase/ML Kit packages on compatible lines.

## Repository

- Repository: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents
- Issues: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues
