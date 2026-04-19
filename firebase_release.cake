using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var FIREBASE_RELEASE_VERSION = Argument ("firebase-version", "");
var FIREBASE_RELEASE_FROM_VERSION = Argument ("from-version", "");
var FIREBASE_RELEASE_TAG_COMMIT = Argument ("firebase-tag-commit", "");

var FIREBASE_RELEASE_ARTIFACT_KEYS = new [] {
	"Firebase.ABTesting",
	"Firebase.Analytics",
	"Firebase.AppCheck",
	"Firebase.AppDistribution",
	"Firebase.Auth",
	"Firebase.CloudFirestore",
	"Firebase.CloudFunctions",
	"Firebase.CloudMessaging",
	"Firebase.Core",
	"Firebase.Crashlytics",
	"Firebase.Database",
	"Firebase.InAppMessaging",
	"Firebase.Installations",
	"Firebase.PerformanceMonitoring",
	"Firebase.RemoteConfig",
	"Firebase.Storage",
	"Google.GoogleAppMeasurement",
};

var FIREBASE_RELEASE_ARTIFACT_VARIABLES = new [] {
	"FIREBASE_AB_TESTING_ARTIFACT",
	"FIREBASE_ANALYTICS_ARTIFACT",
	"FIREBASE_APP_CHECK_ARTIFACT",
	"FIREBASE_APP_DISTRIBUTION_ARTIFACT",
	"FIREBASE_AUTH_ARTIFACT",
	"FIREBASE_CLOUD_FIRESTORE_ARTIFACT",
	"FIREBASE_CLOUD_FUNCTIONS_ARTIFACT",
	"FIREBASE_CLOUD_MESSAGING_ARTIFACT",
	"FIREBASE_CORE_ARTIFACT",
	"FIREBASE_CRASHLYTICS_ARTIFACT",
	"FIREBASE_DATABASE_ARTIFACT",
	"FIREBASE_IN_APP_MESSAGING_ARTIFACT",
	"FIREBASE_INSTALLATIONS_ARTIFACT",
	"FIREBASE_PERFORMANCE_MONITORING_ARTIFACT",
	"FIREBASE_REMOTE_CONFIG_ARTIFACT",
	"FIREBASE_STORAGE_ARTIFACT",
	"GOOGLE_GOOGLE_APP_MEASUREMENT_ARTIFACT",
};

var FIREBASE_RELEASE_PODSPEC_NAMES = new [] {
	"Firebase",
	"FirebaseABTesting",
	"FirebaseAnalytics",
	"FirebaseAppCheck",
	"FirebaseAppCheckInterop",
	"FirebaseAuth",
	"FirebaseAuthInterop",
	"FirebaseCore",
	"FirebaseCoreExtension",
	"FirebaseCoreInternal",
	"FirebaseCrashlytics",
	"FirebaseDatabase",
	"FirebaseFirestore",
	"FirebaseFirestoreInternal",
	"FirebaseFunctions",
	"FirebaseInstallations",
	"FirebaseMessaging",
	"FirebaseMessagingInterop",
	"FirebasePerformance",
	"FirebaseRemoteConfig",
	"FirebaseRemoteConfigInterop",
	"FirebaseSessions",
	"FirebaseSharedSwift",
	"FirebaseStorage",
	"GoogleAppMeasurement",
};

var FIREBASE_RELEASE_RESOLUTION_PODS = new [] {
	"FirebaseABTesting",
	"FirebaseAnalytics",
	"FirebaseAppCheck",
	"FirebaseAuth",
	"FirebaseCrashlytics",
	"FirebaseDatabase",
	"FirebaseFirestore",
	"FirebaseFunctions",
	"FirebaseInstallations",
	"FirebaseMessaging",
	"FirebasePerformance",
	"FirebaseRemoteConfig",
	"FirebaseStorage",
	"GoogleAppMeasurement",
	"Firebase/AppDistribution",
	"Firebase/InAppMessaging",
};

var FIREBASE_RELEASE_MANUAL_POD_PINS = new Dictionary<string, string> {
	{ "RecaptchaInterop", "101.0.0" },
	{ "BoringSSL-GRPC", "0.0.37" },
	{ "gRPC-Core", "1.69.0" },
	{ "gRPC-C++", "1.69.0" },
	{ "abseil", "1.20240722.0" },
	{ "PromisesSwift", "2.4.0" },
	{ "leveldb-library", "1.22.6" },
	{ "GoogleUtilities", "8.1.0" },
	{ "GoogleDataTransport", "10.1.0" },
	{ "nanopb", "3.30910.0" },
};

var FIREBASE_RELEASE_EXPECTED_RESOLVED_PODS = new [] {
	"AppCheckCore",
	"BoringSSL-GRPC",
	"Firebase",
	"FirebaseABTesting",
	"FirebaseAnalytics",
	"FirebaseAppCheck",
	"FirebaseAppCheckInterop",
	"FirebaseAppDistribution",
	"FirebaseAuth",
	"FirebaseAuthInterop",
	"FirebaseCore",
	"FirebaseCoreExtension",
	"FirebaseCoreInternal",
	"FirebaseCrashlytics",
	"FirebaseDatabase",
	"FirebaseFirestore",
	"FirebaseFirestoreInternal",
	"FirebaseFunctions",
	"FirebaseInAppMessaging",
	"FirebaseInstallations",
	"FirebaseMessaging",
	"FirebaseMessagingInterop",
	"FirebasePerformance",
	"FirebaseRemoteConfig",
	"FirebaseRemoteConfigInterop",
	"FirebaseSessions",
	"FirebaseSharedSwift",
	"FirebaseStorage",
	"GTMSessionFetcher",
	"GoogleAdsOnDeviceConversion",
	"GoogleAppMeasurement",
	"GoogleDataTransport",
	"GoogleUtilities",
	"PromisesObjC",
	"PromisesSwift",
	"RecaptchaInterop",
	"abseil",
	"gRPC-C++",
	"gRPC-Core",
	"leveldb-library",
	"nanopb",
};

var FIREBASE_RELEASE_NATIVE_FRAMEWORK_ALIASES = new Dictionary<string, string []> {
	{ "GoogleAppMeasurement", new [] { "GoogleAppMeasurementIdentitySupport" } },
};

class FirebaseReleaseContext
{
	public string TargetVersion { get; set; }
	public string FromVersion { get; set; }
	public string TagCommit { get; set; }
	public Dictionary<string, FirebaseReleaseDownloadInfo> ExternalDownloads { get; set; }
	public Dictionary<string, FirebaseReleaseNativeMetadata> FromNativeMetadata { get; set; }
	public Dictionary<string, FirebaseReleaseNativeMetadata> TargetNativeMetadata { get; set; }
}

class FirebaseReleaseDownloadInfo
{
	public string Id { get; set; }
	public string Version { get; set; }
	public string ArchiveKey { get; set; }
}

class FirebaseReleaseNativeMetadata
{
	public string FrameworkName { get; set; }
	public List<string> Frameworks { get; set; } = new List<string> ();
	public List<string> WeakFrameworks { get; set; } = new List<string> ();
	public List<string> LinkerFlags { get; set; } = new List<string> ();
}

class FirebaseReleasePodResolution
{
	public Dictionary<string, string> Versions { get; set; }
	public HashSet<string> Names { get; set; }
}

class FirebaseReleaseTextFile
{
	public string Text { get; set; }
	public bool HasUtf8Bom { get; set; }
}

class FirebaseReleaseFileUpdate
{
	public FilePath Path { get; set; }
	public string OriginalText { get; set; }
	public string UpdatedText { get; set; }
	public bool HasUtf8Bom { get; set; }
	public bool Changed => !string.Equals (OriginalText, UpdatedText, StringComparison.Ordinal);
}

Task ("firebase-release-check")
	.Does (() => RunFirebaseReleaseTooling (applyChanges: false));

Task ("firebase-release-update")
	.Does (() => RunFirebaseReleaseTooling (applyChanges: true));

void RunFirebaseReleaseTooling (bool applyChanges)
{
	var context = CreateFirebaseReleaseContext ();
	var updates = GetFirebaseReleaseFileUpdates (context);
	var changedUpdates = updates.Where (u => u.Changed).ToList ();

	if (changedUpdates.Count == 0) {
		Information ($"Firebase release metadata is already aligned to {context.TargetVersion}.");
		return;
	}

	foreach (var update in changedUpdates)
		Information ($"{(applyChanges ? "Updating" : "Drift")}: {update.Path}");

	if (!applyChanges)
		throw new Exception ($"Firebase release metadata drift found in {changedUpdates.Count} file(s). Run --target=firebase-release-update --firebase-version={context.TargetVersion} to apply.");

	foreach (var update in changedUpdates)
		WriteFirebaseReleaseTextFile (update.Path, update.UpdatedText, update.HasUtf8Bom);

	Information ($"Updated {changedUpdates.Count} Firebase release metadata file(s) to {context.TargetVersion}.");
}

FirebaseReleaseContext CreateFirebaseReleaseContext ()
{
	if (string.IsNullOrWhiteSpace (FIREBASE_RELEASE_VERSION))
		throw new Exception ("Missing --firebase-version=<version>.");

	SetArtifactsDependencies ();
	SetArtifactsPodSpecs ();

	var fromVersion = string.IsNullOrWhiteSpace (FIREBASE_RELEASE_FROM_VERSION)
		? InferFirebaseReleaseFromVersion ()
		: FIREBASE_RELEASE_FROM_VERSION;

	var tagCommit = string.IsNullOrWhiteSpace (FIREBASE_RELEASE_TAG_COMMIT)
		? ResolveFirebaseReleaseTagCommit (FIREBASE_RELEASE_VERSION)
		: FIREBASE_RELEASE_TAG_COMMIT;

	var externalDownloads = ResolveFirebaseReleaseExternalDownloads (FIREBASE_RELEASE_VERSION);
	var podResolution = ResolveFirebaseReleasePodfile (FIREBASE_RELEASE_VERSION);
	ValidateFirebaseReleaseResolvedPods (podResolution);
	ValidateFirebaseReleaseResolvedPodSet (podResolution);
	var targetNativeMetadata = ResolveFirebaseReleaseNativeMetadata (FIREBASE_RELEASE_VERSION);
	var fromNativeMetadata = string.Equals (fromVersion, FIREBASE_RELEASE_VERSION, StringComparison.Ordinal)
		? targetNativeMetadata
		: ResolveFirebaseReleaseNativeMetadata (fromVersion);

	return new FirebaseReleaseContext {
		TargetVersion = FIREBASE_RELEASE_VERSION,
		FromVersion = fromVersion,
		TagCommit = tagCommit,
		ExternalDownloads = externalDownloads,
		FromNativeMetadata = fromNativeMetadata,
		TargetNativeMetadata = targetNativeMetadata
	};
}

string InferFirebaseReleaseFromVersion ()
{
	var versions = FIREBASE_RELEASE_ARTIFACT_KEYS
		.Select (key => ARTIFACTS [key].NugetVersion)
		.Distinct ()
		.OrderBy (version => version)
		.ToArray ();

	if (versions.Length != 1)
		throw new Exception ($"Cannot infer --from-version because Firebase release artifacts have multiple versions: {string.Join (", ", versions)}.");

	return versions [0];
}

List<FirebaseReleaseFileUpdate> GetFirebaseReleaseFileUpdates (FirebaseReleaseContext context)
{
	var updates = new List<FirebaseReleaseFileUpdate> ();

	updates.Add (CreateFirebaseReleaseFileUpdate ("components.cake", text => UpdateFirebaseReleaseComponentsCake (text, context.TargetVersion)));
	updates.Add (CreateFirebaseReleaseFileUpdate ("custom_externals_download.cake", text => UpdateFirebaseReleaseExternalDownloadsCake (text, context.ExternalDownloads)));
	updates.Add (CreateFirebaseReleaseFileUpdate ("scripts/FirebaseBindingAudit/BindingSurfaceCoverage.cs", text => UpdateFirebaseReleaseBindingSurfaceCoverage (text, context.TargetVersion)));
	updates.Add (CreateFirebaseReleaseFileUpdate ("scripts/FirebaseBindingAudit.Tests/BindingSurfaceCoverageValidatorTests.cs", text => UpdateFirebaseReleaseBindingSurfaceCoverageTests (text)));
	updates.Add (CreateFirebaseReleaseFileUpdate ("tests/E2E/Firebase.Foundation/binding-surface-coverage.json", text => UpdateFirebaseReleaseBindingSurfaceCoverageManifest (text, context.TargetVersion)));
	updates.Add (CreateFirebaseReleaseFileUpdate ("tests/E2E/Firebase.Foundation/runtime-drift-cases.json", text => UpdateFirebaseReleaseBindingSurfaceCoverageManifest (text, context.TargetVersion)));
	updates.Add (CreateFirebaseReleaseFileUpdate ("tests/E2E/Firebase.Foundation/FirebaseFoundationE2E/FirebaseFoundationE2E.csproj", text => UpdateFirebaseReleaseE2EProject (text, context.TargetVersion)));

	foreach (var artifact in FIREBASE_RELEASE_ARTIFACT_KEYS.Select (key => ARTIFACTS [key]))
		updates.Add (CreateFirebaseReleaseFileUpdate (GetArtifactProjectPath (artifact), text => UpdateFirebaseReleaseProjectFile (text, artifact, context)));

	foreach (var targetPath in GetFirebaseReleaseTargetsFiles ())
		updates.Add (CreateFirebaseReleaseFileUpdate (targetPath, text => UpdateFirebaseReleaseTargetsFile (text, context.TargetVersion, context.TagCommit)));

	return updates;
}

FilePath GetArtifactProjectPath (Artifact artifact)
	=> new FilePath ($"source/{artifact.ComponentGroup}/{artifact.CsprojName}/{artifact.CsprojName}.csproj");

IEnumerable<FilePath> GetFirebaseReleaseTargetsFiles ()
{
	yield return new FilePath ("source/Firebase/Analytics/Analytics.targets");
	yield return new FilePath ("source/Firebase/AppCheck/AppCheck.targets");
	yield return new FilePath ("source/Firebase/Core/Core.targets");
	yield return new FilePath ("source/Firebase/Crashlytics/Crashlytics.targets");
	yield return new FilePath ("source/Google/GoogleAppMeasurement/GoogleAppMeasurement.targets");
}

FirebaseReleaseFileUpdate CreateFirebaseReleaseFileUpdate (string relativePath, Func<string, string> update)
	=> CreateFirebaseReleaseFileUpdate (new FilePath (relativePath), update);

FirebaseReleaseFileUpdate CreateFirebaseReleaseFileUpdate (FilePath relativePath, Func<string, string> update)
{
	var textFile = ReadFirebaseReleaseTextFile (relativePath);
	var updatedText = update (textFile.Text);
	if (!string.Equals (textFile.Text, updatedText, StringComparison.Ordinal))
		updatedText = NormalizeFirebaseReleaseUpdatedText (textFile.Text, updatedText);

	return new FirebaseReleaseFileUpdate {
		Path = relativePath,
		OriginalText = textFile.Text,
		UpdatedText = updatedText,
		HasUtf8Bom = textFile.HasUtf8Bom
	};
}

string NormalizeFirebaseReleaseUpdatedText (string originalText, string updatedText)
{
	var originalLines = originalText.Split ('\n');
	var updatedLines = updatedText.Split ('\n');

	for (var i = 0; i < updatedLines.Length; i++)
		if (i >= originalLines.Length || !string.Equals (originalLines [i], updatedLines [i], StringComparison.Ordinal))
			updatedLines [i] = TrimFirebaseReleaseLineTrailingWhitespace (updatedLines [i]);

	updatedText = string.Join ("\n", updatedLines);
	return updatedText.EndsWith ("\n", StringComparison.Ordinal)
		? updatedText
		: updatedText + "\n";
}

string TrimFirebaseReleaseLineTrailingWhitespace (string line)
{
	var hasCarriageReturn = line.EndsWith ("\r", StringComparison.Ordinal);
	var content = hasCarriageReturn ? line.Substring (0, line.Length - 1) : line;
	content = Regex.Replace (content, "[\\t ]+$", "");
	return hasCarriageReturn ? content + "\r" : content;
}

FirebaseReleaseTextFile ReadFirebaseReleaseTextFile (FilePath relativePath)
{
	var absolutePath = MakeAbsolute (relativePath);
	var bytes = System.IO.File.ReadAllBytes (absolutePath.FullPath);
	var hasUtf8Bom = bytes.Length >= 3 && bytes [0] == 0xEF && bytes [1] == 0xBB && bytes [2] == 0xBF;

	return new FirebaseReleaseTextFile {
		Text = System.IO.File.ReadAllText (absolutePath.FullPath, Encoding.UTF8),
		HasUtf8Bom = hasUtf8Bom
	};
}

void WriteFirebaseReleaseTextFile (FilePath relativePath, string text, bool hasUtf8Bom)
{
	var absolutePath = MakeAbsolute (relativePath);
	System.IO.File.WriteAllText (absolutePath.FullPath, text, new UTF8Encoding (hasUtf8Bom));
}

string UpdateFirebaseReleaseComponentsCake (string text, string targetVersion)
{
	foreach (var variableName in FIREBASE_RELEASE_ARTIFACT_VARIABLES)
		text = ReplaceFirebaseReleaseArtifactVersion (text, variableName, targetVersion);

	foreach (var podSpecName in FIREBASE_RELEASE_PODSPEC_NAMES)
		text = ReplaceFirebaseReleasePodSpecVersion (text, podSpecName, targetVersion);

	return text;
}

string ReplaceFirebaseReleaseArtifactVersion (string text, string variableName, string targetVersion)
{
	var pattern = "(" + Regex.Escape (variableName) + "\\s*=\\s*new Artifact\\s*\\(\\s*\"[^\"]+\"\\s*,\\s*\")([^\"]+)(\")";
	return Regex.Replace (text, pattern, "${1}" + targetVersion + "${3}");
}

string ReplaceFirebaseReleasePodSpecVersion (string text, string podSpecName, string targetVersion)
{
	var pattern = "(PodSpec\\.Create\\s*\\(\\s*\"" + Regex.Escape (podSpecName) + "\"\\s*,\\s*\")([^\"]+)(\")";
	return Regex.Replace (text, pattern, "${1}" + targetVersion + "${3}");
}

string UpdateFirebaseReleaseExternalDownloadsCake (string text, Dictionary<string, FirebaseReleaseDownloadInfo> externalDownloads)
{
	foreach (var pair in externalDownloads) {
		var source = pair.Value;
		var pattern = "(\\{\\s*\"" + Regex.Escape (source.Id) + "\"\\s*,\\s*new ExternalDownloadSource\\s*\\(\\s*\"" + Regex.Escape (source.Id) + "\"\\s*,\\s*\")([^\"]+)(\"\\s*,\\s*\")([^\"]+)(\"\\s*\\)\\s*\\},)";
		text = Regex.Replace (text, pattern, "${1}" + source.Version + "${3}" + source.ArchiveKey + "${5}");
	}

	return text;
}

string UpdateFirebaseReleaseProjectFile (string text, Artifact artifact, FirebaseReleaseContext context)
{
	text = ReplaceFirebaseReleaseXmlElement (text, "AssemblyVersion", context.TargetVersion);
	text = ReplaceFirebaseReleaseXmlElement (text, "FileVersion", context.TargetVersion);
	text = ReplaceFirebaseReleasePackageVersion (text, context.TargetVersion);

	if (artifact.Id == "Firebase.Analytics")
		text = ReplaceFirebaseReleasePackageReferenceVersion (text, "AdamE.Google.iOS.GoogleAppMeasurement", context.TargetVersion);

	text = UpdateFirebaseReleaseNativeReferences (text, context.FromNativeMetadata, context.TargetNativeMetadata);
	return text;
}

string ReplaceFirebaseReleaseXmlElement (string text, string elementName, string targetVersion)
{
	var pattern = "(<" + Regex.Escape (elementName) + ">)([^<]+)(</" + Regex.Escape (elementName) + ">)";
	return Regex.Replace (text, pattern, "${1}" + targetVersion + "${3}");
}

string ReplaceFirebaseReleasePackageVersion (string text, string targetVersion)
{
	return Regex.Replace (
		text,
		"(<PackageVersion>)([^<]+)(</PackageVersion>)",
		match => match.Groups [2].Value.Contains ("-")
			? match.Value
			: match.Groups [1].Value + targetVersion + match.Groups [3].Value);
}

string ReplaceFirebaseReleasePackageReferenceVersion (string text, string packageId, string targetVersion)
{
	var pattern = "(<PackageReference\\s+Include=\"" + Regex.Escape (packageId) + "\"\\s+Version=\")([^\"]+)(\")";
	return Regex.Replace (text, pattern, "${1}" + targetVersion + "${3}");
}

string UpdateFirebaseReleaseNativeReferences (
	string text,
	Dictionary<string, FirebaseReleaseNativeMetadata> fromMetadata,
	Dictionary<string, FirebaseReleaseNativeMetadata> targetMetadata)
{
	var pattern = "(<_?NativeReference\\b[^>]*Include=\"([^\"]+\\.xcframework)\"[^>]*>)([\\s\\S]*?)(</_?NativeReference>)";
	return Regex.Replace (text, pattern, match => {
		var frameworkName = GetFirebaseReleaseFrameworkNameFromInclude (match.Groups [2].Value);
		if (string.IsNullOrWhiteSpace (frameworkName))
			return match.Value;

		var from = fromMetadata.ContainsKey (frameworkName)
			? fromMetadata [frameworkName]
			: new FirebaseReleaseNativeMetadata { FrameworkName = frameworkName };
		var target = targetMetadata.ContainsKey (frameworkName)
			? targetMetadata [frameworkName]
			: new FirebaseReleaseNativeMetadata { FrameworkName = frameworkName };

		if (!fromMetadata.ContainsKey (frameworkName) && !targetMetadata.ContainsKey (frameworkName))
			return match.Value;

		var block = match.Value;
		block = UpdateFirebaseReleaseNativeReferenceElement (block, "Frameworks", from.Frameworks, target.Frameworks);
		block = UpdateFirebaseReleaseNativeReferenceElement (block, "WeakFrameworks", from.WeakFrameworks, target.WeakFrameworks);
		block = UpdateFirebaseReleaseNativeReferenceElement (block, "LinkerFlags", from.LinkerFlags, target.LinkerFlags);
		return block;
	});
}

string GetFirebaseReleaseFrameworkNameFromInclude (string include)
{
	var normalized = include.Replace ('\\', '/');
	var match = Regex.Match (normalized, "(^|/)([^/]+)\\.xcframework$");
	return match.Success ? match.Groups [2].Value : null;
}

string UpdateFirebaseReleaseNativeReferenceElement (string block, string elementName, List<string> fromTokens, List<string> targetTokens)
{
	var existingTokens = ReadFirebaseReleaseNativeReferenceTokens (block, elementName);
	var updatedTokens = MergeFirebaseReleaseNativeMetadataTokens (existingTokens, fromTokens, targetTokens);
	var updatedValue = string.Join (" ", updatedTokens);
	return SetFirebaseReleaseNativeReferenceElementValue (block, elementName, updatedValue);
}

List<string> ReadFirebaseReleaseNativeReferenceTokens (string block, string elementName)
{
	var match = Regex.Match (block, "<" + Regex.Escape (elementName) + ">([^<]*)</" + Regex.Escape (elementName) + ">");
	if (!match.Success || string.IsNullOrWhiteSpace (match.Groups [1].Value))
		return new List<string> ();

	return match.Groups [1].Value
		.Split (new [] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
		.ToList ();
}

List<string> MergeFirebaseReleaseNativeMetadataTokens (List<string> existingTokens, List<string> fromTokens, List<string> targetTokens)
{
	var fromSet = new HashSet<string> (fromTokens, StringComparer.Ordinal);
	var targetSet = new HashSet<string> (targetTokens, StringComparer.Ordinal);
	var result = new List<string> ();

	foreach (var token in existingTokens) {
		if (fromSet.Contains (token) && !targetSet.Contains (token))
			continue;

		AddFirebaseReleaseUniqueToken (result, token);
	}

	foreach (var token in targetTokens)
		AddFirebaseReleaseUniqueToken (result, token);

	return result;
}

string SetFirebaseReleaseNativeReferenceElementValue (string block, string elementName, string value)
{
	var elementPattern = "(<" + Regex.Escape (elementName) + ">)([^<]*)(</" + Regex.Escape (elementName) + ">)";
	if (Regex.IsMatch (block, elementPattern)) {
		if (string.IsNullOrWhiteSpace (value))
			return Regex.Replace (block, "\\r?\\n\\s*<" + Regex.Escape (elementName) + ">[^<]*</" + Regex.Escape (elementName) + ">", "");

		return Regex.Replace (block, elementPattern, "${1}" + value + "${3}");
	}

	if (string.IsNullOrWhiteSpace (value))
		return block;

	var closeMatch = Regex.Match (block, "(\\r?\\n)(\\s*)(</_?NativeReference>)");
	if (!closeMatch.Success)
		return block;

	var childIndent = closeMatch.Groups [2].Value + "  ";
	var insertion = closeMatch.Groups [1].Value + childIndent + "<" + elementName + ">" + value + "</" + elementName + ">";
	return Regex.Replace (block, "(\\r?\\n)(\\s*)(</_?NativeReference>)", insertion + "${1}${2}${3}", RegexOptions.None, TimeSpan.FromSeconds (1));
}

string UpdateFirebaseReleaseTargetsFile (string text, string targetVersion, string tagCommit)
{
	text = Regex.Replace (text, "(<_[^>]+AssemblyName>[^<]* Version=)([^,]+)(,)", "${1}" + targetVersion + "${3}");
	text = Regex.Replace (text, "(<_FirebaseCrashlyticsItemsFolder>FCrshlytcs-)([^<]+)(</_FirebaseCrashlyticsItemsFolder>)", "${1}" + targetVersion + "${3}");
	text = Regex.Replace (
		text,
		"https://raw\\.githubusercontent\\.com/firebase/firebase-ios-sdk/[^/]+/Crashlytics/upload-symbols",
		"https://raw.githubusercontent.com/firebase/firebase-ios-sdk/" + tagCommit + "/Crashlytics/upload-symbols");
	return text;
}

string UpdateFirebaseReleaseBindingSurfaceCoverage (string text, string targetVersion)
{
	if (text.Contains ("DefaultFirebasePackageVersion"))
		return Regex.Replace (text, "(DefaultFirebasePackageVersion\\s*=\\s*\")([^\"]+)(\")", "${1}" + targetVersion + "${3}");

	var newLine = text.Contains ("\r\n") ? "\r\n" : "\n";
	var namespaceLine = "namespace FirebaseBindingAudit;" + newLine;
	var versionClass =
		namespaceLine + newLine +
		"internal static class FirebasePackageVersions" + newLine +
		"{" + newLine +
		"    public const string DefaultFirebasePackageVersion = \"" + targetVersion + "\";" + newLine +
		"}" + newLine;

	text = text.Replace (namespaceLine, versionClass);
	text = Regex.Replace (text, "(public\\s+string\\s+Version\\s*\\{\\s*get;\\s*set;\\s*\\}\\s*=\\s*)\"[^\"]+\";", "${1}FirebasePackageVersions.DefaultFirebasePackageVersion;");
	text = Regex.Replace (text, "(AddPackage\\s*\\(\\s*requiredPackages\\s*,\\s*targetManifest\\.PackageId\\s*,\\s*)\"[^\"]+\"(\\s*\\)\\s*;)", "${1}FirebasePackageVersions.DefaultFirebasePackageVersion${2}");
	text = Regex.Replace (text, "(string\\.IsNullOrWhiteSpace\\s*\\(\\s*version\\s*\\)\\s*\\?\\s*)\"[^\"]+\"(\\s*:\\s*version)", "${1}FirebasePackageVersions.DefaultFirebasePackageVersion${2}");
	return text;
}

string UpdateFirebaseReleaseBindingSurfaceCoverageTests (string text)
{
	return Regex.Replace (
		text,
		"(new\\s+BindingSurfacePackageReference\\s*\\{\\s*Id\\s*=\\s*\"AdamE\\.Firebase\\.iOS\\.Core\"\\s*,\\s*Version\\s*=\\s*)\"[^\"]+\"(\\s*\\})",
		"${1}FirebasePackageVersions.DefaultFirebasePackageVersion${2}");
}

string UpdateFirebaseReleaseBindingSurfaceCoverageManifest (string text, string targetVersion)
{
	return Regex.Replace (
		text,
		"(\"id\"\\s*:\\s*\"AdamE\\.Firebase\\.iOS\\.[^\"]+\"\\s*,\\s*\\r?\\n\\s*\"version\"\\s*:\\s*\")([^\"]+)(\")",
		"${1}" + targetVersion + "${3}");
}

string UpdateFirebaseReleaseE2EProject (string text, string targetVersion)
{
	var newLine = text.Contains ("\r\n") ? "\r\n" : "\n";

	if (!text.Contains ("<FirebasePackageVersion>")) {
		var marker = "    <BindingSurfaceCoveragePropsPath></BindingSurfaceCoveragePropsPath>" + newLine;
		var insertion =
			"    <FirebasePackageVersion>" + targetVersion + "</FirebasePackageVersion>" + newLine +
			"    <GoogleAppMeasurementPackageVersion>" + targetVersion + "</GoogleAppMeasurementPackageVersion>" + newLine;
		text = text.Replace (marker, marker + insertion);
	} else {
		text = ReplaceFirebaseReleaseXmlElement (text, "FirebasePackageVersion", targetVersion);
		text = ReplaceFirebaseReleaseXmlElement (text, "GoogleAppMeasurementPackageVersion", targetVersion);
	}

	text = Regex.Replace (text, "(<PackageReference\\s+Include=\"AdamE\\.Firebase\\.iOS\\.[^\"]+\"\\s+Version=\")([^\"]+)(\")", "${1}$(FirebasePackageVersion)${3}");
	text = Regex.Replace (text, "(<PackageReference\\s+Include=\"AdamE\\.Google\\.iOS\\.GoogleAppMeasurement\"\\s+Version=\")([^\"]+)(\")", "${1}$(GoogleAppMeasurementPackageVersion)${3}");

	return text;
}

Dictionary<string, FirebaseReleaseNativeMetadata> ResolveFirebaseReleaseNativeMetadata (string firebaseVersion)
{
	var podFrameworkNames = GetFirebaseReleaseKnownPodFrameworkNames ();
	var metadataByFrameworkName = new Dictionary<string, FirebaseReleaseNativeMetadata> (StringComparer.Ordinal);

	foreach (var artifact in FIREBASE_RELEASE_ARTIFACT_KEYS.Select (key => ARTIFACTS [key])) {
		if (artifact.PodSpecs == null)
			continue;

		foreach (var podSpec in artifact.PodSpecs) {
			var metadata = ResolveFirebaseReleasePodSpecNativeMetadata (podSpec, firebaseVersion, podFrameworkNames);
			metadataByFrameworkName [metadata.FrameworkName] = metadata;

			if (!FIREBASE_RELEASE_NATIVE_FRAMEWORK_ALIASES.ContainsKey (metadata.FrameworkName))
				continue;

			foreach (var alias in FIREBASE_RELEASE_NATIVE_FRAMEWORK_ALIASES [metadata.FrameworkName])
				metadataByFrameworkName [alias] = new FirebaseReleaseNativeMetadata {
					FrameworkName = alias,
					Frameworks = metadata.Frameworks.ToList (),
					WeakFrameworks = metadata.WeakFrameworks.ToList (),
					LinkerFlags = metadata.LinkerFlags.ToList (),
				};
		}
	}

	return metadataByFrameworkName;
}

HashSet<string> GetFirebaseReleaseKnownPodFrameworkNames ()
{
	var result = new HashSet<string> (StringComparer.Ordinal);

	foreach (var artifact in ARTIFACTS.Values) {
		if (artifact.PodSpecs == null)
			continue;

		foreach (var podSpec in artifact.PodSpecs)
			result.Add (podSpec.FrameworkName);
	}

	foreach (var alias in FIREBASE_RELEASE_NATIVE_FRAMEWORK_ALIASES.SelectMany (pair => pair.Value))
		result.Add (alias);

	return result;
}

FirebaseReleaseNativeMetadata ResolveFirebaseReleasePodSpecNativeMetadata (PodSpec podSpec, string firebaseVersion, HashSet<string> podFrameworkNames)
{
	var podVersion = GetFirebaseReleasePodSpecVersion (podSpec, firebaseVersion);
	var specJson = RunPodSpecCatExact (podSpec.Name, podVersion);
	using (var document = JsonDocument.Parse (specJson)) {
		var metadata = new FirebaseReleaseNativeMetadata { FrameworkName = podSpec.FrameworkName };
		AddFirebaseReleaseNativeMetadataFromElement (metadata, document.RootElement);
		AddFirebaseReleaseNativeMetadataFromPlatformElement (metadata, document.RootElement);

		if (podSpec.SubSpecs != null && document.RootElement.TryGetProperty ("subspecs", out var subSpecs) && subSpecs.ValueKind == JsonValueKind.Array) {
			foreach (var subSpec in subSpecs.EnumerateArray ()) {
				if (!subSpec.TryGetProperty ("name", out var subSpecNameElement))
					continue;

				var subSpecName = subSpecNameElement.GetString ();
				if (!podSpec.SubSpecs.Contains (subSpecName))
					continue;

				AddFirebaseReleaseNativeMetadataFromElement (metadata, subSpec);
				AddFirebaseReleaseNativeMetadataFromPlatformElement (metadata, subSpec);
			}
		}

		RemoveFirebaseReleasePodFrameworkReferences (metadata, podFrameworkNames);
		return metadata;
	}
}

string GetFirebaseReleasePodSpecVersion (PodSpec podSpec, string firebaseVersion)
	=> FIREBASE_RELEASE_PODSPEC_NAMES.Contains (podSpec.Name)
		? firebaseVersion
		: podSpec.Version;

void AddFirebaseReleaseNativeMetadataFromPlatformElement (FirebaseReleaseNativeMetadata metadata, JsonElement element)
{
	if (element.TryGetProperty ("ios", out var iosElement) && iosElement.ValueKind == JsonValueKind.Object)
		AddFirebaseReleaseNativeMetadataFromElement (metadata, iosElement);
}

void AddFirebaseReleaseNativeMetadataFromElement (FirebaseReleaseNativeMetadata metadata, JsonElement element)
{
	AddFirebaseReleaseJsonStringOrArray (element, "frameworks", metadata.Frameworks);
	AddFirebaseReleaseJsonStringOrArray (element, "weak_frameworks", metadata.WeakFrameworks);
	AddFirebaseReleaseLibraries (element, "libraries", metadata.LinkerFlags);
	AddFirebaseReleaseXcconfigLinkerFlags (metadata, element, "pod_target_xcconfig");
	AddFirebaseReleaseXcconfigLinkerFlags (metadata, element, "user_target_xcconfig");
}

void AddFirebaseReleaseJsonStringOrArray (JsonElement element, string propertyName, List<string> values)
{
	if (!element.TryGetProperty (propertyName, out var property))
		return;

	foreach (var value in GetFirebaseReleaseJsonStringOrArrayValues (property))
		AddFirebaseReleaseUniqueToken (values, value);
}

void AddFirebaseReleaseLibraries (JsonElement element, string propertyName, List<string> values)
{
	if (!element.TryGetProperty (propertyName, out var property))
		return;

	foreach (var value in GetFirebaseReleaseJsonStringOrArrayValues (property))
		AddFirebaseReleaseUniqueToken (values, "-l" + value);
}

IEnumerable<string> GetFirebaseReleaseJsonStringOrArrayValues (JsonElement element)
{
	if (element.ValueKind == JsonValueKind.String)
		return SplitFirebaseReleaseNativeMetadataTokens (element.GetString ());

	if (element.ValueKind == JsonValueKind.Array) {
		var values = new List<string> ();
		foreach (var item in element.EnumerateArray ())
			if (item.ValueKind == JsonValueKind.String)
				values.AddRange (SplitFirebaseReleaseNativeMetadataTokens (item.GetString ()));
		return values;
	}

	return Enumerable.Empty<string> ();
}

List<string> SplitFirebaseReleaseNativeMetadataTokens (string value)
	=> string.IsNullOrWhiteSpace (value)
		? new List<string> ()
		: value.Split (new [] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList ();

void AddFirebaseReleaseXcconfigLinkerFlags (FirebaseReleaseNativeMetadata metadata, JsonElement element, string propertyName)
{
	if (!element.TryGetProperty (propertyName, out var xcconfig) || xcconfig.ValueKind != JsonValueKind.Object)
		return;

	if (!xcconfig.TryGetProperty ("OTHER_LDFLAGS", out var flagsElement) || flagsElement.ValueKind != JsonValueKind.String)
		return;

	var tokens = SplitFirebaseReleaseNativeMetadataTokens (flagsElement.GetString ());
	for (var i = 0; i < tokens.Count; i++) {
		var token = NormalizeFirebaseReleaseLinkerToken (tokens [i]);
		if (string.IsNullOrWhiteSpace (token) || token == "$(inherited)")
			continue;

		if (token == "-framework" && i + 1 < tokens.Count) {
			AddFirebaseReleaseUniqueToken (metadata.Frameworks, NormalizeFirebaseReleaseLinkerToken (tokens [++i]));
			continue;
		}

		if (token == "-weak_framework" && i + 1 < tokens.Count) {
			AddFirebaseReleaseUniqueToken (metadata.WeakFrameworks, NormalizeFirebaseReleaseLinkerToken (tokens [++i]));
			continue;
		}

		AddFirebaseReleaseUniqueToken (metadata.LinkerFlags, token);
	}
}

string NormalizeFirebaseReleaseLinkerToken (string value)
{
	if (string.IsNullOrWhiteSpace (value))
		return value;

	return value.Trim ().Trim ('"');
}

void RemoveFirebaseReleasePodFrameworkReferences (FirebaseReleaseNativeMetadata metadata, HashSet<string> podFrameworkNames)
{
	metadata.Frameworks = metadata.Frameworks.Where (framework => !podFrameworkNames.Contains (framework)).ToList ();
	metadata.WeakFrameworks = metadata.WeakFrameworks.Where (framework => !podFrameworkNames.Contains (framework)).ToList ();
}

void AddFirebaseReleaseUniqueToken (List<string> values, string value)
{
	if (string.IsNullOrWhiteSpace (value) || values.Contains (value))
		return;

	values.Add (value);
}

string ResolveFirebaseReleaseTagCommit (string version)
{
	var args = new ProcessArgumentBuilder ();
	args.Append ("ls-remote");
	args.Append ("--tags");
	args.Append ("https://github.com/firebase/firebase-ios-sdk.git");
	args.Append (version);

	var output = RunFirebaseReleaseProcess ("git", args, ".");
	var expectedRef = "refs/tags/" + version;
	foreach (var line in output.Split (new [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
		var parts = line.Split (new [] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 2 && parts [1] == expectedRef)
			return parts [0];
	}

	throw new Exception ($"Could not resolve firebase-ios-sdk tag {version}.");
}

Dictionary<string, FirebaseReleaseDownloadInfo> ResolveFirebaseReleaseExternalDownloads (string version)
{
	var result = new Dictionary<string, FirebaseReleaseDownloadInfo> ();

	foreach (var id in new [] { "FirebaseAnalytics", "GoogleAppMeasurement" }) {
		var specJson = RunPodSpecCatExact (id, version);
		using (var document = JsonDocument.Parse (specJson)) {
			var sourceUrl = document.RootElement.GetProperty ("source").GetProperty ("http").GetString ();
			var match = Regex.Match (sourceUrl, "/analytics/([^/]+)/" + Regex.Escape (id) + "-([^/]+)\\.tar\\.gz$");
			if (!match.Success)
				throw new Exception ($"Could not parse {id} source URL from podspec: {sourceUrl}");

			result [id] = new FirebaseReleaseDownloadInfo {
				Id = id,
				ArchiveKey = match.Groups [1].Value,
				Version = match.Groups [2].Value
			};
		}
	}

	return result;
}

string RunPodSpecCat (string podName, string version)
{
	var args = new ProcessArgumentBuilder ();
	args.Append ("spec");
	args.Append ("cat");
	args.Append (podName);
	args.Append ("--version=" + version);
	return RunFirebaseReleaseProcess ("pod", args, ".");
}

string RunPodSpecCatExact (string podName, string version)
{
	var args = new ProcessArgumentBuilder ();
	args.Append ("spec");
	args.Append ("cat");
	args.Append ("^" + Regex.Escape (podName) + "$");
	args.Append ("--regex");
	args.Append ("--version=" + version);
	return RunFirebaseReleaseProcess ("pod", args, ".");
}

void ValidateFirebaseReleaseResolvedPods (FirebaseReleasePodResolution podResolution)
{
	var mismatches = new List<string> ();

	foreach (var pair in FIREBASE_RELEASE_MANUAL_POD_PINS) {
		if (!podResolution.Versions.ContainsKey (pair.Key))
			continue;

		if (podResolution.Versions [pair.Key] != pair.Value)
			mismatches.Add ($"{pair.Key}: components.cake pins {pair.Value}, upstream resolves {podResolution.Versions [pair.Key]}");
	}

	if (mismatches.Count > 0)
		throw new Exception ("Firebase transitive pod pin drift detected:\n" + string.Join ("\n", mismatches));
}

void ValidateFirebaseReleaseResolvedPodSet (FirebaseReleasePodResolution podResolution)
{
	var expected = new HashSet<string> (FIREBASE_RELEASE_EXPECTED_RESOLVED_PODS, StringComparer.Ordinal);
	var added = podResolution.Names.Where (name => !expected.Contains (name)).OrderBy (name => name).ToArray ();
	var removed = expected.Where (name => !podResolution.Names.Contains (name)).OrderBy (name => name).ToArray ();

	if (added.Length == 0 && removed.Length == 0)
		return;

	var messages = new List<string> ();
	if (added.Length > 0)
		messages.Add ("Added upstream pod dependencies: " + string.Join (", ", added));
	if (removed.Length > 0)
		messages.Add ("Removed upstream pod dependencies: " + string.Join (", ", removed));

	throw new Exception ("Firebase dependency graph drift detected. Manual dependency mapping review required.\n" + string.Join ("\n", messages));
}

FirebaseReleasePodResolution ResolveFirebaseReleasePodfile (string version)
{
	var checkDirectory = new DirectoryPath ("output/firebase-release-check");
	if (DirectoryExists (checkDirectory))
		DeleteDirectory (checkDirectory, new DeleteDirectorySettings { Recursive = true, Force = true });

	EnsureDirectoryExists (checkDirectory);

	var podfileLines = new List<string> {
		"platform :ios, '15.0'",
		"install! 'cocoapods', :integrate_targets => false",
		"use_frameworks!",
		"target 'FirebaseReleaseCheck' do",
	};

	foreach (var pod in FIREBASE_RELEASE_RESOLUTION_PODS)
		podfileLines.Add ($"\tpod '{pod}', '{version}'");

	podfileLines.Add ("end");

	var podfilePath = checkDirectory.CombineWithFilePath ("Podfile");
	FileWriteLines (podfilePath, podfileLines.ToArray ());
	CocoaPodInstall (checkDirectory);

	var lockPath = MakeAbsolute (checkDirectory.CombineWithFilePath ("Podfile.lock"));
	if (!System.IO.File.Exists (lockPath.FullPath))
		throw new Exception ($"Expected CocoaPods lockfile at {lockPath}.");

	var versions = new Dictionary<string, string> (StringComparer.Ordinal);
	var names = new HashSet<string> (StringComparer.Ordinal);
	foreach (var line in System.IO.File.ReadAllLines (lockPath.FullPath)) {
		var match = Regex.Match (line, "^  -\\s+\"?([^/\\s(]+)(?:/[^\\s(]+)?\\s+\\(([^)]+)\\)\"?");
		if (!match.Success)
			continue;

		var podName = match.Groups [1].Value;
		names.Add (podName);
		if (!versions.ContainsKey (podName))
			versions [podName] = match.Groups [2].Value;
	}

	var podsDirectory = checkDirectory.Combine ("Pods");
	if (DirectoryExists (podsDirectory))
		DeleteDirectory (podsDirectory, new DeleteDirectorySettings { Recursive = true, Force = true });

	return new FirebaseReleasePodResolution {
		Versions = versions,
		Names = names
	};
}

string RunFirebaseReleaseProcess (string tool, ProcessArgumentBuilder args, DirectoryPath workingDirectory)
{
	var processSettings = new ProcessSettings {
		Arguments = args,
		WorkingDirectory = workingDirectory,
		RedirectStandardOutput = true,
		RedirectStandardError = true
	};

	using (var process = StartAndReturnProcess (tool, processSettings)) {
		process.WaitForExit ();

		var output = string.Join ("\n", process.GetStandardOutput ());
		var error = string.Join ("\n", process.GetStandardError ());

		if (process.GetExitCode () != 0)
			throw new Exception ($"{tool} failed with exit code {process.GetExitCode ()}.\n{error}");

		if (!string.IsNullOrWhiteSpace (error))
			Verbose (error);

		return output;
	}
}
