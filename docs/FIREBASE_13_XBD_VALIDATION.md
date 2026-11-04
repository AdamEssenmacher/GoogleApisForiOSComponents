# Firebase 13.1.1 XBD experiment: local validation

This records feature-branch validation on macOS with Xcode 27.1 and the repository's
.NET 10 workloads. It is not a sealed release manifest or a claim of GitHub CI
completion. No packages have been published by this work.

## Completed checks

- The official Firebase 13.1.1 ZIP matches the committed SHA-256 and the digest
  supplied by its GitHub release asset. Producer validation checks archive paths
  and links, METADATA, tagged Package.swift, native versions, supported slices,
  complete framework ownership, duplicate framework payloads, privacy/UI resources,
  notices and Crashlytics tool hashes. Independently signed outer XCFramework
  signatures are pinned by the archive hash; duplicate comparison excludes those
  detached signatures and compares the actual framework payloads.
- All 23 scoped NuGets pack for iOS and Mac Catalyst. Package inspection requires
  the exact public XBD 0.11.6 dependency and both build import paths, and rejects
  embedded frameworks, bundles, executables or native download remnants.
- All 16 Firebase product binding audits pass. Swift compatibility headers require
  explicit public-header input for the Storage fallback. Narrow audit dispositions
  identify APIs already supplied through NSObject/NSCopying or generated protocol
  implementation. They are supported by generated bindings and simulator probes.
- All 177 explicit Objective-C class registrations match exports in the 40
  canonical iOS libraries. Actual Mach-O architectures match metadata for all 119
  iOS/device, simulator and Catalyst slices, independently of Info.plist claims.
- The combined Analytics/Auth/Firestore closure passes direct and
  library-transitive consumer builds for iOS simulator, unsigned iOS device and
  Mac Catalyst. Core separately passes a fresh-cache simulator probe. Restore
  leaves the native cache untouched; the first application
  build populates it. Subsequent builds with HTTP(S) proxied to a closed local port
  reuse unchanged cache bytes. The first offline pass performs a Rebuild.
- All 16 Firebase products together pass simulator build/load and offline-cache
  checks. Analytics without advertising-ID support passes separately. Consumer
  inspection checks native ownership, logical bundle uniqueness and privacy
  resource hashes, including the advertising-ID/platform exclusions.
- Secret-free simulator probes verify Firebase 13.1.1 load, representative native
  class availability, Firestore BSON value round-trips and pipeline construction.
  Crashlytics upload-symbols executes its help command from the downloaded archive.
- Eleven new Python tests cover manifest/delivery and hostile-archive cases; the
  existing 72 release-tool tests and 125 binding-audit tests pass.
- Local producer and consumer paths run with a failing pod sentinel. Consumer
  probes assert that it was never invoked. The dedicated Firebase entry point
  never loads CocoaPods code.

## Limits and promotion work

- These are local experimental artifacts. The new read-only workflow can repeat
  schema checks and the Mac matrix, but this record does not assert a workflow run.
- The older Firebase publisher assumes packed native payloads. Publication remains
  disabled. Before release, adapt its candidate validation, exact artifact handoff
  and evidence sealing to this delivery model and rerun the complete matrix for
  the exact release commit. Existing Sign-In release behavior is not being migrated.
- The all-product and no-ad-ID checks above are simulator checks; the committed
  harness supports the broader matrix but it has not all been executed for those
  two shapes locally. The full platform matrix was run for the combined
  Analytics/Auth/Firestore closure.
- Non-Firebase producer isolation is covered by the locked-baseline tests and a
  separate Sign-In production/consumer CI lane; see LEGACY_PRODUCTION.md.
- Real Windows/Pair-to-Mac execution, authenticated Firebase service operations and
  the compatibility of existing non-Firebase packages with the new shared native
  baseline are not established. Current Sign-In dependency bounds are incompatible.
- Ten Crashlytics tests verify local/remote upload integrity and real stock-XBD
  ZIP caching with a bounded local archive. This replaces tests of the removed raw
  script download. The ZIP fixture confirms that a fresh download with deliberately
  mismatched digest metadata succeeds in XBD 0.11.6; it is explicitly **not** a
  consumer-time digest guarantee. Producer verification still rejects mismatches.
- This accepts stock XBD cache/extraction semantics. Producer archive safety and
  digest tests do not establish additional consumer-time protections beyond XBD.
- Analytics/App Measurement licensing, package acceptance wording and downstream
  notice obligations remain open before publication; direct acquisition is not a
  legal conclusion. See FIREBASE_13_XBD.md.
