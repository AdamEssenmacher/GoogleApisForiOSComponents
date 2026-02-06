#addin nuget:?package=Cake.XCode&version=4.2.0
#addin nuget:?package=Cake.Xamarin.Build&version=4.1.1
#addin nuget:?package=Cake.FileHelpers&version=3.2.0

#load "poco.cake"
#load "components.cake"
#load "common.cake"
#load "custom_externals_download.cake"

var TARGET = Argument ("t", Argument ("target", "ci"));
var NAMES = Argument ("names", "");

var BUILD_COMMIT = EnvironmentVariable("BUILD_COMMIT") ?? "DEV";
var BUILD_NUMBER = EnvironmentVariable("BUILD_NUMBER") ?? "DEBUG";
var BUILD_TIMESTAMP = DateTime.UtcNow.ToString();

var IS_LOCAL_BUILD = true;
var BACKSLASH = string.Empty;

var SOLUTION_PATH = "./Xamarin.Google.sln";
var EXTERNALS_PATH = new DirectoryPath ("./externals");

// Artifacts that need to be built from pods or be copied from pods
var ARTIFACTS_TO_BUILD = new List<Artifact> ();

var SOURCES_TARGETS = new List<string> ();
var SAMPLES_TARGETS = new List<string> ();

FilePath GetCakeToolPath ()
{
	var possibleExe = GetFiles ("./**/tools/Cake/Cake.exe").FirstOrDefault ();
	if (possibleExe != null)
		return possibleExe;
		
	var p = System.Diagnostics.Process.GetCurrentProcess ();	
	return new FilePath (p.Modules[0].FileName);
}

string GetDefaultiOSSimulatorRuntimeIdentifier ()
{
	var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
	return arch == System.Runtime.InteropServices.Architecture.Arm64
		? "iossimulator-arm64"
		: "iossimulator-x64";
}

void BuildCake (string target)
{
	var cakeSettings = new CakeSettings { 
		ToolPath = GetCakeToolPath (),
		Arguments = new Dictionary<string, string> { { "target", target }, { "names", NAMES } },
		Verbosity = Verbosity.Diagnostic
	};

	// Run the script from the subfolder
	CakeExecuteScript ("./build.cake", cakeSettings);
}

// From Cake.Xamarin.Build, dumps out versions of things
// LogSystemInfo ();

Setup (context =>
{
	IS_LOCAL_BUILD = string.IsNullOrWhiteSpace (EnvironmentVariable ("AGENT_ID"));
	Information ($"Is a local build? {IS_LOCAL_BUILD}");
	// Always use forward slashes for MSBuild targets on all platforms
	BACKSLASH = "/";
});

Task("build")
	.Does(() =>
{
	BuildCake ("nuget");
	BuildCake ("samples");
});

// Prepares the artifacts to be built.
// From CI will always build everything but, locally you can customize what
// you build, just to save some time when testing locally.
Task("prepare-artifacts")
	.IsDependeeOf("externals")
	.Does(() =>
{
	SetArtifactsDependencies ();
	SetArtifactsPodSpecs ();
	SetArtifactsExtraPodfileLines ();
	SetArtifactsSamples ();

	var orderedArtifactsForBuild = new List<Artifact> ();
	var orderedArtifactsForSamples = new List<Artifact> ();

	if (string.IsNullOrWhiteSpace (NAMES)) {
		var artifacts = ARTIFACTS.Values.Where (a => !a.Ignore);
		orderedArtifactsForBuild.AddRange (artifacts);
		orderedArtifactsForSamples.AddRange (artifacts);
	} else {
		var names = NAMES.Split (',');
		foreach (var name in names) {
			if (!(ARTIFACTS.ContainsKey (name) && ARTIFACTS [name] is Artifact artifact))
				throw new Exception($"The {name} component does not exist.");
			
			if (artifact.Ignore)
				continue;

			orderedArtifactsForBuild.Add (artifact);
			AddArtifactDependencies (orderedArtifactsForBuild, artifact.Dependencies);
			orderedArtifactsForSamples.Add (artifact);
		}

		orderedArtifactsForBuild = orderedArtifactsForBuild.Distinct ().ToList ();
		orderedArtifactsForSamples = orderedArtifactsForSamples.Distinct ().ToList ();
	}

	orderedArtifactsForBuild.Sort ((f, s) => s.BuildOrder.CompareTo (f.BuildOrder));
	orderedArtifactsForSamples.Sort ((f, s) => s.BuildOrder.CompareTo (f.BuildOrder));
	ARTIFACTS_TO_BUILD.AddRange (orderedArtifactsForBuild);

	Information ("Build order:");

	foreach (var artifact in ARTIFACTS_TO_BUILD) {
		SOURCES_TARGETS.Add($@"{artifact.ComponentGroup}{BACKSLASH}{artifact.CsprojName.Replace ('.', '_')}");
		Information (artifact.Id);
	}

	foreach (var artifact in orderedArtifactsForSamples)
		if (artifact.Samples != null)
			foreach (var sample in artifact.Samples)
				SAMPLES_TARGETS.Add($@"{artifact.ComponentGroup}{BACKSLASH}{sample.Replace ('.', '_')}");
});

Task ("externals")
	.WithCriteria (!DirectoryExists (EXTERNALS_PATH) || !string.IsNullOrWhiteSpace (NAMES))
	.Does (() => 
{
	EnsureDirectoryExists (EXTERNALS_PATH);

	Information ("////////////////////////////////////////");
	Information ("// Pods Repo Update Started           //");
	Information ("////////////////////////////////////////");
	
	Information ("\nUpdating Cocoapods repo...");
	CocoaPodRepoUpdate ();

	Information ("////////////////////////////////////////");
	Information ("// Pods Repo Update Ended             //");
	Information ("////////////////////////////////////////");

	foreach (var artifact in ARTIFACTS_TO_BUILD) {
		UpdateVersionInCsproj (artifact);
		CreateAndInstallPodfile (artifact);
		BuildSdkOnPodfileV2 (artifact);
	}

	// Call here custom methods created at custom_externals_download.cake file
	// to download frameworks and/or bundles for the artifact
	if (ARTIFACTS_TO_BUILD.Contains (FIREBASE_ANALYTICS_ARTIFACT))
		FirebaseAnalyticsDownload ();
	if (ARTIFACTS_TO_BUILD.Contains (GOOGLE_GOOGLE_APP_MEASUREMENT_ARTIFACT))
		GoogleAppMeasurementDownload ();
});

Task ("ci-setup")
	.WithCriteria (!BuildSystem.IsLocalBuild)
	.Does (() => 
{
	var glob = "./source/**/AssemblyInfo.cs";

	ReplaceTextInFiles(glob, "{BUILD_COMMIT}", BUILD_COMMIT);
	ReplaceTextInFiles(glob, "{BUILD_NUMBER}", BUILD_NUMBER);
	ReplaceTextInFiles(glob, "{BUILD_TIMESTAMP}", BUILD_TIMESTAMP);
});

Task ("libs")
	.IsDependentOn("externals")
	.IsDependentOn("ci-setup")
	.Does(() =>
{
	var dotNetCoreBuildSettings = new DotNetCoreBuildSettings { 
		Configuration = "Release",
		Verbosity = DotNetCoreVerbosity.Diagnostic,
		NoRestore = false
	};
	
	// Build each artifact's csproj directly instead of using solution targets
	foreach (var artifact in ARTIFACTS_TO_BUILD) {
		var csprojPath = $"./source/{artifact.ComponentGroup}/{artifact.CsprojName}/{artifact.CsprojName}.csproj";
		Information ($"Building: {csprojPath}");
		DotNetCoreBuild(csprojPath, dotNetCoreBuildSettings);
	}
});

Task ("samples")
	.IsDependentOn("libs")
	.Does(() =>
{
	var msBuildSettings = new DotNetCoreMSBuildSettings ();
	msBuildSettings.Properties ["Platform"] = new [] { "iPhoneSimulator" };
	msBuildSettings.Properties ["RuntimeIdentifier"] = new [] { GetDefaultiOSSimulatorRuntimeIdentifier () };
	msBuildSettings.Properties ["EnableCodeSigning"] = new [] { "false" };

	var dotNetCoreBuildSettings = new DotNetCoreBuildSettings { 
		Configuration = "Release",
		Verbosity = DotNetCoreVerbosity.Diagnostic,
		NoRestore = false,
		MSBuildSettings = msBuildSettings
	};
	
	// Build each sample csproj directly
	foreach (var artifact in ARTIFACTS_TO_BUILD) {
		if (artifact.Samples == null)
			continue;
		foreach (var sample in artifact.Samples) {
			var samplePath = $"./samples/{artifact.ComponentGroup}/{sample}/{sample}.csproj";
			if (FileExists(samplePath)) {
				Information ($"Building sample: {samplePath}");
				DotNetCoreBuild(samplePath, dotNetCoreBuildSettings);
			}
		}
	}
});

Task ("nuget")
	.IsDependentOn("libs")
	.Does(() =>
{
	EnsureDirectoryExists("./output/");

	var dotNetCorePackSettings = new DotNetCorePackSettings {
		Configuration = "Release",
		NoRestore = true,
		NoBuild = true,
		OutputDirectory = "./output/",
		Verbosity = DotNetCoreVerbosity.Diagnostic,
	};

	// Pack each artifact's csproj directly
	foreach (var artifact in ARTIFACTS_TO_BUILD) {
		var csprojPath = $"./source/{artifact.ComponentGroup}/{artifact.CsprojName}/{artifact.CsprojName}.csproj";
		Information ($"Packing: {csprojPath}");
		DotNetCorePack(csprojPath, dotNetCorePackSettings);
	}
});

Task ("clean")
	.Does (() => 
{
	CleanVisualStudioSolution ();

	var deleteDirectorySettings = new DeleteDirectorySettings {
		Recursive = true,
		Force = true
	};

	if (DirectoryExists ("./externals/"))
		DeleteDirectory ("./externals", deleteDirectorySettings);

	if (DirectoryExists ("./output/"))
		DeleteDirectory ("./output", deleteDirectorySettings);
});

Task ("ci")
	.IsDependentOn("externals")
	.IsDependentOn("libs")
	.IsDependentOn("nuget")
	.IsDependentOn("samples");

Teardown (context =>
{
	var artifacts = GetFiles ("./output/**/*");

	if (artifacts?.Count () <= 0)
		return;

	Information ($"Found Artifacts ({artifacts.Count ()})");
	foreach (var a in artifacts)
		Information ("{0}", a);
});

RunTarget (TARGET);
