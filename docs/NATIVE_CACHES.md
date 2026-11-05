# Native build caches

The CocoaPods/Xcode archive helper and the direct native archive downloader verify
cache inputs and outputs before reuse. Existing directories without completion
records are rebuilt or downloaded once; deleting a record is also a way to force
that operation without clearing unrelated native outputs.

## Xcode archives

`BuildXcodeXcframework` records completion beside each platform archive as
`<platform>.xcarchive.cache`. Its identity includes the requested pod definitions,
Podfile, both CocoaPods lockfiles, generated Xcode project and target-support files,
Xcode version, developer-directory override, target, platform, architectures and
build settings. Unlocked builds and explicit local-path pod sources always rebuild.

Before rebuilding, the helper removes the old completion record and archive. A
successful build must contain the requested framework's Info.plist and binary.
The record then binds the input identity to an inventory of all archive files,
including resources. Each scheme builds and records its own archive; it does not
populate another scheme's cache by copying dependency frameworks.

## Direct downloads

`DownloadAndExtract` records completion in `externals/<SDK>.download-cache`. Its
identity includes the SDK version, archive key, full URL, expected SHA-256,
extraction root and required outputs. GoogleAppMeasurement requires both its
measurement and IdentitySupport frameworks.

A miss removes the old completion record before downloading. The helper verifies
the archive hash, extracts it, and compares every copied file against the verified
extraction before writing a record. This comparison includes framework slices and
embedded resource bundles. A failed extraction, interrupted copy, or incomplete
copy cannot leave a valid record.

Both cache hits rehash the recorded outputs. A missing directory, deleted binary
or resource, empty output, or changed file invalidates the cache. This adds local
file reads to a hit in exchange for detecting incomplete and modified outputs.
Completion records are written through a temporary file and renamed only after
success.

## Regression coverage and release policy

`NativeCacheTests` compiles the production Cake cache helpers and executes them
against temporary fixtures. Only Cake filesystem/process aliases and external
build/download operations are simulated. The tests require no Apple SDKs, vendor
network access or application credentials, and run with the managed audit suite:

```sh
dotnet test scripts/FirebaseBindingAudit.Tests/FirebaseBindingAudit.Tests.csproj
```

These shared helper changes affect the package fingerprints of both the Firebase
and Sign-In closures. The cache correction prepares Firebase and
GoogleAppMeasurement 12.19.3.1, Sign-In 10.1.0.1, and revisions of all seven Firebase
support packages (four of which are shared with Sign-In). Native SDK pins remain
unchanged. The checked-in initial release baselines and version guards are unchanged.
Publish the Firebase revision before the Sign-In revision so the latter can reuse
its four shared support packages, following the normal approved release workflow.
