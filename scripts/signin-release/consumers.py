#!/usr/bin/env python3
"""Secret-free consumers of the sealed CocoaPods-produced Sign-In package candidate."""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
import os
import platform as host_platform
import shutil
import shlex
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release', ROOT / 'scripts/firebase-release/release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)
spec = importlib.util.spec_from_file_location('validation', ROOT / 'scripts/signin-release/validate.py')
validation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validation)


def files(root):
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in root.rglob('*') if p.is_file()}


def inspect_application(project, rid, expected_bundle):
    apps = list(project.parent.glob('bin/**/' + rid + '/Probe.app'))
    if len(apps) != 1:
        raise ValueError('Missing consumer application: ' + str(project))
    bundles = list(apps[0].rglob('GoogleSignIn.bundle'))
    if len(bundles) != 1 or not any(p.is_file() for p in bundles[0].rglob('*')):
        raise ValueError('Missing/duplicate Sign-In resource bundle: ' + str(project))
    if files(bundles[0]) != expected_bundle:
        raise ValueError('Altered Sign-In resource bundle: ' + str(project))
    counts = Counter(p.name for p in apps[0].rglob('*.bundle') if p.name.startswith(
        ('GoogleSignIn', 'AppAuth', 'AppCheckCore', 'GTMAppAuth', 'GoogleUtilities', 'FBLPromises', 'PromisesObjC')))
    if any(count > 1 for count in counts.values()):
        raise ValueError('Duplicate shared native resource bundle: ' + str(counts))
    return apps[0]


def build_application(run, name, command, project, rid, expected_bundle, rebuild=False):
    run(name, command, offline=True)
    app = inspect_application(project, rid, expected_bundle)
    if rebuild:
        run(name + '-rebuild', command + ['-t:Rebuild'], offline=True)
        app = inspect_application(project, rid, expected_bundle)
    return app


PROBE = '''using System; using System.IO; using System.Linq; using System.Reflection;
using System.Runtime.InteropServices; using Foundation; using UIKit; using ObjCRuntime;
class Program { static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate)); }
// iOS 27 requires the UIScene lifecycle; the probe needs a scene but no window.
[Register("SceneDelegate")] public class SceneDelegate : UIWindowSceneDelegate { public override UIWindow Window { get; set; } }
[Register("AppDelegate")] public class AppDelegate : UIApplicationDelegate {
 [DllImport("/usr/lib/libobjc.dylib")] static extern IntPtr objc_getClass(string name);
 [DllImport("/usr/lib/libobjc.dylib")] static extern IntPtr sel_registerName(string name);
 [DllImport("/usr/lib/libobjc.dylib")] static extern IntPtr class_getInstanceMethod(IntPtr type, IntPtr selector);
 [DllImport("/usr/lib/libobjc.dylib")] static extern IntPtr class_getClassMethod(IntPtr type, IntPtr selector);
 public override bool FinishedLaunching(UIApplication application, NSDictionary options) {
  string result;
  try {
   using var config = new Google.SignIn.Configuration("fixture-client-id");
   if (config.ClientId != "fixture-client-id") throw new Exception("Configuration roundtrip failed");
   if (Google.SignIn.SignIn.SharedInstance is null) throw new Exception("Missing singleton");
   using var button = new Google.SignIn.SignInButton();
   using var claim = Google.SignIn.Claim.CreateAuthTimeClaim();
   using var essentialClaim = Google.SignIn.Claim.CreateEssentialAuthTimeClaim();
   if (claim.Name != "auth_time" || claim.Essential || essentialClaim.Name != "auth_time" || !essentialClaim.Essential)
    throw new Exception("Claim factory/property roundtrip failed");
   if (Google.SignIn.Constants.AuthTimeClaimName.ToString() != "auth_time")
    throw new Exception("Missing auth_time claim constant");
   CLAIMS_PROBE
   // Actual refresh-token expiry requires credentialed token acquisition.
   Func<Google.SignIn.GoogleUser, NSDate?> refreshExpiration = user => user.RefreshToken.ExpirationDate;
   // Compile existing null-hint calls without starting a credentialed flow.
   Action<UIViewController> existingCall = vc => Google.SignIn.SignIn.SharedInstance.SignInWithPresentingViewController(vc, null, null);
   var registeredClasses = new System.Collections.Generic.HashSet<string>();
   var expectedClasses = new[] { "GIDClaim", "GIDConfiguration", "GIDGoogleUser", "GIDProfileData",
                                "GIDSignIn", "GIDSignInButton", "GIDSignInResult", "GIDToken" };
   int classes = 0, selectors = 0;
   foreach (var type in typeof(Google.SignIn.SignIn).Assembly.GetExportedTypes().Where(t => t.Namespace == "Google.SignIn")) {
    var registration = type.GetCustomAttribute<RegisterAttribute>(inherit: false);
    if (registration is null || !typeof(NSObject).IsAssignableFrom(type)) continue;
    var native = objc_getClass(registration.Name);
    if (native == IntPtr.Zero) throw new Exception("Missing native class " + registration.Name);
    if (!registeredClasses.Add(registration.Name)) throw new Exception("Duplicate native registration " + registration.Name);
    classes++;
    foreach (var member in type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
     if (member is not MethodBase method) continue;
     var exported = member.GetCustomAttribute<ExportAttribute>();
     if (exported is null) continue;
     var selector = sel_registerName(exported.Selector);
     var found = method.IsStatic ? class_getClassMethod(native, selector) : class_getInstanceMethod(native, selector);
     if (found == IntPtr.Zero) throw new Exception("Missing selector " + registration.Name + "::" + exported.Selector);
     selectors++;
    }
   }
   if (!registeredClasses.SetEquals(expectedClasses) || selectors < SELECTOR_FLOOR)
    throw new Exception("Incomplete native surface probe: " + classes + "/" + selectors);
   EXTRA_PROBE
   result = "PASS classes=" + classes + " selectors=" + selectors + " claims=CLAIM_NAMESINTEGRATION_RESULT";
  } catch (Exception e) { result = e.ToString(); }
  File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "signin-result.txt"), result);
  return true;
 }
}'''


AMR_PROBE = """   using var amr = Google.SignIn.Claim.CreateAmrClaim();
   using var essentialAmr = Google.SignIn.Claim.CreateEssentialAmrClaim();
   if (amr.Name != "amr" || amr.Essential || essentialAmr.Name != "amr" || !essentialAmr.Essential)
    throw new Exception("AMR claim factory/property roundtrip failed");
   if (Google.SignIn.Constants.AmrClaimName.ToString() != "amr")
    throw new Exception("Missing amr claim constant");
   using var claims = new NSSet<Google.SignIn.Claim>(claim, essentialClaim, amr, essentialAmr);
#if !__MACCATALYST__
   // Compile App Check setup (iOS only) without generating App Attest keys.
   Action<string> appCheckSetup = key => { Google.SignIn.SignIn.SharedInstance.ConfigureWithCompletion(null);
    Google.SignIn.SignIn.SharedInstance.ConfigureDebugProvider(key, null); };
   if (Google.SignIn.Constants.AppCheckErrorDomain is null || Google.SignIn.AppCheckErrorCode.UnexpectedError != (Google.SignIn.AppCheckErrorCode) 1)
    throw new Exception("Missing App Check error domain");
#endif
"""


def probe_source(combined, transitive, package_version):
    amr = release.version(package_version) >= release.version('9.2.0')
    claims_probe = AMR_PROBE if amr else 'using var claims = new NSSet<Google.SignIn.Claim>(claim, essentialClaim);'
    extra = 'BindingProbe.Describe();' if transitive else ''
    if combined:
        extra += '''foreach (var n in new[] { "FIRAuth", "FIRAnalytics", "FIRFirestore" })
            if (objc_getClass(n) == IntPtr.Zero) throw new Exception("Missing Firebase class " + n);
            if (!Firebase.Core.App.FirebaseVersion.StartsWith("12.", StringComparison.Ordinal))
                throw new Exception("Expected Firebase 12 runtime");'''
    marker = ' firebase12=FIRAuth,FIRAnalytics,FIRFirestore' if combined else ''
    return (PROBE.replace('CLAIMS_PROBE', claims_probe)
            .replace('SELECTOR_FLOOR', '71' if amr else '67')
            .replace('CLAIM_NAMES', 'auth_time,amr' if amr else 'auth_time')
            .replace('EXTRA_PROBE', extra).replace('INTEGRATION_RESULT', marker))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate', type=Path, default=ROOT / 'output/signin-candidate')
    parser.add_argument('--feed', type=Path)
    parser.add_argument('--combined', action='store_true')
    args = parser.parse_args()
    release.configure_channel('signin')
    manifest = release.read(args.candidate / 'manifest.json')
    release.validate_manifest_channel(manifest)
    feed = (args.feed or args.candidate).resolve()
    versions = {}
    for package in feed.glob('*.nupkg'):
        info = release.package_info(package.read_bytes())
        if info['id'] in versions:
            raise ValueError('Multiple package versions in consumer feed: ' + info['id'])
        versions[info['id']] = info['version']
    for package in manifest['packages']:
        if hashlib.sha256((feed / package['file']).read_bytes()).hexdigest() != package['sha256']:
            raise ValueError('Consumer feed does not contain the sealed candidate: ' + package['id'])
    hashes = {p['id']: p['sha256'] for p in manifest['packages']}
    results = ROOT / 'output/signin-results'; results.mkdir(parents=True, exist_ok=True)
    kind = 'combined' if args.combined else 'consumers'
    architecture = host_platform.machine()
    if architecture not in ('arm64', 'x86_64'):
        raise ValueError('Unsupported Apple consumer host architecture: ' + architecture)
    simulator_rid = 'iossimulator-' + architecture
    catalyst_rid = 'maccatalyst-' + architecture
    evidence = results / (kind + '.json'); evidence.unlink(missing_ok=True)
    work = Path(tempfile.mkdtemp(prefix='signin-consumers-')).resolve()
    bundle = 'com.googleapisforioscomponents.tests.signin.' + work.name.removeprefix('signin-consumers-').replace('_', '-')
    shutil.copy2(ROOT / 'global.json', work / 'global.json')
    (results / (kind + '-workspace.txt')).write_text(str(work) + '\n')
    tools = work / 'tools'; tools.mkdir()
    pod_marker = work / 'pod-invoked'
    (tools / 'pod').write_text('#!/bin/sh\ntouch ' + shlex.quote(str(pod_marker)) + '\necho Unexpected consumer CocoaPods invocation >&2\nexit 97\n')
    (tools / 'pod').chmod(0o755)
    env = dict(os.environ, PATH=str(tools) + os.pathsep + os.environ['PATH'],
               NUGET_PACKAGES=str(work / 'nuget'), NUGET_HTTP_CACHE_PATH=str(work / 'http'),
               XamarinBuildDownloadDir=str(work / 'xbd') + '/')
    blocked = dict(env, NO_PROXY='localhost,127.0.0.1', no_proxy='localhost,127.0.0.1', NUGET_CERT_REVOCATION_MODE='offline')
    for key in ('HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY', 'http_proxy', 'https_proxy', 'all_proxy'):
        blocked[key] = 'http://127.0.0.1:9'
    config = ET.Element('configuration')
    sources = ET.SubElement(config, 'packageSources'); ET.SubElement(sources, 'clear')
    ET.SubElement(sources, 'add', key='local', value=str(feed))
    ET.SubElement(sources, 'add', key='nuget', value='https://api.nuget.org/v3/index.json')
    mapping = ET.SubElement(config, 'packageSourceMapping')
    local = ET.SubElement(mapping, 'packageSource', key='local'); ET.SubElement(local, 'package', pattern='AdamE.*')
    online = ET.SubElement(mapping, 'packageSource', key='nuget'); ET.SubElement(online, 'package', pattern='*')
    ET.ElementTree(config).write(work / 'NuGet.config')
    checks = []

    def run(name, command, offline=False):
        print(kind + ': ' + name, flush=True)
        log = results / (kind + '-' + name + '.log')
        with log.open('w') as output:
            p = subprocess.run(command, cwd=work, env=blocked if offline else env, stdout=output, stderr=subprocess.STDOUT)
        if p.returncode:
            print(log.read_text()[-9000:], flush=True)
            raise RuntimeError('Failed ' + name + '; see ' + str(log))
        checks.append(name)

    def project(platform, transitive):
        path = work / (platform + ('-transitive' if transitive else '-direct')); path.mkdir()
        tfm = 'net10.0-' + platform
        # The Xcode 27 Mac Catalyst workload rejects apps targeting below Mac Catalyst 17.0.
        minimum_os = '17.0' if platform == 'maccatalyst' else '15.0'
        rids = simulator_rid + ';ios-arm64' if platform == 'ios' else catalyst_rid
        selected = ['AdamE.Google.iOS.SignIn']
        if args.combined:
            selected += ['AdamE.Firebase.iOS.' + n for n in ('Auth', 'Analytics', 'CloudFirestore')]
        refs = ''.join(f'<PackageReference Include="{n}" Version="[{versions[n]}]" />' for n in selected)
        if transitive:
            library = path / 'Bindings'; library.mkdir()
            (library / 'Bindings.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>{tfm}</TargetFramework></PropertyGroup><ItemGroup>{refs}</ItemGroup></Project>')
            (library / 'Bindings.cs').write_text('public static class BindingProbe { public static string Describe() => typeof(Google.SignIn.SignIn).FullName; }')
            refs = '<ProjectReference Include="Bindings/Bindings.csproj" />'
        (path / 'Probe.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>{tfm}</TargetFramework><RuntimeIdentifiers Condition="'$(RuntimeIdentifier)' == ''">{rids}</RuntimeIdentifiers><OutputType>Exe</OutputType>
<SupportedOSPlatformVersion>{minimum_os}</SupportedOSPlatformVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<IsPackable>false</IsPackable><MtouchLink>None</MtouchLink><EnableCodeSigning>false</EnableCodeSigning>
</PropertyGroup><ItemGroup>{refs}<Compile Include="Main.cs" /></ItemGroup></Project>''')
        (path / 'Main.cs').write_text(probe_source(args.combined, transitive, versions['AdamE.Google.iOS.SignIn']))
        (path / 'Info.plist').write_text('<?xml version="1.0"?><plist version="1.0"><dict><key>CFBundleIdentifier</key><string>' + bundle + '</string><key>CFBundleShortVersionString</key><string>1.0</string><key>CFBundleVersion</key><string>1</string>'
            '<key>UIApplicationSceneManifest</key><dict><key>UIApplicationSupportsMultipleScenes</key><false/>'
            '<key>UISceneConfigurations</key><dict><key>UIWindowSceneSessionRoleApplication</key><array><dict>'
            '<key>UISceneConfigurationName</key><string>Default Configuration</string>'
            '<key>UISceneDelegateClassName</key><string>SceneDelegate</string></dict></array></dict></dict>'
            '</dict></plist>')
        return path / 'Probe.csproj'

    projects = [(platform, transitive, project(platform, transitive)) for platform in ('ios', 'maccatalyst') for transitive in (False, True)]
    for platform, transitive, path in projects:
        name = path.parent.name
        run('restore-' + name, ['dotnet', 'restore', str(path), '--configfile', str(work / 'NuGet.config')])
        if files(work / 'xbd'):
            raise ValueError('Restore populated XBD')
    restored = files(work / 'nuget')
    simulator = None
    for platform, transitive, path in projects:
        for rid in ((simulator_rid, 'ios-arm64') if platform == 'ios' else (catalyst_rid,)):
            name = path.parent.name + '-' + rid
            command = ['dotnet', 'build', str(path), '-c', 'Debug', '--no-restore', '-p:RuntimeIdentifier=' + rid,
                       '-p:UseSharedCompilation=false', '-p:BuildInParallel=false']
            if platform == 'ios':
                command += ['-p:Platform=' + ('iPhone' if rid == 'ios-arm64' else 'iPhoneSimulator'),
                            '-p:SdkIsSimulator=' + ('false' if rid == 'ios-arm64' else 'true')]
            source = next(p for p in manifest['packages'] if p['id'] == 'AdamE.Google.iOS.SignIn')
            expected_bundle = validation.signin_bundle((feed / source['file']).read_bytes(), platform, rid)
            rebuild = platform == 'ios' and not transitive and rid.startswith('iossimulator')
            app = build_application(run, name, command, path, rid, expected_bundle, rebuild)
            if rebuild:
                simulator = app
    if pod_marker.exists():
        raise ValueError('Consumer invoked CocoaPods')
    if files(work / 'xbd') or files(work / 'nuget') != restored:
        raise ValueError('Packed consumers populated XBD or changed restored package bytes')
    for library in sorted(simulator.rglob('*.dylib')):
        subprocess.run(['codesign', '--force', '--sign', '-', str(library)], check=True, capture_output=True)
    subprocess.run(['codesign', '--force', '--deep', '--sign', '-', str(simulator)], check=True, capture_output=True)
    devices = json.loads(subprocess.check_output(['xcrun', 'simctl', 'list', 'devices', 'available', '-j']))
    device = next(d['udid'] for group in devices['devices'].values() for d in group if d['name'].startswith('iPhone'))
    subprocess.run(['xcrun', 'simctl', 'boot', device], capture_output=True)
    subprocess.run(['xcrun', 'simctl', 'bootstatus', device, '-b'], check=True)
    subprocess.run(['xcrun', 'simctl', 'uninstall', device, bundle], capture_output=True)
    subprocess.run(['xcrun', 'simctl', 'install', device, str(simulator)], check=True)
    subprocess.run(['xcrun', 'simctl', 'launch', device, bundle], check=True)
    container = subprocess.check_output(['xcrun', 'simctl', 'get_app_container', device, bundle, 'data']).decode().strip()
    import time
    result = Path(container) / 'Documents/signin-result.txt'
    for _ in range(30):
        if result.exists(): break
        time.sleep(1)
    if not result.exists() or not result.read_text().startswith('PASS '):
        raise ValueError('Sign-In runtime probe failed: ' + (result.read_text() if result.exists() else 'timeout'))
    if args.combined and ' firebase12=FIRAuth,FIRAnalytics,FIRFirestore' not in result.read_text():
        raise ValueError('Missing Firebase 12 runtime probe evidence')
    checks.append(result.read_text())
    release.write(evidence, {'Success': True, 'FatalError': None,
        'Cases': [{'Success': True, 'Name': name} for name in checks],
        'Checks': checks, 'RuntimeArchitecture': architecture, 'Lane': kind,
        'sourceCommit': manifest['sourceCommit'], 'packageHashes': hashes})
    print('PASS ' + kind + ': ' + str(evidence), flush=True)


if __name__ == '__main__':
    main()
