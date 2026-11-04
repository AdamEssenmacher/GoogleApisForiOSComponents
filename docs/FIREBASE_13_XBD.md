# Firebase 13.1.1 with Xamarin.Build.Download

This is an unpublished Firebase-only delivery experiment. Firebase 13.1.1 is the
first candidate; versions 13.1.0 and earlier do not contain the Firestore eMudhra
certificate-trust-store security fix. The preserved earlier Firebase 13 migration
is reference material, not the build provider for this experiment.

## Scope and delivery

The 16 existing Firebase products and their seven existing Google support packages
use the exact Xamarin.Build.Download `[0.11.6]` dependency. Package IDs, namespaces,
managed type names, and .NET 10 iOS/Mac Catalyst targets remain stable. No AI Logic
binding product is introduced. No Sign-In, Maps, Places, Tag Manager, Cast, UMP, or
ML Kit product is migrated here.

`source/Firebase/native-assets.json` owns the source, package versions, dependency
closure, native framework paths, linker metadata, actual binary versions, resource
slices, and tool digests. `tools/firebase13/generate.py` produces the checked-in
`build`/`buildTransitive` targets; schema validation rejects stale generated files.
Repository-owned NuGet dependencies use exact bounded versions for the verified
archive closure. Schema validation checks Cake, project references and the ownership
manifest together, including duplicate dependencies; package inspection checks the
actual nuspec ranges for every target framework.
RecaptchaInterop retains its AppCheckCore NuGet owner. Firestore native bridge
registrations use the actual `__FIR…` names while retaining managed class names.

Packages contain managed assemblies, delivery targets, readmes, and license/notice
metadata. They contain no XCFrameworks, frameworks, native bundles, static archives,
Crashlytics executables, CocoaPods output, or native source-build recipe. Binding
compilation creates managed bindings only. The producer inspects Google's prebuilt
archive; it never builds Firebase's native SDKs from source.

Restore does not acquire native SDKs. A real application/extension build uses stock
XBD to acquire Google's official release archive. Each package adds only its owned
frameworks and resources after `_XamarinBuildDownload`, before Apple interface
compilation. Outer/design-time and binding-project builds skip acquisition. The
normal application build still links and signs native code.

## Source and cache

- URL: `https://github.com/firebase/firebase-ios-sdk/releases/download/13.1.1/Firebase.zip`
- SHA-256: `eab04111b6b6ebd2bbd9264b452b2eec5be13703207f32f7cf10d9040e246b70`
- Shared XBD cache ID: `FirebaseiOSSDK-13.1.1`
- Archive bytes: 387,929,918; expanded archive bytes: 1,682,508,330.

All 23 packages declare the identical archive/cache identity. The common target
excludes an already-declared XBD item, and version checks reject conflicting
Firebase archive identities. Cold acquisition downloads the complete archive even
for a Core-only consumer. No partial ZIP provider or repository mirror is used.

On macOS the standard cache defaults to
`~/Library/Caches/XamarinBuildDownload`; XBD uses its Windows default on Windows.
Set `XamarinBuildDownloadDir` to relocate it. Consumers need network access for
initial native acquisition as well as ordinary NuGet restore. A restored consumer
with a populated native cache can build with `--no-restore` offline.

For recovery, delete the `FirebaseiOSSDK-13.1.1` directory **and** its neighboring
`FirebaseiOSSDK-13.1.1.unpacked` completion marker. Remove the corresponding `.zip`
if present when recovering from a failed/corrupt download. Rebuild online. Deleting
only extracted files can leave XBD believing extraction already completed.

Consumer cache/extraction behavior is stock XBD. The targets pass the pinned `Sha256` and the 1,800-second `ExclusiveLockTimeout`
through stock XBD metadata (verified against the installed 0.11.6 implementation).
Producer/release validation independently enforces the complete ZIP digest.
A real stock-XBD fixture proves that 0.11.6 does **not** enforce that digest after
a fresh ZIP download: its hash check only applies to an existing archive before
downloading. Passing `Sha256` metadata therefore does not guarantee consumer-time
integrity of freshly downloaded frameworks. This experiment accepts the stock
behavior and does not add a replacement downloader or extraction path. Any stronger
consumer integrity requirement must be resolved before production promotion.
These targets do
not add consumer-time rehashing of completed caches, hostile-archive protection,
atomic extraction, or warm-cache corruption detection beyond standard XBD. Crashlytics upload-symbols is independently hashed
before execution, including after copying it to a paired Mac.

## Native details and compatibility

The archive supplies static frameworks. Targets preserve per-project Apple
framework/linker settings, add `-ObjC`/`-lc++`, and request SDK Swift runtime linking.
Privacy bundles use the actual device/simulator/Catalyst bundle layout. Their
payloads are compared after normalizing Apple's `Contents/Resources` container;
SDK-specific bundle Info.plists are retained per slice. Compiled In-App Messaging
UI resources are selected per platform instead of requiring byte equality across
platforms. Duplicate product-framework payloads must match except detached outer
XCFramework signatures; the complete archive digest covers those signatures too.

Analytics/App Measurement binaries in this official 13.1.1 archive report binary
version 13.0.1; the ownership manifest records the actual binary versions rather
than falsely requiring every binary to report the umbrella release version.
GoogleAdsOnDeviceConversion is included only on iOS with advertising-ID support.
`FirebaseWithoutAdIdSupport=True` excludes both it and IdentitySupport; Catalyst
always excludes GoogleAdsOnDeviceConversion.

The shared native baseline includes AppCheckCore 12.0.0, GTMSessionFetcher 5.3.1,
PromisesObjC 2.4.1, GoogleUtilities 8.1.5 and GoogleDataTransport 10.1.1. These are
new package payloads with new package versions, including binding revisions when
upstream native versions did not change. Previously published versions are not
replaced. Non-Firebase product source/native pins remain untouched. Existing
Sign-In packages with AppCheckCore `<12` or GTMSessionFetcher `<4` bounds cannot be
represented as compatible with this Firebase closure. Resolving those combinations
is a separate product upgrade; this experiment does not relax their bounds.

## Commands

Run from the repository root on a Mac with the pinned .NET workload and Xcode:

```sh
dotnet tool restore
dotnet tool run dotnet-cake firebase13.cake --target=native-assets-validate
dotnet tool run dotnet-cake firebase13.cake --target=externals
dotnet tool run dotnet-cake firebase13.cake --target=nuget
python3 tools/firebase13/native.py packages-validate
dotnet tool install Sharpie.Bind.Tool --version 26.3.0.11 --tool-path /tmp/firebase13-sharpie
python3 tools/firebase13/audit.py --cache "$HOME/Library/Caches/XamarinBuildDownload" --sharpie-path /tmp/firebase13-sharpie/sharpie
python3 tools/firebase13/consumers.py --scope core
python3 tools/firebase13/consumers.py --scope combined
python3 tools/firebase13/consumers.py --scope combined --without-ad-id --quick
python3 tools/firebase13/consumers.py --scope all
```

`--names=Firebase.Core` selects its dependency closure; an omitted name packs all
23 packages. Packages go to `output/firebase13`, separate from legacy outputs.
`--cache` overrides the producer cache. Producer validation requires the pinned
ZIP: use `--archive` when a warm XBD cache no longer retains it. Acquisition uses
XBD, and archive verification occurs before packing. An absent official binary
archive is a release blocker; there is no CocoaPods or source-build fallback.

Firebase 13 production uses `firebase13.cake` or a Firebase selection through
`build.cake`. Existing non-Firebase commands use the isolated, locked legacy
producer. Default/mixed requests are partitioned so a Firebase 13 failure never
falls back to CocoaPods. Scoped projects reject unverified packing. See
[LEGACY_PRODUCTION.md](LEGACY_PRODUCTION.md) for commands, source isolation and the
unchanged Sign-In 10.1.0 production/validation baseline.

## Managed API updates

Firestore retains its existing managed names while correcting Objective-C class
names for its pipeline bridge types to the actual `__FIR…Bridge` exports. New 13.x
bindings cover BSON ObjectId/Timestamp, Decimal128, explicit Int32, binary subtypes,
MinKey/MaxKey/Regex values, literal-source and write pipeline stages, and the atomic
pipeline constructor. The literals, update and upsert stages expose expression-valued
`NSDictionary<NSString, ExprBridge>` constructors (including the literals array).
Their untyped native interop initializers are internal. Simulator probes exercise
these typed stage constructors, BSON round-trips, inherited
NSObject equality and pipeline construction without contacting a Firebase service.

The existing binding audit runs against all 16 Firebase products using the pinned
archive. Audit-only staged copies adapt Google's standard framework headers and
Swift compatibility headers; the XBD cache is never modified. Explicitly scoped
audit dispositions document inherited NSObject/NSCopying methods and the Storage
task protocol methods already generated by bgen. They do not remove those public
managed APIs or waive the required fallback comparison.

## Validation and publication limits

The `Firebase 13 XBD experiment` workflow runs schema/hostile-archive/unit checks on
PRs. Same-repository PR branches also run the Mac producer and
consumer matrix; fork PRs cannot schedule the self-hosted runner. Manual dispatch
is available once the workflow exists on the default branch. The workflow has
read-only repository permissions and no publication job. A failing `pod` is placed
first on PATH. Consumer tests isolate NuGet caches, verify restore leaves XBD alone,
perform native acquisition on the first build, block HTTP(S) for subsequent builds,
verify warm-cache bytes, test direct/transitive references and supported platforms,
check native/resource duplication, and execute a secret-free simulator load probe.

The experiment keeps publication disabled in both its manifest and the existing
Firebase release configuration. These local candidates are not sealed publication
artifacts. Promotion to the approval-gated publisher must update the existing
packed-native assumptions and seal/verify the XBD-delivered candidate evidence.
Do not bypass existing release checks or represent the experiment as released.
The current pinned .NET Catalyst SDK requires a 17.0 deployment floor for test
applications; binding TFMs and declared package minimums are preserved.
Local validation evidence is summarized in `FIREBASE_13_XBD_VALIDATION.md`.
Non-Firebase package production uses the isolated baseline described in
[LEGACY_PRODUCTION.md](LEGACY_PRODUCTION.md). Sign-In remains at 10.1.0 without a
binding revision; its existing consumer dependency bounds remain unchanged.
Real Pair-to-Mac execution and credentialed Firebase service flows are not proved
by the simulator load probe. Windows path/remote-task behavior still needs review.

## Unresolved licensing questions

The NuGets distribute bindings/targets and obtain native binaries directly from
Google, reducing the native redistribution surface. This does not establish legal
clearance. Before publication, confirm the terms governing Analytics and Google
App Measurement acquisition/use through these targets, the appropriate package
license/acceptance wording, and any downstream notice obligations. Preserve the
separate Analytics/App Measurement redistribution review; it has not been closed.
Google's archive NOTICES are retained verbatim as `UPSTREAM-NOTICES.txt` in packages.
No SDK binary or executable is mirrored or redistributed in the NuGets.

References:
- [Firebase 13.1.1 release](https://github.com/firebase/firebase-ios-sdk/releases/tag/13.1.1)
- [Google eMudhra remediation guidance](https://firebase.google.com/support/guides/compromised-emudhra-intermediate-ca)
- [Tagged upstream package manifest](https://github.com/firebase/firebase-ios-sdk/blob/13.1.1/Package.swift)
