# Overview

This repository is a community-supported continuation of the Xamarin Google/Firebase iOS binding libraries. Microsoft stopped updating the original `Xamarin.Firebase.iOS.*` and `Xamarin.Google.iOS.*` packages, so I maintain and publish compatible packages under these prefixes:

- `AdamE.Firebase.iOS.*`
- `AdamE.Google.iOS.*`
- `AdamE.MLKit.iOS.*`

The goal is to give .NET for iOS and Mac Catalyst developers a common binding baseline for Google, Firebase, and ML Kit native SDKs. That helps downstream libraries share the same native dependencies instead of each shipping incompatible copies of overlapping xcframeworks.

This repo contains the binding projects, Cake build automation, documentation, and sample apps used to build and validate those packages.

## Quick links

- Build locally: [docs/BUILDING.md](docs/BUILDING.md)
- Publish to GitHub Packages: [docs/PUBLISHING_GITHUB_PACKAGES.md](docs/PUBLISHING_GITHUB_PACKAGES.md)
- Contributing guidelines: [CONTRIBUTING.md](CONTRIBUTING.md)
- Repository license: [License.md](License.md)

Local builds use the checked-in Cake tool manifest:

```sh
dotnet tool restore
dotnet tool run dotnet-cake -- --target=nuget --names=Google.SignIn
```

## Installation and usage notes

These packages are intended for Apple TFMs such as `net9.0-ios`, `net10.0-ios`, and their Mac Catalyst equivalents.

### `Firebase.Installations` may need an explicit reference

Some package combinations still work more reliably when `AdamE.Firebase.iOS.Installations` is referenced explicitly, even if it looks like it should be brought in transitively.

### Crashlytics is native crash reporting, not full .NET exception reporting

The underlying Crashlytics SDK does not understand managed .NET exceptions the same way it understands native crashes. Treat it as native crash reporting support, not a complete .NET error-reporting solution.

On .NET 8 iOS projects, set this property to avoid missing symbol/export issues:

```xml
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <_ExportSymbolsExplicitly>false</_ExportSymbolsExplicitly>
</PropertyGroup>
```

### Condition package references for Apple targets only

If your project targets multiple platforms, keep these packages inside Apple-only `ItemGroup` conditions. Unconditional references in multi-targeted projects can lead to restore/build hangs.

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <PackageReference Include="AdamE.Firebase.iOS.Core" Version="12.6.0" />
</ItemGroup>
```

Use the equivalent `maccatalyst` condition for Mac Catalyst targets.

### Windows and Visual Studio users should expect long-path limitations

These packages include native xcframework contents, which can create very long file paths once NuGet expands them locally. On Windows and in Visual Studio, that can surface as restore failures, build failures, or archive failures.

If you hit errors such as `Could not find a part of the path...` or `Could not find or use auto-linked framework...`, the usual recovery steps are:

1. Enable Windows long-path support.
2. Close Visual Studio.
3. Clear the NuGet cache and the local Xamarin build download cache.
4. Run `dotnet restore` and `dotnet build` from the CLI first.
5. Reopen Visual Studio.

Archiving can still be more reliable on macOS or from the command line.

## Package status

The lists below reflect what is currently published on [nuget.org](https://www.nuget.org/profiles/adamessenmacher) under the `adamessenmacher` owner profile. That is intentionally different from the repo state in [`components.cake`](components.cake): the repo can contain projects or version bumps that have not been published yet.

Firebase `12.6.0` is the current published Firebase package line.

### Currently published on nuget.org

#### Firebase packages (`AdamE.Firebase.iOS.*`)

| Package | Version |
| --- | --- |
| `ABTesting` | `12.6.0` |
| `Analytics` | `12.6.0` |
| `AppCheck` | `12.6.0` |
| `AppDistribution` | `12.6.0` |
| `Auth` | `12.6.0` |
| `CloudFirestore` | `12.6.0` |
| `CloudFunctions` | `12.6.0` |
| `CloudMessaging` | `12.6.0` |
| `Core` | `12.6.0` |
| `Crashlytics` | `12.6.0` |
| `Database` | `12.6.0` |
| `InAppMessaging` | `12.6.0` |
| `Installations` | `12.6.0` |
| `PerformanceMonitoring` | `12.6.0` |
| `RemoteConfig` | `12.6.0` |
| `Storage` | `12.6.0` |

#### Google packages (`AdamE.Google.iOS.*`)

| Package | Version |
| --- | --- |
| `Maps` | `9.2.0.8` |
| `Places` | `7.4.0.2` |
| `SignIn` | `9.0.0` |

#### Google support packages (`AdamE.Google.iOS.*`)

These packages are usually consumed transitively rather than referenced directly:

| Package | Version |
| --- | --- |
| `AppCheckCore` | `11.2.0` |
| `GoogleAppMeasurement` | `12.6.0` |
| `GoogleDataTransport` | `10.1.0.5` |
| `GoogleUtilities` | `8.1.0.3` |
| `Nanopb` | `3.30910.0.5` |
| `PromisesObjC` | `2.4.0.5` |

#### ML Kit packages (`AdamE.MLKit.iOS.*`)

| Package | Version |
| --- | --- |
| `BarcodeScanning` | `6.0.0.3` |
| `Core` | `12.0.0.3` |

### Published on nuget.org, but older / legacy package lines

These packages are still published on nuget.org, but they are not part of the current .NET 9 / .NET 10 publishing wave:

- `AdamE.Firebase.iOS.DynamicLinks` `11.15.0`
- `AdamE.Google.iOS.GTMSessionFetcher` `4.3.0`

### Present in the repo, but not currently published on nuget.org

These projects exist in this repository, but I could not confirm current nuget.org packages for them under the `AdamE.*` package names:

- `AdamE.Google.iOS.Analytics`, `Cast`, `MobileAds`, `TagManager`, and `UserMessagingPlatform` are present in `source/`, but are not listed in the current nuget.org package set.
- `AdamE.MLKit.iOS.DigitalInkRecognition`, `FaceDetection`, `ImageLabeling`, and `ObjectDetection` are present in `source/` at `1.5.0`, but are not currently published on nuget.org.
- `AdamE.MLKit.iOS.TextRecognition`, `TextRecognition.Chinese`, `TextRecognition.Devanagari`, `TextRecognition.Japanese`, and `TextRecognition.Korean` are present in `source/` at `1.0.0.3`, but are not currently published on nuget.org.
- `AdamE.MLKit.iOS.TextRecognition.Latin` is present in `source/` at `1.4.0.3`, but is not currently published on nuget.org.
- `AdamE.MLKit.iOS.Vision` is present in `source/` at `3.0.0`, but is not currently published on nuget.org.

## Troubleshooting

### Duplicate native dependency conflicts

Google, Firebase, and ML Kit SDKs share native xcframework dependencies. Avoid mixing these packages with other independent binding libraries that ship their own copies of the same native Google/Firebase frameworks, or you can end up with duplicate symbols, linker failures, or runtime issues.

In practice, it is safest to make sure all Google/Firebase/ML Kit dependencies in an app come from one compatible binding set.

### Windows / Visual Studio restore and archive failures

If you see errors like `Could not find a part of the path...`, `Could not find or use auto-linked framework...`, or inconsistent archive behavior on Windows, start with the long-path recovery steps in the installation notes above. In most cases, the issue is not the package itself; it is the path length limit in the Windows/Visual Studio toolchain.

### Builds hanging in multi-targeted projects

If a project targets Android, Windows, or other non-Apple TFMs, do not place these package references in an unconditional `ItemGroup`. Condition them so they only apply to the iOS or Mac Catalyst targets.

### 7-zip extraction issues

If package extraction appears to be failing on Windows, see [this issue comment](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues/21#issuecomment-2172949175).

## Contributing and support

Pull requests are welcome, especially for stale bindings, dependency updates, docs improvements, and sample fixes. For anything non-trivial, please open an issue or discussion first so we can align on the approach before you spend time on build tooling or binding surface changes.

For contributor workflow details, see [CONTRIBUTING.md](CONTRIBUTING.md) and [docs/BUILDING.md](docs/BUILDING.md).

If this project saves you time, or if you want to help prioritize maintenance work, one-time and recurring sponsorship options are available from my GitHub profile.

## License

See [License.md](License.md).
