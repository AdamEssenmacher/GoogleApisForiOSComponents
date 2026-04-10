# Firebase Runtime Failure Backlog

This backlog tracks binding drifts that are concrete candidates for the runtime-failure remediation loop.

## Ready

| Case id | Target/member | Audit finding reference | Expected runtime failure mechanism | Proof status | Fix PR | Merged commit |
| --- | --- | --- | --- | --- | --- | --- |
| `cloudfirestore-getquerynamed` | `Firebase.CloudFirestore.Firestore.GetQueryNamed` | `output/firebase-binding-audit/report.json` -> `CloudFirestore` -> `GetQueryNamed` (`signature-drift`) | The binding currently exports `getQueryNamed:completion:` as `NSInputStream` instead of `NSString`, so native Firestore receives `__NSCFInputStream` and throws an Objective-C exception when it treats the argument like a string. | Proved locally on the unfixed binding. Evidence: `ObjCRuntime.ObjCException`, `NSInvalidArgumentException`, selector `-[__NSCFInputStream length]`, artifact `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-result-cloudfirestore-getquerynamed-failure.json`, log `tests/E2E/Firebase.Foundation/artifacts/firebase-foundation-sim-cloudfirestore-getquerynamed-failure.log`. | Pending | - |

## Investigating

No additional runtime-failure candidates are currently promoted.

## Done

None yet.
