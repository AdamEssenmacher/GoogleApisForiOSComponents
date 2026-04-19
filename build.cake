#load "poco.cake"
#load "components.cake"
#load "common.cake"
#load "custom_externals_download.cake"
#load "firebase_release.cake"

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

void AddArtifactInDependencyOrder (List<Artifact> orderedArtifacts, HashSet<string> visitingArtifacts, HashSet<string> visitedArtifacts, Artifact artifact)
{
	if (artifact == null || artifact.Ignore)
		return;

	if (visitedArtifacts.Contains (artifact.Id))
		return;

	if (visitingArtifacts.Contains (artifact.Id))
		throw new Exception ($"Dependency cycle detected while preparing {artifact.Id}.");

	visitingArtifacts.Add (artifact.Id);

	if (artifact.Dependencies != null)
		foreach (var dependency in artifact.Dependencies)
			AddArtifactInDependencyOrder (orderedArtifacts, visitingArtifacts, visitedArtifacts, dependency);

	visitingArtifacts.Remove (artifact.Id);
	visitedArtifacts.Add (artifact.Id);
	orderedArtifacts.Add (artifact);
}

List<Artifact> OrderArtifactsByDependencies (IEnumerable<Artifact> artifacts)
{
	var orderedArtifacts = new List<Artifact> ();
	var visitingArtifacts = new HashSet<string> ();
	var visitedArtifacts = new HashSet<string> ();

	foreach (var artifact in artifacts)
		AddArtifactInDependencyOrder (orderedArtifacts, visitingArtifacts, visitedArtifacts, artifact);

	return orderedArtifacts;
}

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

	var selectedArtifactsForBuild = new List<Artifact> ();

	if (string.IsNullOrWhiteSpace (NAMES)) {
		var artifacts = ARTIFACTS.Values.Where (a => !a.Ignore);
		selectedArtifactsForBuild.AddRange (artifacts);
	} else {
		var names = NAMES.Split (',');
		foreach (var name in names) {
			if (!(ARTIFACTS.ContainsKey (name) && ARTIFACTS [name] is Artifact artifact))
				throw new Exception($"The {name} component does not exist.");
			
			if (artifact.Ignore)
				continue;

			selectedArtifactsForBuild.Add (artifact);
		}

		selectedArtifactsForBuild = selectedArtifactsForBuild.Distinct ().ToList ();
	}

	var orderedArtifactsForBuild = OrderArtifactsByDependencies (selectedArtifactsForBuild);
	ARTIFACTS_TO_BUILD.AddRange (orderedArtifactsForBuild);

	Information ("Build order:");

	foreach (var artifact in ARTIFACTS_TO_BUILD) {
		SOURCES_TARGETS.Add($@"{artifact.ComponentGroup}{BACKSLASH}{artifact.CsprojName.Replace ('.', '_')}");
		Information (artifact.Id);
	}
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
	var dotNetBuildSettings = new DotNetBuildSettings {
		Configuration = "Release",
		Verbosity = DotNetVerbosity.Diagnostic,
		NoRestore = false
	};
	
	// Build each artifact's csproj directly instead of using solution targets
	foreach (var artifact in ARTIFACTS_TO_BUILD) {
		var csprojPath = $"./source/{artifact.ComponentGroup}/{artifact.CsprojName}/{artifact.CsprojName}.csproj";
		Information ($"Building: {csprojPath}");
		DotNetBuild(csprojPath, dotNetBuildSettings);
	}
});

Task ("nuget")
	.IsDependentOn("externals")
	.IsDependentOn("ci-setup")
	.Does(() =>
{
	EnsureDirectoryExists("./output/");

	var outputPath = MakeAbsolute ((DirectoryPath)"./output/");
	var dotNetBuildMsBuildSettings = new DotNetMSBuildSettings ();
	dotNetBuildMsBuildSettings.Properties ["RestoreAdditionalProjectSources"] = new [] { outputPath.FullPath };

	var dotNetBuildSettings = new DotNetBuildSettings {
		Configuration = "Release",
		Verbosity = DotNetVerbosity.Diagnostic,
		NoRestore = false,
		MSBuildSettings = dotNetBuildMsBuildSettings
	};

	var dotNetPackSettings = new DotNetPackSettings {
		Configuration = "Release",
		NoRestore = true,
		NoBuild = true,
		OutputDirectory = "./output/",
		Verbosity = DotNetVerbosity.Diagnostic,
	};

	// Pack each artifact's csproj directly
	foreach (var artifact in ARTIFACTS_TO_BUILD) {
		var csprojPath = $"./source/{artifact.ComponentGroup}/{artifact.CsprojName}/{artifact.CsprojName}.csproj";
		Information ($"Building for pack: {csprojPath}");
		DotNetBuild(csprojPath, dotNetBuildSettings);
		Information ($"Packing: {csprojPath}");
		DotNetPack(csprojPath, dotNetPackSettings);
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
	.IsDependentOn("nuget");

Teardown (context =>
{
	var artifacts = GetFiles ("./output/**/*")
		.Where (path => !path.FullPath.Contains ("/output/firebase-release-check/"))
		.ToList ();

	if (artifacts?.Count () <= 0)
		return;

	Information ($"Found Artifacts ({artifacts.Count ()})");
	foreach (var a in artifacts)
		Information ("{0}", a);
});

RunTarget (TARGET);
