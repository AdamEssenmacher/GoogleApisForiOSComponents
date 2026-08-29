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
// GoogleMaps: https://github.com/CocoaPods/Specs/tree/master/Specs/1/9/0/GoogleMaps
// GooglePlaces: https://github.com/CocoaPods/Specs/tree/master/Specs/c/3/2/GooglePlaces
var ExternalDownloads = new Dictionary<string, ExternalDownloadSource> {
	{ "FirebaseAnalytics", new ExternalDownloadSource ("FirebaseAnalytics", "12.10.0", "3c185b45848d98d8") },
	{ "GoogleAppMeasurement", new ExternalDownloadSource ("GoogleAppMeasurement", "12.10.0", "5f5e4d8cb469941e") },
	{ "GoogleMaps", new ExternalDownloadSource (
		"GoogleMaps",
		"9.2.0",
		"33a7ac549361ab23",
		"https://dl.google.com/dl/cpdc",
		extractionRootName: "Maps",
		expectedSha256: "81bbd92c2d627087ae222ae955e5f746590812d7389b9d800add15e4004b6431") },
	{ "GooglePlaces", new ExternalDownloadSource ("GooglePlaces", "7.4.0", "3e8dc2602895d53405d075ff4eb569bff93ff1af97e69915d1e657c07ef28dd8", "https://dl.google.com/dl/geosdk") },
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

void GooglePlacesDownload ()
{
	var source = ExternalDownloads["GooglePlaces"];

	DownloadAndExtract (
		source,
		() => DirectoryExists (new DirectoryPath ("./externals/GooglePlaces.xcframework")),
		(extractionRoot, externalsPath, deleteSettings) => {
			var frameworkSource = extractionRoot.Combine ("Frameworks").Combine ("GooglePlaces.xcframework");
			var frameworkDestination = externalsPath.Combine ("GooglePlaces.xcframework");

			if (!DirectoryExists (frameworkSource))
				throw new Exception ($"Expected GooglePlaces.xcframework at {frameworkSource} after extraction.");

			if (DirectoryExists (frameworkDestination))
				DeleteDirectory (frameworkDestination, deleteSettings);

			CopyDirectory (frameworkSource, frameworkDestination);
		});
}

void GoogleMapsDownload ()
{
	var source = ExternalDownloads["GoogleMaps"];

	DownloadAndExtract (
		source,
		() => DirectoryExists (new DirectoryPath ("./externals/GoogleMaps.xcframework")) &&
		      DirectoryExists (new DirectoryPath ("./externals/GoogleMaps.bundle")),
		(extractionRoot, externalsPath, deleteSettings) => {
			var frameworkSource = extractionRoot.Combine ("Frameworks").Combine ("GoogleMaps.xcframework");
			var resourceSource = extractionRoot.Combine ("Resources").Combine ("GoogleMapsResources").Combine ("GoogleMaps.bundle");
			var frameworkDestination = externalsPath.Combine ("GoogleMaps.xcframework");
			var resourceDestination = externalsPath.Combine ("GoogleMaps.bundle");

			if (!DirectoryExists (frameworkSource))
				throw new Exception ($"Expected GoogleMaps.xcframework at {frameworkSource} after extraction.");

			if (!DirectoryExists (resourceSource))
				throw new Exception ($"Expected GoogleMaps.bundle at {resourceSource} after extraction.");

			if (DirectoryExists (frameworkDestination))
				DeleteDirectory (frameworkDestination, deleteSettings);

			if (DirectoryExists (resourceDestination))
				DeleteDirectory (resourceDestination, deleteSettings);

			CopyDirectory (frameworkSource, frameworkDestination);
			CopyDirectory (resourceSource, resourceDestination);
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
