## Dependency Graph

"(~> X)" below means that the SDK requires all of the xcframeworks from X. You
should make sure to include all of the xcframeworks from X when including the
SDK.

## FirebaseAnalytics
- FBLPromises.xcframework
- FirebaseAnalytics.xcframework
- FirebaseCore.xcframework
- FirebaseCoreInternal.xcframework
- FirebaseInstallations.xcframework
- GoogleAdsOnDeviceConversion.xcframework
- GoogleAppMeasurement.xcframework
- GoogleAppMeasurementIdentitySupport.xcframework
- GoogleUtilities.xcframework
- nanopb.xcframework

## FirebaseABTesting (~> FirebaseAnalytics)
- FirebaseABTesting.xcframework

## FirebaseAILogic (~> FirebaseAnalytics)
- AppCheckCore.xcframework
- FirebaseAILogic.xcframework
- FirebaseAppCheck.xcframework
- FirebaseAppCheckInterop.xcframework
- FirebaseAuthInterop.xcframework
- FirebaseCoreExtension.xcframework
- RecaptchaInterop.xcframework

## FirebaseAppCheck (~> FirebaseAnalytics)
- AppCheckCore.xcframework
- FirebaseAppCheck.xcframework
- FirebaseAppCheckInterop.xcframework
- RecaptchaInterop.xcframework

## FirebaseAppDistribution (~> FirebaseAnalytics)
- FirebaseAppDistribution.xcframework

## FirebaseAuth (~> FirebaseAnalytics)
- FirebaseAppCheckInterop.xcframework
- FirebaseAuth.xcframework
- FirebaseAuthInterop.xcframework
- FirebaseCoreExtension.xcframework
- GTMSessionFetcher.xcframework
- RecaptchaInterop.xcframework

## FirebaseCrashlytics (~> FirebaseAnalytics)
- FirebaseCoreExtension.xcframework
- FirebaseCrashlytics.xcframework
- FirebaseRemoteConfigInterop.xcframework
- FirebaseSessions.xcframework
- GoogleDataTransport.xcframework

## FirebaseDatabase (~> FirebaseAnalytics)
- FirebaseAppCheckInterop.xcframework
- FirebaseDatabase.xcframework
- FirebaseSharedSwift.xcframework
- leveldb.xcframework

## FirebaseFirestore (~> FirebaseAnalytics)
- FirebaseAppCheckInterop.xcframework
- FirebaseCoreExtension.xcframework
- FirebaseFirestore.xcframework
- FirebaseFirestoreInternal.xcframework
- FirebaseSharedSwift.xcframework
- absl.xcframework
- grpc.xcframework
- grpcpp.xcframework
- leveldb.xcframework
- openssl_grpc.xcframework

## FirebaseFunctions (~> FirebaseAnalytics)
- FirebaseAppCheckInterop.xcframework
- FirebaseAuthInterop.xcframework
- FirebaseCoreExtension.xcframework
- FirebaseFunctions.xcframework
- FirebaseMessagingInterop.xcframework
- FirebaseSharedSwift.xcframework
- GTMSessionFetcher.xcframework

## FirebaseInAppMessaging (~> FirebaseAnalytics)
- FirebaseABTesting.xcframework
- FirebaseInAppMessaging.xcframework

## FirebaseMessaging (~> FirebaseAnalytics)
- FirebaseMessaging.xcframework
- GoogleDataTransport.xcframework

## FirebasePerformance (~> FirebaseAnalytics)
- FirebaseABTesting.xcframework
- FirebaseCoreExtension.xcframework
- FirebasePerformance.xcframework
- FirebaseRemoteConfig.xcframework
- FirebaseRemoteConfigInterop.xcframework
- FirebaseSessions.xcframework
- FirebaseSharedSwift.xcframework
- GoogleDataTransport.xcframework

## FirebaseRemoteConfig (~> FirebaseAnalytics)
- FirebaseABTesting.xcframework
- FirebaseRemoteConfig.xcframework
- FirebaseRemoteConfigInterop.xcframework
- FirebaseSharedSwift.xcframework

## FirebaseStorage (~> FirebaseAnalytics)
- FirebaseAppCheckInterop.xcframework
- FirebaseAuthInterop.xcframework
- FirebaseCoreExtension.xcframework
- FirebaseStorage.xcframework
- GTMSessionFetcher.xcframework



## Versions

The xcframeworks in this directory map to these versions of the Firebase SDKs in
CocoaPods.

          CocoaPod          | Version
----------------------------|---------
AppCheckCore                | 12.0.0
BoringSSL-GRPC              | 0.0.42
Firebase                    | 13.1.1
FirebaseABTesting           | 13.1.1
FirebaseAILogic             | 13.1.1
FirebaseAnalytics           | 13.1.1
FirebaseAppCheck            | 13.1.1
FirebaseAppCheckInterop     | 13.1.1
FirebaseAppDistribution     | 13.1.1-beta
FirebaseAuth                | 13.1.1
FirebaseAuthInterop         | 13.1.1
FirebaseCore                | 13.1.1
FirebaseCoreExtension       | 13.1.1
FirebaseCoreInternal        | 13.1.1
FirebaseCrashlytics         | 13.1.1
FirebaseDatabase            | 13.1.1
FirebaseFirestore           | 13.1.1
FirebaseFirestoreInternal   | 13.1.1
FirebaseFunctions           | 13.1.1
FirebaseInAppMessaging      | 13.1.1-beta
FirebaseInstallations       | 13.1.1
FirebaseMessaging           | 13.1.1
FirebaseMessagingInterop    | 13.1.1
FirebasePerformance         | 13.1.1
FirebaseRemoteConfig        | 13.1.1
FirebaseRemoteConfigInterop | 13.1.1
FirebaseSessions            | 13.1.1
FirebaseSharedSwift         | 13.1.1
FirebaseStorage             | 13.1.1
GTMSessionFetcher           | 5.3.1
GoogleAdsOnDeviceConversion | 3.7.0
GoogleAppMeasurement        | 13.1.1
GoogleDataTransport         | 10.1.1
GoogleUtilities             | 8.1.5
PromisesObjC                | 2.4.1
RecaptchaInterop            | 101.0.0
abseil                      | 1.20250512.2
gRPC-C++                    | 1.83.1
gRPC-Core                   | 1.83.1
leveldb-library             | 1.22.6
nanopb                      | 3.30910.0

