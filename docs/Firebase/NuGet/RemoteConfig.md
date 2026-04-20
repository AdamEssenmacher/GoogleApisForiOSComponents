# AdamE.Firebase.iOS.RemoteConfig

.NET bindings for Firebase Remote Config on Apple platforms.

## Scope

Remote Config defaults, fetch, activation, realtime update, custom signal, settings, and value APIs exposed by the Firebase Apple SDK.

These packages are thin bindings over the native Firebase Apple SDK. The native documentation is the source of truth for product behavior, Firebase console setup, quotas, policy requirements, and feature workflows.

## Native Documentation

- Firebase Apple setup: https://firebase.google.com/docs/ios/setup
- Firebase Remote Config documentation: https://firebase.google.com/docs/remote-config/use-config-ios

## Package

- Package ID: `AdamE.Firebase.iOS.RemoteConfig`
- Managed namespace: `Firebase.RemoteConfig`

Supported target frameworks include:

- `net9.0-ios`
- `net10.0-ios`
- `net9.0-maccatalyst`
- `net10.0-maccatalyst`

When multi-targeting, condition package references so they restore only for Apple targets:

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' Or $([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">
  <PackageReference Include="AdamE.Firebase.iOS.RemoteConfig" Version="x.y.z" />
</ItemGroup>
```

## Binding Notes

Use the official Firebase Apple docs for setup and usage. In .NET, call the equivalent APIs from the managed namespace listed above. Keep app-specific Firebase configuration, such as `GoogleService-Info.plist`, in the application project.

No product walkthrough is maintained here. Add only binding-specific caveats that are not covered by the native docs.

## Version Alignment

Firebase Apple SDKs are packaged as native xcframeworks. Applications should pin package versions intentionally and keep all `AdamE.Firebase.iOS.*` packages on the same major/minor Firebase line.

Avoid mixing unrelated Firebase binding package sets or mismatched Firebase native SDK lines in one application. That can cause duplicate symbols, linker failures, runtime loading failures, or undefined native SDK behavior.

## Repository

- Repository: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents
- Issues: https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues
