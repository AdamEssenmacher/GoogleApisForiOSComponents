# Firebase Runtime Failure Backlog

This backlog tracks binding drifts that are concrete candidates for the runtime-failure remediation loop.

## Ready

| Case id | Target/member | Audit finding reference | Expected runtime failure mechanism | Proof status | Fix PR | Merged commit |
| --- | --- | --- | --- | --- | --- | --- |
| `abtesting-validaterunningexperiments` | `Firebase.ABTesting.ExperimentController.ValidateRunningExperiments` | `output/firebase-binding-audit/report.json` -> `ABTesting` -> `ValidateRunningExperiments` (`signature-drift`) | The old binding exports `validateRunningExperimentsForServiceOrigin:runningExperimentPayloads:` as `NSObject[]` instead of `ABTExperimentPayload[]`, so callers can send arbitrary objects and native ABTesting immediately raises `ObjCRuntime.ObjCException` when it invokes `ABTExperimentPayload` selectors on those objects. | Proved locally on the unfixed binding. Evidence artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-abtesting-validaterunningexperiments-failure.json`. Regression artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-abtesting-validaterunningexperiments-success.json`. | - | - |

## Investigating

No additional runtime-failure candidates are currently promoted.

## Done

| Case id | Target/member | Audit finding reference | Runtime failure mechanism | Proof status | Fix PR | Merged commit |
| --- | --- | --- | --- | --- | --- | --- |
| `abtesting-activateexperiment` | `Firebase.ABTesting.ExperimentController.ActivateExperiment` | `output/firebase-binding-audit/report.json` -> `ABTesting` -> `ActivateExperiment` (`signature-drift`) | The old binding exported `activateExperiment:forServiceOrigin:` as `NSObject` instead of `ABTExperimentPayload`, so callers could send an arbitrary object and native ABTesting immediately raised `ObjCRuntime.ObjCException` when it invoked `ABTExperimentPayload` selectors on that object. | Proved and fixed. Evidence artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-abtesting-activateexperiment-failure.json`. Regression artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-abtesting-activateexperiment-success.json`. | [#114](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/114) | `f77b1c57eff15cf6c1b5ca0df47d8195b46f0f40` |
| `cloudfunctions-usefunctionsemulatororigin` | `Firebase.CloudFunctions.CloudFunctions.UseFunctionsEmulatorOrigin` | Manual runtime candidate from the current binding surface and native `FirebaseFunctions` Swift header | The binding exported `useFunctionsEmulatorOrigin:` even though the current framework only exposes `useEmulatorWithHost:port:`. Calling the stale selector raised `ObjCRuntime.ObjCException` with an unrecognized-selector native exception. | Proved and fixed. Evidence artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-cloudfunctions-usefunctionsemulatororigin-failure.json`. Regression artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-cloudfunctions-usefunctionsemulatororigin-success.json`. | [#113](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/113) | `b6a6c82c74e0cf3645edb9dc5519d4a628f7ed36` |
| `cloudfirestore-getquerynamed` | `Firebase.CloudFirestore.Firestore.GetQueryNamed` | `output/firebase-binding-audit/report.json` -> `CloudFirestore` -> `GetQueryNamed` (`signature-drift`) | The old binding exported `getQueryNamed:completion:` as `NSInputStream` instead of `NSString`, so native Firestore received `__NSCFInputStream` and threw an Objective-C exception when it treated the argument like a string. | Proved and fixed. Evidence artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-cloudfirestore-getquerynamed-failure.json`. Regression artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-cloudfirestore-getquerynamed-success.json`. | [#112](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/112) | `3077a538a892de0db3350f0a459bf8620729f4db` |
