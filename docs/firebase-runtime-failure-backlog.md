# Firebase Runtime Failure Backlog

This backlog tracks binding drifts that are concrete candidates for the runtime-failure remediation loop.

## Ready

| Case id | Target/member | Audit finding reference | Expected runtime failure mechanism | Proof status | Fix PR | Merged commit |
| --- | --- | --- | --- | --- | --- | --- |
| `cloudfunctions-usefunctionsemulatororigin` | `Firebase.CloudFunctions.CloudFunctions.UseFunctionsEmulatorOrigin` | Manual runtime candidate from the current binding surface and native `FirebaseFunctions` Swift header | The binding still exports `useFunctionsEmulatorOrigin:` even though the current framework only exposes `useEmulatorWithHost:port:`. Calling the stale selector raises `ObjCRuntime.ObjCException` with an unrecognized-selector native exception. | Proved locally on the unfixed binding. Evidence: `ObjCRuntime.ObjCException`, `NSInvalidArgumentException`, selector `-[FIRFunctions useFunctionsEmulatorOrigin:]`, artifact `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-cloudfunctions-usefunctionsemulatororigin-failure.json`, log `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-sim-cloudfunctions-usefunctionsemulatororigin-failure.log`. | Pending | - |

## Investigating

No additional runtime-failure candidates are currently promoted.

## Done

| Case id | Target/member | Audit finding reference | Runtime failure mechanism | Proof status | Fix PR | Merged commit |
| --- | --- | --- | --- | --- | --- | --- |
| `cloudfirestore-getquerynamed` | `Firebase.CloudFirestore.Firestore.GetQueryNamed` | `output/firebase-binding-audit/report.json` -> `CloudFirestore` -> `GetQueryNamed` (`signature-drift`) | The old binding exported `getQueryNamed:completion:` as `NSInputStream` instead of `NSString`, so native Firestore received `__NSCFInputStream` and threw an Objective-C exception when it treated the argument like a string. | Proved and fixed. Evidence artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-cloudfirestore-getquerynamed-failure.json`. Regression artifact: `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-cloudfirestore-getquerynamed-success.json`. | [#112](https://github.com/AdamEssenmacher/GoogleApisForiOSComponents/pull/112) | `3077a538a892de0db3350f0a459bf8620729f4db` |
