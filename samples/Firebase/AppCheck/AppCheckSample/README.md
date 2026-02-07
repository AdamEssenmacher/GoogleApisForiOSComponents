# Firebase App Check — AppCheckSample (iOS)

This sample validates the **Firebase App Check iOS** bindings in a real app.

## Prerequisites

- A Firebase project with an **iOS app** registered.
- A real device is strongly recommended.
  - Simulator runs can fail with keychain/attestation related issues and may not be representative.
- Add `GoogleService-Info.plist` next to the sample project (do **not** commit secrets):
  - `samples/Firebase/AppCheck/AppCheckSample/GoogleService-Info.plist`

## App Check (Debug) — Two workflows

Both workflows end up with the same outcome:
- Firebase Console knows your **debug token**.
- The app injects the same token at runtime.

### Workflow A — Get token from app logs, then register it in Firebase Console

1) Run the app once (see command below) and watch the console output.

2) Copy the debug token printed by the Firebase SDK.

3) Firebase Console:
- Go to **App Check** → select your iOS app → **Manage Debug tokens**.
- Paste the debug token value you copied.

4) In the sample app, tap on the `Fetch App Check Token` button: the test should succeed.

### Workflow B — Generate your own token in Firebase Console, then inject it at build/run time

This is useful when you want a deterministic token without first scraping logs.

1) Firebase Console:
- Go to **App Check** → select your iOS app → **Manage Debug tokens**.
- Generate a new debug token and copy its value.

2) Inject that same value when launching the app using `FIRAAppCheckDebugToken` (see command below).

3) In the sample app, tap on the `Fetch App Check Token` button: the test should succeed.

## Run on a real device (recommended)

Replace:
- `UDID` with your device UDID
- `YOUR_DEBUG_TOKEN` with your debug token (Workflow B only)

```bash
dotnet build samples/Firebase/AppCheck/AppCheckSample/AppCheckSample.csproj \
  -c Debug -f net9.0-ios -t:Run \
  -p:Platform=iPhone -p:RuntimeIdentifier=ios-arm64 \
  -p:_DeviceName=UDID \
  -p:_BundlerDebug=true -v:n \
  -p:MlaunchAdditionalArgumentsProperty="--setenv=FIRAAppCheckDebugToken=YOUR_DEBUG_TOKEN"
```

Note: on some setups `mlaunch` expects `_DeviceName` to be the **raw UDID** (no `:v2:udid=` prefix).

## Troubleshooting

- **`403 PERMISSION_DENIED` / `App attestation failed`**
  - The debug token is not registered for this Firebase app, or you’re using the wrong Firebase project / `GoogleService-Info.plist`.

- **`Keychain access error` (often on simulator)**
  - Prefer real device.
  - If you must use simulator, try a fresh simulator device and reinstall.

- **No Firebase configured UI / missing plist**
  - Ensure `GoogleService-Info.plist` is present at:
    - `samples/Firebase/AppCheck/AppCheckSample/GoogleService-Info.plist`

## What success looks like

The UI action to fetch an App Check token returns successfully.
