# Self-hosted macOS runner

Every native (Xcode) job in this repository runs on a maintainer-controlled Mac registered as a
self-hosted GitHub Actions runner. The pinned Xcode (`nativeBuild.xcode` in
`eng/firebase-release.json` and `eng/signin-release.json`) is newer than any Xcode on GitHub's
hosted macOS images, so hosted runners cannot build these packages.

## Which jobs use it

All of these run on a runner labelled `self-hosted`, `macOS`, `ARM64`, and
`xcode-<version>` (the `nativeBuild.runner` value, for example `xcode-27.1`):

- Firebase CI and release: `validate` and `signin-validate`
- Google Foundation Validation: `places` and `maps`
- CI: the manual `pack` job

Planning, the required aggregate checks, and publication stay on GitHub-hosted Ubuntu. Release
candidates for `main` are therefore built on the self-hosted Mac and published from hosted
Ubuntu after environment approval.

## When native jobs run

Pull requests never run on the self-hosted Mac. Native validation runs after merge, on each push
to `main`, and when a maintainer manually runs **Firebase CI and release** or **Google Foundation
Validation** on a branch. On pull requests the **Firebase validation** and **Google Sign-In
validation** checks only run the hosted planners. When native validation would be required, they
pass with a notice instead of waiting on the Mac. Updating a PR branch after another merge
therefore costs only the hosted checks.

Merged changes can break the native build on `main`. Publication still requires a passing
validation of the exact `main` commit, so a break blocks the release until it is fixed. Run the
workflow manually on a risky branch to validate it before merging.

A fork pull request can edit workflow files, so the job conditions alone are not a security
boundary. Under the repository's Actions settings, require approval for workflow runs from **all
outside collaborators**, and only approve fork runs after reading their workflow changes.

## Machine requirements

- Apple Silicon Mac, logged in to a GUI session. Simulator tests need one.
- The pinned Xcode installed anywhere under `/Applications` (any app name; for example
  `Xcode-27.1.0.app` from Xcodes). Jobs select it with `tools/ci/select-xcode.sh`, which sets
  `DEVELOPER_DIR` and `MD_APPLE_SDK_ROOT` for that job only. It does not run `sudo` and does not
  change the machine's `xcode-select` setting.
- An iOS simulator runtime for that Xcode.
- A Ruby on `PATH` that can install gems (for example Homebrew Ruby), and CocoaPods (`pod`).
  The Sign-In job installs its pinned CocoaPods into a job-local `GEM_HOME`.
- Python 3 and Git. .NET, Node.js, and Java are installed per job by their setup actions.

The runner keeps the job's `.env` and `.path` from registration. Set `DOTNET_INSTALL_DIR` in the
runner's `.env` to a directory inside the runner folder, so `actions/setup-dotnet` and
`dotnet workload install` never touch a machine-wide .NET install.

## Register a runner

1. Download and extract the macOS ARM64 runner into its own folder, for example
   `~/actions-runner-<repository>`.
2. Create a registration token and configure the runner:

   ```bash
   gh api -X POST repos/OWNER/REPO/actions/runners/registration-token --jq .token
   ```

   ```bash
   ./config.sh --url https://github.com/OWNER/REPO --token TOKEN --name "$(scutil --get LocalHostName)-REPO" --labels xcode-27.1 --unattended
   ```

3. Install and start it as a LaunchAgent for the logged-in user:

   ```bash
   ./svc.sh install && ./svc.sh start
   ```

When `nativeBuild.xcode` changes, install the new Xcode and add the matching `xcode-<version>`
label to the runner before merging the change.

## Throughput and sharing

One runner runs one job at a time, so native jobs from several merges or manual runs queue behind
each other. Other runners on the same Mac can run at the same time; they share the CPU and the simulator
service. The Firestore emulator checks pick free loopback ports for each run, so they do not
collide with emulators started by other jobs.

## Caches

The runner keeps CocoaPods, NuGet, and workload caches between jobs. Checkout removes ignored
build output such as `output/` and `externals/`, and the consumer suites restore into fresh
package folders, so packages built by earlier runs are not reused. If a run looks stale, clear
`~/Library/Caches/CocoaPods` and `~/.nuget/packages`.
