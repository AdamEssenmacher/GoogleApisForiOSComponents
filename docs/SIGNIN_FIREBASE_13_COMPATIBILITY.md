# Sign-In 10.1.0 binding revision for Firebase 13

The companion candidate `AdamE.Google.iOS.SignIn` 10.1.0.1 uses Google's native
Sign-In 10.1.0, AppAuth 3.0.0, and GTMAppAuth 6.0.0. Its exact shared dependencies
match the Firebase 13.1.1 archive. Use the published Sign-In 10.1.0 package for
the existing Firebase 12 package line.

The published Sign-In package contains dynamic libraries that load AppCheckCore
and GTMSessionFetcher by framework install name. Firebase 13 links those
libraries statically. Widening the dependency ranges would therefore leave a
native loader mismatch. This revision compiles only the private Sign-In closure
as static frameworks and continues to use the Firebase NuGets for shared
libraries. SwiftPM runs on the package producer's Mac. The application consumes
the packed Sign-In frameworks and bundles, and obtains shared binaries through
the Firebase packages' normal XBD targets.

To reproduce the companion candidate on a Mac with Xcode 27.1, stage the 23
Firebase 13.1.1 candidate packages in `output/firebase13`, then run:

```sh
python3 tools/signin-native/build.py
dotnet restore source/Google/SignIn/SignIn.csproj \
  --source "$PWD/output/firebase13" --source https://api.nuget.org/v3/index.json
dotnet pack source/Google/SignIn/SignIn.csproj --no-restore -c Release -o output/firebase13
python3 tools/firebase13/consumers.py --scope combined --with-signin
```

The combined consumer checks the exact restored packages, direct and transitive
builds, required resource bundles, absence of embedded dynamic copies of the
static frameworks, and secret-free simulator calls. A credentialed Sign-In,
token refresh, and Firebase Auth exchange on a device remain separate validation.

The companion candidate uses the explicit producer commands above while the
release pipeline remains on its published Sign-In baseline. Release integration
needs to recognize the binding revision, validate and seal its exact package,
and verify the required Firebase 13 support packages on both feeds before any
Sign-In deployment can become eligible.
