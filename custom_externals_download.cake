// The release plan fingerprints this file for every Firebase and Sign-In package, and the Cake
// metadata export records each download source's properties. Any change to this class, even
// dropping an option nothing uses, changes all of those packages' release inputs.
class ExternalDownloadSource
{
	// Most of these archives live under the Firebase analytics path, but Google publishes
	// other SDKs under different hosts/paths, so the prefix is overridable.
	const string DefaultUrlPrefix = "https://dl.google.com/firebase/ios/analytics";

	public string Id { get; }
	public string Version { get; }
	public string ArchiveKey { get; }
	public string UrlPrefix { get; }
	public string ExtractionRootName { get; }
	public string ExpectedSha256 { get; }

	public ExternalDownloadSource (
		string id,
		string version,
		string archiveKey,
		string urlPrefix = DefaultUrlPrefix,
		string extractionRootName = null,
		string expectedSha256 = null)
	{
		Id = id;
		Version = version;
		ArchiveKey = archiveKey;
		UrlPrefix = urlPrefix;
		ExtractionRootName = extractionRootName ?? $"{Id}-{Version}";
		ExpectedSha256 = expectedSha256;
	}

	public string ArchiveFileName => $"{Id}-{Version}.tar.gz";
	public string Url => $"{UrlPrefix}/{ArchiveKey}/{ArchiveFileName}";
}

// *.tar.gz URLs can be found in the podspecs (e.g., CocoaPods Specs repo paths), such as:
// FirebaseAnalytics: https://github.com/CocoaPods/Specs/tree/master/Specs/e/2/1/FirebaseAnalytics
// GoogleAppMeasurement: https://github.com/CocoaPods/Specs/tree/master/Specs/e/3/b/GoogleAppMeasurement
//
// Everything listed here is packed into a published nupkg, so only SDKs whose license permits
// redistribution belong here; both entries are declared Apache-2.0 in their podspecs. Proprietary
// SDKs such as Maps and Places stay on XamarinBuildDownload, so consumers fetch them from Google
// under Google's terms.
var ExternalDownloads = new Dictionary<string, ExternalDownloadSource> {
	{ "FirebaseAnalytics", new ExternalDownloadSource ("FirebaseAnalytics", "12.17.0", "5b4ffbefbb018b03", expectedSha256: "5b4ffbefbb018b034c6351126f53381aa63d340bafaf7f47a8e9bc096fb1fcbb") },
	{ "GoogleAppMeasurement", new ExternalDownloadSource ("GoogleAppMeasurement", "12.17.0", "5e80294989c75a36", expectedSha256: "5e80294989c75a365daaa9f5d0573bc13f9584e3523ce4fced57aad848f5fb3d") },
};

FilePath GetArchivePath (ExternalDownloadSource source, DirectoryPath externalsPath) =>
	externalsPath.CombineWithFilePath (source.ArchiveFileName);

DirectoryPath GetExtractionRoot (ExternalDownloadSource source, DirectoryPath externalsPath) =>
	externalsPath.Combine (source.ExtractionRootName);

void DownloadAndExtract (ExternalDownloadSource source, Func<bool> artifactsAlreadyPresent, Action<DirectoryPath, DirectoryPath, DeleteDirectorySettings> copyArtifacts)
{
	var externalsPath = new DirectoryPath ("./externals");

	if (artifactsAlreadyPresent ()) {
		Information ($"{source.Id} artifacts already available in externals. Skipping download.");
		return;
	}

	EnsureDirectoryExists (externalsPath);

	var archivePath = GetArchivePath (source, externalsPath);
	var extractionRoot = GetExtractionRoot (source, externalsPath);
	var deleteSettings = new DeleteDirectorySettings { Recursive = true, Force = true };

	if (DirectoryExists (extractionRoot))
		DeleteDirectory (extractionRoot, deleteSettings);

	if (FileExists (archivePath))
		DeleteFile (archivePath);

	DownloadArchive (source, archivePath);
	VerifyArchiveHash (source, archivePath);

	var exitCode = ExtractArchive (source, externalsPath, archivePath);

	if (exitCode != 0)
		throw new Exception ($"tar failed with exit code {exitCode} while extracting {archivePath.GetFilename ()}.");

	copyArtifacts (extractionRoot, externalsPath, deleteSettings);

	DeleteDirectory (extractionRoot, deleteSettings);
	DeleteFile (archivePath);
}

int ExtractArchive (ExternalDownloadSource source, DirectoryPath externalsPath, FilePath archivePath)
{
	Information ($"Extracting {source.ArchiveFileName} into externals...");
	return StartProcess ("tar", $"-xzf \"{archivePath.FullPath}\" -C \"{externalsPath.FullPath}\"");
}

void DownloadArchive (ExternalDownloadSource source, FilePath archivePath)
{
	Information ($"Downloading {source.ArchiveFileName}...");
	DownloadFile (source.Url, archivePath);
}

void VerifyArchiveHash (ExternalDownloadSource source, FilePath archivePath)
{
	if (string.IsNullOrWhiteSpace (source.ExpectedSha256))
		return;

	string actualSha256;
	using (var stream = System.IO.File.OpenRead (archivePath.FullPath))
	using (var sha256 = System.Security.Cryptography.SHA256.Create ())
		actualSha256 = BitConverter.ToString (sha256.ComputeHash (stream)).Replace ("-", "").ToLowerInvariant ();

	if (!string.Equals (actualSha256, source.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
		throw new Exception ($"SHA-256 mismatch for {source.ArchiveFileName}: expected {source.ExpectedSha256}, got {actualSha256}.");

	Information ($"Verified SHA-256 for {source.ArchiveFileName}.");
}

void FirebaseAnalyticsDownload ()
{
	var source = ExternalDownloads["FirebaseAnalytics"];

	DownloadAndExtract (
		source,
		() => DirectoryExists (new DirectoryPath ("./externals/FirebaseAnalytics.xcframework")),
		(extractionRoot, externalsPath, deleteSettings) => {
			var frameworkSource = extractionRoot.Combine ("Frameworks").Combine ("FirebaseAnalytics.xcframework");
			var frameworkDestination = externalsPath.Combine ("FirebaseAnalytics.xcframework");

			if (!DirectoryExists (frameworkSource))
				throw new Exception ($"Expected FirebaseAnalytics.xcframework at {frameworkSource} after extraction.");

			if (DirectoryExists (frameworkDestination))
				DeleteDirectory (frameworkDestination, deleteSettings);

			CopyDirectory (frameworkSource, frameworkDestination);
		});
}

void GoogleAppMeasurementDownload ()
{
	var source = ExternalDownloads["GoogleAppMeasurement"];

	DownloadAndExtract (
		source,
		() => DirectoryExists (new DirectoryPath ("./externals/GoogleAppMeasurementIdentitySupport.xcframework")) &&
		      DirectoryExists (new DirectoryPath ("./externals/GoogleAppMeasurement.xcframework")),
		(extractionRoot, externalsPath, deleteSettings) => {
			var frameworkSource = extractionRoot.Combine ("Frameworks");
			var identitySupportSource = frameworkSource.Combine ("GoogleAppMeasurementIdentitySupport.xcframework");
			var measurementSource = frameworkSource.Combine ("GoogleAppMeasurement.xcframework");
			var identitySupportDest = externalsPath.Combine ("GoogleAppMeasurementIdentitySupport.xcframework");
			var measurementDest = externalsPath.Combine ("GoogleAppMeasurement.xcframework");

			if (!DirectoryExists (identitySupportSource) || !DirectoryExists (measurementSource))
				throw new Exception ($"Expected GoogleAppMeasurement xcframeworks at {frameworkSource} after extraction.");

			if (DirectoryExists (identitySupportDest))
				DeleteDirectory (identitySupportDest, deleteSettings);

			if (DirectoryExists (measurementDest))
				DeleteDirectory (measurementDest, deleteSettings);

			CopyDirectory (identitySupportSource, identitySupportDest);
			CopyDirectory (measurementSource, measurementDest);
		});
}
