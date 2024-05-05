# Overview
This is a **community-supported** fork of the GoogleApisForiOSComponents repo maintained initially by Xamarin. Microsoft just kind of stopped maintaining these bindings libraries, which were published under the package prefixes Xamarin.Firebase.iOS.* and Xamarin.Google.iOS.*

Apple's new privacy manifest requirements, which went into effect 1 May 2024, impacted all Xamarin.Firebase.iOS.* packages. App Store compliance requires native SDK versions 10.24+, while the Xamarin packages are stuck on 8.10.

Less than two months before this privacy deadline, Microsoft's response to impacted developers was that they "don't have any updates to share about producing new versions of those packages", and suggested that developers re-write their own bindings using this [slim binding demo project](https://github.com/Redth/DotNet.Platform.SlimBindings) as a guide.

As an alternative solution for the community, I created this fork and upgraded a subset of the affected packages to latest native SDK versions and published them under a new package prefix: **AdamE.Firebase.iOS.***

If these packages are important to you, please consider **[sponsoring the project](https://github.com/sponsors/AdamEssenmacher)**  and/or contributing to this fork.

# Firebase Apple SDK and Nuget Package Releases

Here's a table that shows in which Nuget package version is located each sdk of Firebase:


| SDK Name                  |  SDK Version |   Nuget Package Version   |
| ------------------------------- | :---------------: | :----------------: |
| Firebase A/B Testing            |    **10.24.0**    | ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.ABTesting) |
| Firebase Analytics              |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Analytics)     |
| Firebase Auth                   |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Auth)    |
| Firebase Firestore        |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.CloudFirestore)     |
| Firebase Functions        |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.CloudFunctions)     |
| Firebase Messaging        |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.CloudMessaging)    |
| Firebase Core                   |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Core) |
| Firebase Crashlytics            |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Crashlytics)     |
| Firebase Database               |    **10.24.0**    | ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Database) |
| Firebase Dynamic Links          |    **8.10.0**     | ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.DynamicLinks) |
| Firebase Installations          |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Installations)     |
| Firebase RemoteConfig           |    **8.10.0**     | ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.RemoteConfig) |
| Firebase Storage                |    **10.24.0**    |    ![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Storage)    |

> The alpha versions are pending to be tested.

### Firebase Apple SDKs Pending to be updated


| SDK Name                  |  SDK Version |   Nuget Package Version   |
| ------------------------------- | :---------------: | :----------------: |
| Firebase AdMob                  |    **8.10.0**     |     **8.10.0**     |
| Firebase In App Messaging       |    **8.10.0**     |     **8.10.0**     |
| Firebase Performance Monitoring |    **8.10.0**     |     **8.10.0**     |
| Google User Messaging Platform  |    **1.1.0.0**    |     **8.10.0**     |
| Google Cast                     |    **4.7.0.0**    |     **8.10.0**     |
| Google Sign-In                  |    **5.0.2.2**    |     **8.10.0**     |
| Google Tag Manager              |    **7.4.0.0**    |     **8.10.0**     |


# Fork notes:

## Firebase Analytics

On .NET 6+, FirebaseAnalytics 10.17+ requires this target be added to your app's .csproj:

```xml
<!-- Target needed until LinkWithSwiftSystemLibraries makes it into the SDK: https://github.com/xamarin/xamarin-macios/pull/20463 -->
<Target Name="LinkWithSwift" DependsOnTargets="_ParseBundlerArguments;_DetectSdkLocations" BeforeTargets="_LinkNativeExecutable">
    <PropertyGroup>
        <_SwiftPlatform Condition="$(RuntimeIdentifier.StartsWith('iossimulator-'))">iphonesimulator</_SwiftPlatform>
        <_SwiftPlatform Condition="$(RuntimeIdentifier.StartsWith('ios-'))">iphoneos</_SwiftPlatform>
    </PropertyGroup>
    <ItemGroup>
        <_CustomLinkFlags Include="-L" />
        <_CustomLinkFlags Include="/usr/lib/swift" />
        <_CustomLinkFlags Include="-L" />
        <_CustomLinkFlags Include="$(_SdkDevPath)/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/$(_SwiftPlatform)" />
        <_CustomLinkFlags Include="-Wl,-rpath" />
        <_CustomLinkFlags Include="-Wl,/usr/lib/swift" />
    </ItemGroup>
</Target>
```

Additionally, you may be required to include also the **Firebase Installations** package reference.

On *Legacy Xamarin.iOS*, FirebaseAnalytics 10.17+ requires that additional mtouch arguments be added to the iOS project:

For iPhone: 
```
--gcc_flags -L/Applications/Xcode.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/iphoneos
```

For simulator: 
```
--gcc_flags -L/Applications/Xcode.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/iphonesimulator/
```

You may need to adjust the XCode location if it's not default.

## Firebase Crashlytics

On .NET8, you must set the following property. Note that this will increase your app's size.

```xml
<!--https://github.com/xamarin/GoogleApisForiOSComponents/issues/643#issuecomment-1920970044-->
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0-ios'">
    <_ExportSymbolsExplicitly>false</_ExportSymbolsExplicitly>
</PropertyGroup>
```

## Building

### Prerequisites

Before building the libraries and samples in this repository, you will need to install [.NET Core][30] and the [Cake .NET Core Tool][32]:

Currently requires a version of Cake less than 1.0 (due to dependencies).

```sh
dotnet tool install -g cake.tool --version 0.38.5
```

When building on macOS, you may also need to install [CocoaPods][31]:

```sh
# Homebrew
brew install cocoapods

# Ruby Gems
gem install cocoapods
```

### Compiling

You can either build all the libraries and samples in the repository from the root:

```sh
dotnet cake
```

Or, you can specify the components and its dependencies to be build by using the `--names=Key1,Key2,...`:

```sh
// Firebase keys
Firebase.ABTesting
Firebase.AdMob
Firebase.Analytics
Firebase.Auth
Firebase.CloudFirestore
Firebase.CloudFunctions
Firebase.CloudMessaging
Firebase.Core
Firebase.Crashlytics
Firebase.Database
Firebase.DynamicLinks
Firebase.InAppMessaging
Firebase.Installations
Firebase.PerformanceMonitoring
Firebase.RemoteConfig
Firebase.Storage

// Google keys
Google.Analytics
Google.Cast
Google.Maps
Google.MobileAds
Google.UserMessagingPlatform
Google.Places
Google.SignIn
Google.TagManager

// MLKit keys
MLKit.BarcodeScanning
MLKit.Core
MLKit.DigitalInkRecognition
MLKit.FaceDetection
MLKit.ImageLabeling
MLKit.ObjectDetection
MLKit.TextRecognition
MLKit.TextRecognition.Chinese
MLKit.TextRecognition.Devanagari
MLKit.TextRecognition.Japanese
MLKit.TextRecognition.Korean
MLKit.TextRecognition.Latin
MLKit.Vision
```

The following targets can be specified using the `--target=<target-name>`:

- `libs` builds the class library bindings (depends on `externals`)
- `externals` downloads and builds the external dependencies
- `samples` builds all of the samples (depends on `libs`)
- `nuget` builds the nuget packages (depends on `libs`)
- `clean` cleans up everything

### Working in Visual Studio

Before the `.sln` files will compile in the IDEs, the external dependencies need to be downloaded. This can be done by running the `externals` target:

```sh
dotnet cake --target=externals
```

After the externals are downloaded and built, the `.sln` files should compile in your IDE.

## License

The license for this repository is specified in
[License.md](License.md)
