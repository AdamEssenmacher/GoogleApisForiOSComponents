# CI validation scope

Automatic validation uses two workflows:

- `Firebase CI and release` plans and validates the Firebase and Google Sign-In
  channels on pull requests, including package inspection and consumer/runtime
  checks. The required `Firebase validation` check and approval-gated main
  publication remain part of that workflow. It cancels superseded runs for the
  same pull request; main-branch and explicitly dispatched runs have unique
  concurrency groups.
- `Google Foundation Validation` runs after merge on main, or for any branch
  through a manual run, so pull requests never queue the self-hosted Mac
  (docs/SELF_HOSTED_MACOS_RUNNER.md). A Linux planner then schedules the Maps and
  Places Mac jobs independently when their selected inputs change. Those jobs
  retain build/pack, package structure, direct/transitive consumers, offline
  builds, and simulator tests.

The `CI` workflow is an explicit `workflow_dispatch` component build. Its `names`
input and package artifact remain available; it no longer automatically repeats
Google Sign-In build/pack already covered by the release workflow.

## Maps and Places planning

The planner compares the current revision with the previous main SHA. Cake
exports each selected dependency closure. The comparison includes native pins/download coordinates, package/project inputs, packed files,
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
