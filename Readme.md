# Overview
This is a **community-supported** fork of the GoogleApisForiOSComponents repo maintained initially by Xamarin. Microsoft just kind of stopped maintaining these bindings libraries, which were published under the package prefixes Xamarin.Firebase.iOS.* and Xamarin.Google.iOS.*

This was a really unfortunate decision from Microsoft. Having these extremely common native iOS dependencies published by a central, trusted source allowed plugin developers for things like MLKit, Google Sign-In, and Firebase to coexist. Now, such libraries will either have to include native dependencies directly (which will make them incompatible with each-other) or accept a new community standard. I have published updated packages under the prefix AdamE.Firebase.iOS.* which have so far been adopted by [Plugin.Firebase](https://github.com/TobiasBuchholz/Plugin.Firebase) and [BarcodeScanner.Mobile.Maui](https://github.com/JimmyPun610/BarcodeScanner.Mobile)

Microsoft's current recommendation to iOS.NET developers is that they should write their own bindings using this [slim binding demo project](https://github.com/Redth/DotNet.Platform.SlimBindings) as a guide. However, developers following this approach can find their 'slim bindings' to be incompatible with any other published libraries with dependency conflicts, such as Plugin.Firebase.

If these packages are important to you, please consider **[sponsoring the project](https://github.com/sponsors/AdamEssenmacher)**  and/or contributing to this fork. I'm a solo developer and considerably under-sponsored for this work.

# (Important!) Installation Instructions and Usage Notes
Failure to read and follow these instructions will result in build failures and/or runtime errors.

At the moment, you may need to explicitly reference AdamE.Firebase.iOS.Installations even if you don't "need" it. I'll try to get this fixed in future releases.

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

It's important to understand that the native Crashlytics SDK doesn't know anything about .NET, so its not going to be as useful as a complete error/crash reporting mechanism. See this [issue in Plugin.Firebase](https://github.com/TobiasBuchholz/Plugin.Firebase/issues/291)

On .NET8, you must set the following property. Note that this will increase your app's size.

```xml
<!--https://github.com/xamarin/GoogleApisForiOSComponents/issues/643#issuecomment-1920970044-->
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
    <_ExportSymbolsExplicitly>false</_ExportSymbolsExplicitly>
</PropertyGroup>
```

## Windows + Visual Studio (long path issue)
If you're using Mac, you can skip this section. If you're using Windows and/or Visual Studio, you need to be aware that these packages are impacted by a long-path filename issue that will cause errors when restoring packages and archiving builds.

While this is fundamentally an issue with Visual Studio, I think this project could be updated to work around it by statically linking XCFramework dependencies into intermediate 'shim' XCFrameworks. However, I do not have the personal bandwidth or sponsorship to fully explore this. Unless something changes, you should not expect this issue to be resolved until Visual Studio properly supports long file paths.

### Long-path issue root cause
In the iOS/Catalyst world, code libraries are typically shipped as XCFrameworks. An XCFramework is a packaging format that bundles libraries for multiple platforms (e.g. iOS, macOS) and architectures into a single artifact. To build these AdamE.* artifacts, several XCFrameworks (Firebase and Google dependencies) are assembled and packed into the Nuget packages. This is how iOS.NET and Nuget are _designed_ to work.

XCFrameworks follow a rigid, opinionated naming structure. For example, a framework's binary falls under a path like `[XCFramework Name]\[Arch]\[Framework Name]\[Binary Name]`. Header files fall under a path like `[XCFramework Name]\[Arch]\[Framework Name]\Headers\[Framework]-*.h`

In practical application, this results in long file paths like: `FirebaseRemoteConfigInterop.xcframework\ios-arm64_x86_64-simulator\FirebaseRemoteConfigInterop.framework\Headers\FirebaseRemoteConfigInterop-umbrella.h`

Nuget itself also follows an opinionated naming structure. Packages downloaded to your local machine's cache are unpacked under paths like `[Cache Path]\[Package Name]\[Version]`.

Combining both, we get full file paths like `C:\n\adame.firebase.ios.core\10.24.0\lib\net6.0-ios16.1\Firebase.Core.resources\FirebaseCoreInternal.xcframework\ios-arm64_x86_64-simulator\FirebaseCoreInternal.framework\Modules\FirebaseCoreInternal.swiftmodule\arm64-apple-ios-simulator.private.swiftinterface`--260 characters long! (this is just one example)

### Long-path issue workarounds

Windows has trouble with path lengths > 256, but a registry edit can be made that helps
`New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -Value 1 -PropertyType DWORD -Force`

Even with that Visual Studio can still be very temperamental with long file paths. It's a limitation baked into the core of the IDE. It is still possible to build iOS apps using Visual Studio, but you *must restore packages from the CLI first:*

1. Close Visual Studio
2. Make sure you didn't skip step 1.
3. Clear your local NuGet cache AND your local XamarinBuildDownload cache:
   - In your Windows Terminal (Powershell):
     ```ps
     dotnet nuget locals all -c # Clear all Nuget cache
     cd $env:localappdata # Go to your AppData\Local folder
     Get-ChildItem -Filter "*Xamarin*" # Check for your XamarinBuildDownloadCache folder
     rm -Force -Recurse XamarinBuildDownloadCache # Delete that folder
     Get-ChildItem -Filter "*Xamarin*" # Confirm that it has been deleted
     ```
   - In your Mac Terminal:
     ```zsh
     setopt interactive_comments
     # Now comment is enabled
     cd ~/Library/Caches # Go to your Caches folder
     ls -d *Xamarin* # Check for your XamarinBuildDownload folder
     rm -rf XamarinBuildDownload # Delete that folder
     ls -d *Xamarin* # Confirm that it has been deleted
     ```
4. Make sure you didn't skip step 3.
5. Delete your project `bin` and `obj` folders.
6. Make sure you didn't skip step 5.
7. Run a `dotnet restore` for your project/solution from the command line.
8. Build your project/solution successfully from the command line.
9. Make sure you didn't skip step 8.

Now you should be able to build and debug your iOS app in Visual Studio. It will probably still choke during archiving though, so either do that step directly on a mac or try it on the CLI as well.

# Firebase Apple SDK and Nuget Package Releases

Here's a table that shows in which Nuget package version is located each sdk of Firebase:


| SDK Name                        | SDK Version |   Nuget Package Version   |
| ------------------------------- |:-----------:| :----------------: |
| Firebase A/B Testing            | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.ABTesting)](https://nuget.org/packages/AdamE.Firebase.iOS.ABTesting) |
| Firebase Analytics              | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Analytics)](https://nuget.org/packages/AdamE.Firebase.iOS.Analytics) |
| Firebase Auth                   | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Auth)](https://nuget.org/packages/AdamE.Firebase.iOS.Auth) |
| Firebase Firestore              | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.CloudFirestore)](https://nuget.org/packages/AdamE.Firebase.iOS.CloudFirestore) |
| Firebase Functions              | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.CloudFunctions)](https://nuget.org/packages/AdamE.Firebase.iOS.CloudFunctions) |
| Firebase Messaging              | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.CloudMessaging)](https://nuget.org/packages/AdamE.Firebase.iOS.CloudMessaging) |
| Firebase Core                   | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Core)](https://nuget.org/packages/AdamE.Firebase.iOS.Core) |
| Firebase Crashlytics            | **10.28.1** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Crashlytics)](https://nuget.org/packages/AdamE.Firebase.iOS.Crashlytics)  
| Firebase Database               | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Database)](https://nuget.org/packages/AdamE.Firebase.iOS.Database) |
| Firebase Dynamic Links          | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.DynamicLinks)](https://nuget.org/packages/AdamE.Firebase.iOS.DynamicLinks) |
| Firebase Installations          | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Installations)](https://nuget.org/packages/AdamE.Firebase.iOS.Installations) |
| Firebase RemoteConfig           | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.RemoteConfig)](https://nuget.org/packages/AdamE.Firebase.iOS.RemoteConfig) |
| Firebase Storage                | **10.28.0** | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Firebase.iOS.Storage)](https://nuget.org/packages/AdamE.Firebase.iOS.Storage) |
| Google Maps                     |  **9.1.1**  | [![NuGet Version](https://img.shields.io/nuget/v/AdamE.Google.iOS.Maps)](https://www.nuget.org/packages/AdamE.Google.iOS.Maps) |


### SDKs net yet updates from where Microsoft left them:

| SDK Name                  |  SDK Version |   Nuget Package Version   |
| ------------------------------- | :---------------: | :----------------: |
| Firebase AdMob                  |    **8.10.0**     |     **8.10.0**     |
| Firebase In App Messaging       |    **8.10.0**     |     **8.10.0**     |
| Firebase Performance Monitoring |    **8.10.0**     |     **8.10.0**     |
| Google User Messaging Platform  |    **1.1.0.0**    |     **8.10.0**     |
| Google Cast                     |    **4.7.0.0**    |     **8.10.0**     |
| Google Sign-In                  |    **5.0.2.2**    |     **8.10.0**     |
| Google Tag Manager              |    **7.4.0.0**    |     **8.10.0**     |


# Troubleshooting

## Incompatibility with other iOS.NET libraries that ship Google / Firebase native SDKs
Google, Firebase, and MLKit native SDKs can have some shared XCFramework dependencies. Take [GTMSessionFetcher](https://github.com/google/gtm-session-fetcher) as an example. It is a shared dependency of both Google Sign-In and Firebase.Core.

In native iOS development, this dependency would be resolved through a dependency manager (Cocoapods, SPM), but we can't use these native dependency managers in iOS.NET!

Microsoft's official recommendation to iOS.NET developers needing access to native SDKs is to build their own binding libraries using this ['slim binding' demo project](https://github.com/Redth/DotNet.Platform.SlimBindings) as a guide. This dependency situation for common mobile SDKs causes a major issue with this approach. For example, if a developer were to include their own 'slim binding' project in an iOS.NET solution for Google Sign-In, they would end up statically linking GTMSessionFetcher. Since AdamE.Firebase.iOS.Core _also_ includes GTMSessionFetcher, all AdamE.iOS.* packages would no longer be compatible due to the dependency conflict.

To provide a real-world example, [Plugin.Firebase](https://github.com/TobiasBuchholz/Plugin.Firebase) migrated off of the abandoned Xamarin.Firebase.iOS.* packages to use AdamE.Firebase.iOS.* This made it incompatible with the popular [BarcodeScanner.Mobile.Maui](https://github.com/JimmyPun610/BarcodeScanner.Mobile) package until the maintainer also migrated to use AdamE.Firebase.iOS.* dependencies.

As another example, the Shiny library includes modular support for push notifications using Firebase Cloud Messaging. Versions <4 used Xamarin.Firebase.iOS.* and so are incompatible with AdamE.Firebase.iOS.* Version 4 will include its own slim bindings for Firebase, which makes it incompatible with AdamE.Firebase.iOS.*

## "Could not find a part of the path..."
It's the long-path issue described above.

## Build or archiving problems on Windows / Visual Studio
It's the long-path issue described above.

## The build is hanging
Most likely, you've added these AdamE.* packages to a .csproj that targets other frameworks like:
```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>net8.0-android;net8.0-ios;</TargetFrameworks>
    </PropertyGroup>
...
    <ItemGroup>
        <PackageReference Include="AdamE.Firebase.iOS.Core" Version="10.24.0.2" />
    </ItemGroup>
</Project>
```
Instead, you should do:
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0-ios'">
    <PackageReference Include="AdamE.Firebase.iOS.Core" Version="10.24.0.2" />
</ItemGroup>
```

## 7-zip issues
Try https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/issues/21#issuecomment-2172949175

## Could not find or use auto-linked framework...
If you're on Windows, it's most likely the long-path issue described above. Clear your XamarinBuildDownload cache and try again.

## Firebase Installations
Make sure you've referenced AdamE.Firebase.iOS.Installations

# Building the Nuget packages
The following steps are not required for consuming the published Nugets. These are only relevant if you've pulled this repo and are building it locally.

## Prerequisites

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

## Compiling

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

## Working in Visual Studio

Before the `.sln` files will compile in the IDEs, the external dependencies need to be downloaded. This can be done by running the `externals` target:

```sh
dotnet cake --target=externals
```

After the externals are downloaded and built, the `.sln` files should compile in your IDE.

## License

The license for this repository is specified in
[License.md](License.md)
