# CI validation scope

Automatic pull-request validation uses two workflows:

- `Firebase CI and release` plans and validates the Firebase and Google Sign-In
  channels, including package inspection and consumer/runtime checks. The
  required `Firebase validation` check and approval-gated main publication remain
  part of that workflow.
- `Google Foundation Validation` runs a Linux planner and schedules the Maps and
  Places Mac jobs independently when their selected inputs change. Those jobs
  retain build/pack, package structure, direct/transitive consumers, offline
  builds, and simulator tests.

Both workflows cancel superseded runs for the same pull request. Main-branch and
explicitly dispatched runs have unique concurrency groups and are never canceled
by a newer pull-request revision.

The `CI` workflow is an explicit `workflow_dispatch` component build. Its `names`
input and package artifact remain available; it no longer automatically repeats
Google Sign-In build/pack already covered by the release workflow.

## Maps and Places planning

The planner compares the current revision with the pull request's base SHA (or
previous main SHA). Cake exports each selected dependency closure. The comparison
includes native pins/download coordinates, package/project inputs, packed files,
SDK/tool pins and normalized shared build logic. Unselected component declarations
and assignments do not trigger a native foundation build. Selected transitive
packages and shared changes do.

Manual runs always validate both components. Changes to the planner, its workflow
or foundation test tooling also validate both. Unknown Cake helpers are treated
conservatively. Invalid metadata, unsupported dynamic project inputs and planning
errors fail the plan; they never produce a successful skip.

The additional component normalization is used only for CI relevance. Existing
publication fingerprints and package-version checks retain their original path.

Run the planner and fingerprint regressions with:

```sh
python3 -m unittest discover -s tests/ci -v
dotnet test scripts/FirebaseBindingAudit.Tests/FirebaseBindingAudit.Tests.csproj
```
