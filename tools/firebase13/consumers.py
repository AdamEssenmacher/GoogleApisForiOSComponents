#!/usr/bin/env python3
"""Exercise direct/transitive Firebase native acquisition and warm-cache offline builds."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
from generate import ROOT, MANIFEST
from native import digest

PROBE = '''using System; using System.IO; using System.Runtime.InteropServices; using Foundation; using UIKit;
class Program { static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate)); }
[Register("AppDelegate")] public class AppDelegate : UIApplicationDelegate { public override bool FinishedLaunching(UIApplication app, NSDictionary options) => true; }
[Register("SceneDelegate")] public class SceneDelegate : UIWindowSceneDelegate {
 public override UIWindow Window {get;set;}
 [DllImport("/usr/lib/libobjc.dylib")] static extern IntPtr objc_getClass(string name);
 public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions) {
  Window=new UIWindow((UIWindowScene)scene) { RootViewController=new UIViewController() }; Window.MakeKeyAndVisible();
  string result;
  try {
   if (!Firebase.Core.App.FirebaseVersion.Equals("EXPECTED_FIREBASE_VERSION")) throw new Exception("Wrong Firebase runtime");
   foreach (var name in new[] { CLASS_NAMES }) if(objc_getClass(name)==IntPtr.Zero) throw new Exception("Missing class "+name);
   EXTRA_PROBE
   result="PASS Firebase " + Firebase.Core.App.FirebaseVersion;
  } catch(Exception e) {result=e.ToString();}
  File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"firebase13-result.txt"),result);
 }
}'''


def snapshot(path):
    return {str(p.relative_to(path)): digest(p) for p in path.rglob('*') if p.is_file() and not p.name.endswith('.locked')}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resume',type=Path)
    parser.add_argument('--platform',choices=['ios','maccatalyst'])
    parser.add_argument('--scope',choices=['core','combined','all'],default='combined')
    parser.add_argument('--without-ad-id',action='store_true')
    parser.add_argument('--quick',action='store_true',help='Direct iOS simulator only; the default includes device/Catalyst/transitive shapes')
    parser.add_argument('--cache',type=Path,help='Reuse an existing XBD cache instead of testing a fresh acquisition')
    args=parser.parse_args()
    m=json.loads(MANIFEST.read_text());feed=ROOT/'output/firebase13'
    label=args.scope+('-without-ad-id' if args.without_ad_id else '')
    roots={'core':['Core'],'combined':['Analytics','Auth','CloudFirestore'],'all':[p['id'].split('.',1)[1] for p in m['packages'] if p['id'].startswith('Firebase.')]}[args.scope]
    work=(args.resume or Path(tempfile.mkdtemp(prefix='firebase13-consumer-'))).resolve();print('WORKSPACE',work,flush=True)
    results=ROOT/'output/firebase13-results';results.mkdir(parents=True,exist_ok=True)
    (results/(label+'-workspace.txt')).write_text(str(work))
    shutil.copy2(ROOT/'global.json',work/'global.json')
    cache=(args.cache or work/'xbd').resolve()
    env=dict(os.environ,NUGET_PACKAGES=str(work/'nuget'),NUGET_HTTP_CACHE_PATH=str(work/'http'),XamarinBuildDownloadDir=str(cache))
    tools=work/'tools';tools.mkdir(exist_ok=True);pod=tools/'pod';pod.write_text('#!/bin/sh\ntouch "'+str(work/'pod-invoked')+'"\nexit 97\n');pod.chmod(0o755);env['PATH']=str(tools)+os.pathsep+env['PATH']
    offline=dict(env,NO_PROXY='localhost,127.0.0.1',no_proxy='localhost,127.0.0.1',NUGET_CERT_REVOCATION_MODE='offline')
    for k in ('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','http_proxy','https_proxy','all_proxy'):offline[k]='http://127.0.0.1:9'
    (work/'NuGet.config').write_text(f'<configuration><packageSources><clear/><add key="local" value="{feed}"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping><packageSource key="local"><package pattern="AdamE.*"/></packageSource><packageSource key="nuget"><package pattern="*"/></packageSource></packageSourceMapping></configuration>')
    def run(name,cmd,blocked=False):
        print(name,flush=True)
        with (results/(label+'-'+name+'.log')).open('w') as output:
            p=subprocess.run(cmd,cwd=work,env=offline if blocked else env,stdout=output,stderr=subprocess.STDOUT)
        if p.returncode:raise RuntimeError((results/(label+'-'+name+'.log')).read_text()[-10000:])
    projects=[]
    for platform in ([args.platform] if args.platform else ['ios'] if args.quick else ['ios','maccatalyst']):
        for transitive in ([False] if args.quick else [False,True]):
            project=work/(platform+('-transitive' if transitive else '-direct'));project.mkdir(exist_ok=True)
            versions={p['id'].split('.',1)[1]:p['version'] for p in m['packages'] if p['id'].startswith('Firebase.')}
            refs=''.join(f'<PackageReference Include="AdamE.Firebase.iOS.{name}" Version="[{versions[name]}]"/>' for name in roots)
            if transitive:
                lib=project/'Library';lib.mkdir(exist_ok=True)
                (lib/'Library.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-{platform}</TargetFramework></PropertyGroup><ItemGroup>{refs}</ItemGroup></Project>')
                (lib/'Class.cs').write_text('public class BindingConsumer { public static string Version => Firebase.Core.App.FirebaseVersion; }')
                refs='<ProjectReference Include="Library/Library.csproj"/>'
            minimum_os='17.0' if platform=='maccatalyst' else '15.0'
            (project/'Probe.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-{platform}</TargetFramework><RuntimeIdentifiers Condition="&apos;$(RuntimeIdentifier)&apos; == &apos;&apos;">{"iossimulator-arm64;ios-arm64" if platform=="ios" else "maccatalyst-arm64"}</RuntimeIdentifiers><OutputType>Exe</OutputType><SupportedOSPlatformVersion>{minimum_os}</SupportedOSPlatformVersion><EnableCodeSigning>false</EnableCodeSigning><EnableDefaultCompileItems>false</EnableDefaultCompileItems><MtouchLink>None</MtouchLink></PropertyGroup><ItemGroup>{refs}<Compile Include="Main.cs"/></ItemGroup><Target Name="ReportFirebaseAssets" AfterTargets="_BeforeCoreCompileInterfaceDefinitions"><WriteLinesToFile File="$(MSBuildProjectDirectory)/native-assets.txt" Lines="@(NativeReference)" Overwrite="true"/><WriteLinesToFile File="$(MSBuildProjectDirectory)/bundle-assets.txt" Lines="@(BundleResource -&gt; &apos;%(Identity)|%(LogicalName)&apos;)" Overwrite="true"/></Target></Project>')
            classes=['FIRApp']+([] if args.scope=='core' else ['FIRAnalytics','FIRAuth','FIRFirestore','__FIRExprBridge','__FIRSortStageBridge'])
            extra=''
            if args.scope!='core':
                extra='''var sortType=typeof(Firebase.CloudFirestore.SortStageBridge);
                if(sortType==null) throw new Exception("Missing managed Firestore bridge");
                using var field=new Firebase.CloudFirestore.FieldBridge("field"); if(field.FieldName!="field") throw new Exception("Firestore bridge roundtrip failed");
                using var objectId=new Firebase.CloudFirestore.BsonObjectId("507f1f77bcf86cd799439011");
                using var sameId=new Firebase.CloudFirestore.BsonObjectId("507f1f77bcf86cd799439011"); if(!objectId.IsEqual(sameId)) throw new Exception("Inherited NSObject equality failed");
                using var timestamp=new Firebase.CloudFirestore.BsonTimestamp(7,2);
                using var integer=new Firebase.CloudFirestore.Int32Value(42);
                using var number=new Firebase.CloudFirestore.Decimal128Value("1.25");
                using var regex=new Firebase.CloudFirestore.RegexValue("a","i");
                using var bytes=NSData.FromArray(new byte[]{1,2});
                using var blob=Firebase.CloudFirestore.Blob.FromBsonBinary(bytes,128);
                using var expression=new Firebase.CloudFirestore.ConstantBridge(new NSString("value"));
                using var fields=NSDictionary<NSString,Firebase.CloudFirestore.ExprBridge>.FromObjectsAndKeys(new Firebase.CloudFirestore.ExprBridge[]{expression},new NSString[]{new NSString("field")});
                using var updateStage=new Firebase.CloudFirestore.UpdateStageBridge(fields);
                using var upsertStage=new Firebase.CloudFirestore.UpsertStageBridge(fields,null,null);
                using var literalsStage=new Firebase.CloudFirestore.LiteralsSourceStageBridge(new[]{fields});
                if(typeof(Firebase.CloudFirestore.UpdateStageBridge).GetConstructor(new[]{typeof(NSDictionary)})!=null ||
                   typeof(Firebase.CloudFirestore.UpsertStageBridge).GetConstructor(new[]{typeof(NSDictionary),typeof(string),typeof(Firebase.CloudFirestore.ExprBridge)})!=null ||
                   typeof(Firebase.CloudFirestore.LiteralsSourceStageBridge).GetConstructor(new[]{typeof(NSDictionary[])})!=null)
                    throw new Exception("Untyped stage initializer must remain internal");
                using var databaseStage=new Firebase.CloudFirestore.DatabaseSourceStageBridge();
                using var deleteStage=new Firebase.CloudFirestore.DeleteStageBridge();
                if(objectId.Value!="507f1f77bcf86cd799439011" || timestamp.Seconds!=7 || timestamp.Increment!=2 || integer.Value!=42 || number.Value!="1.25" || regex.Pattern!="a" || regex.Options!="i" || blob.Subtype!=128 || blob.Bytes.Length!=2 || Firebase.CloudFirestore.MinKey.SharedInstance is null || Firebase.CloudFirestore.MaxKey.SharedInstance is null)
                    throw new Exception("Firebase13 BSON value roundtrip failed");'''
            if args.scope=='all':
                extra += """using(var metadata=new Firebase.Storage.StorageMetadata()) {
                 using var copy=metadata.Copy(); if(!metadata.IsEqual(copy) || metadata.Description==null) throw new Exception("Inherited Storage API failed");
                }
                Action<Firebase.Storage.StorageDownloadTask> downloadApi=task=>{task.Enqueue();task.Pause();task.Cancel();task.Resume();};
                Action<Firebase.Storage.StorageUploadTask> uploadApi=task=>{task.Enqueue();task.Pause();task.Cancel();task.Resume();};
                GC.KeepAlive(downloadApi); GC.KeepAlive(uploadApi);
                """
            (project/'Main.cs').write_text(PROBE.replace('EXPECTED_FIREBASE_VERSION',m['releaseVersion']).replace('CLASS_NAMES',','.join('"'+n+'"' for n in classes)).replace('EXTRA_PROBE',extra))
            bundle='com.googleapisforioscomponents.firebase13.'+work.name.split('-')[-1]+'.'+platform+('.transitive' if transitive else '.direct')
            (project/'Info.plist').write_text(f'<plist version="1.0"><dict><key>CFBundleIdentifier</key><string>{bundle}</string><key>CFBundleShortVersionString</key><string>1.0</string><key>CFBundleVersion</key><string>1</string><key>UIApplicationSceneManifest</key><dict><key>UIApplicationSupportsMultipleScenes</key><false/><key>UISceneConfigurations</key><dict><key>UIWindowSceneSessionRoleApplication</key><array><dict><key>UISceneConfigurationName</key><string>Default Configuration</string><key>UISceneDelegateClassName</key><string>SceneDelegate</string></dict></array></dict></dict></dict></plist>')
            projects.append((project,platform,bundle))
    before=snapshot(cache)
    for project,platform,bundle in projects:run('restore-'+project.name,['dotnet','restore',str(project/'Probe.csproj'),'--force','--configfile',str(work/'NuGet.config')])
    assert snapshot(cache)==before,'Restore acquired native SDK files'
    for p in m['packages']:
        version=p['version'].removesuffix('.0')
        restored=work/'nuget'/p['package'].lower()/version/(p['package'].lower()+'.'+version+'.nupkg')
        if restored.exists():
            assert digest(restored)==digest(feed/(p['package']+'.'+version+'.nupkg')), 'Restored package differs from current candidate; rerun in a fresh workspace: '+p['package']
    restored_packages=snapshot(work/'nuget')
    first=True;simulator=None
    for project,platform,bundle in projects:
        for rid in (['iossimulator-arm64'] if args.quick else (['iossimulator-arm64','ios-arm64'] if platform=='ios' else ['maccatalyst-arm64'])):
            cmd=['dotnet','build',str(project/'Probe.csproj'),'-c','Debug','--no-restore','-p:RuntimeIdentifier='+rid,'-p:UseSharedCompilation=false','-p:BuildInParallel=false']
            if args.without_ad_id:cmd+=['-p:FirebaseWithoutAdIdSupport=True']
            if platform=='ios':cmd+=['-p:Platform='+('iPhoneSimulator' if 'simulator' in rid else 'iPhone'),'-p:SdkIsSimulator='+('true' if 'simulator' in rid else 'false')]
            run(project.name+'-'+rid,cmd,blocked=not first)
            warm=snapshot(cache)
            run(project.name+'-'+rid+'-offline-rebuild',cmd+(['-t:Rebuild'] if first and not args.resume else []),blocked=True)
            assert snapshot(cache)==warm,'Offline build modified the native cache'
            assets=(project/'native-assets.txt').read_text().splitlines()
            assert assets and len(assets)==len(set(assets)),'Missing/duplicate native references'
            app=next(project.glob('bin/**/'+rid+'/Probe.app'))
            for row in (project/'bundle-assets.txt').read_text().splitlines():
                source, logical = row.split('|',1)
                if source.endswith('PrivacyInfo.xcprivacy'):
                    candidates=[app/logical,app/'Contents/Resources'/logical]
                    assert any(p.exists() and digest(p)==digest(Path(source)) for p in candidates), 'Missing/altered privacy resource: '+logical
            bundles=[p.name for p in app.rglob('*.bundle')]
            assert len(bundles)==len(set(bundles)),'Duplicate logical bundles'
            if args.scope!='core':assert ('GoogleAdsOnDeviceConversion.xcframework' in '\n'.join(assets)) == (platform=='ios' and not args.without_ad_id)
            if args.without_ad_id:assert 'GoogleAppMeasurementIdentitySupport.xcframework' not in '\n'.join(assets)
            if first:simulator=(app,bundle)
            first=False
    assert snapshot(work/'nuget')==restored_packages,'Consumer modified restored package bytes'
    assert not (work/'pod-invoked').exists(),'Consumer invoked CocoaPods'
    if args.platform=='maccatalyst':
        (results/(label+'-catalyst-result.json')).write_text(json.dumps({'success':True,'workspace':str(work),'platform':'maccatalyst'},indent=2))
        print('PASS Mac Catalyst direct/transitive offline builds',flush=True)
        return
    app,bundle=simulator
    for lib in sorted(app.rglob('*.dylib')):subprocess.run(['codesign','--force','--sign','-',str(lib)],check=True,capture_output=True)
    subprocess.run(['codesign','--force','--deep','--sign','-',str(app)],check=True,capture_output=True)
    device=subprocess.check_output([str(ROOT/'tools/e2e/select-simulator.sh')],text=True).strip()
    subprocess.run(['xcrun','simctl','boot',device],capture_output=True)
    subprocess.run(['xcrun','simctl','bootstatus',device,'-b'],check=True,stdout=subprocess.DEVNULL)
    subprocess.run(['xcrun','simctl','install',device,str(app)],check=True)
    subprocess.run(['xcrun','simctl','launch',device,bundle],check=True)
    container=Path(subprocess.check_output(['xcrun','simctl','get_app_container',device,bundle,'data']).decode().strip())
    result=container/'Documents/firebase13-result.txt'
    for _ in range(30):
        if result.exists():break
        time.sleep(1)
    text=result.read_text() if result.exists() else 'timeout'
    assert text.startswith('PASS Firebase 13.'),text
    (results/(label+'-result.json')).write_text(json.dumps({'success':True,'runtime':text,'workspace':str(work),'sourceCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT).decode().strip(),'scope':args.scope,'quick':args.quick},indent=2))
    print(text,flush=True)


if __name__=='__main__':main()
