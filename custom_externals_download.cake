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
	{ "FirebaseAnalytics", new ExternalDownloadSource ("FirebaseAnalytics", "12.19.0", "f02e7923f9b32c38", expectedSha256: "f02e7923f9b32c381d309badd6b9366c09d1eb50bd4e9650a91ffa1eea53fc4e") },
	{ "GoogleAppMeasurement", new ExternalDownloadSource ("GoogleAppMeasurement", "12.19.0", "aa72db15c887f5f9", expectedSha256: "aa72db15c887f5f9f2c6169885a7d205fa49ee1c7099ee800e6dbf9dc47cbaee") },
};

FilePath GetArchivePath (ExternalDownloadSource source, DirectoryPath externalsPath) =>
	externalsPath.CombineWithFilePath (source.ArchiveFileName);

DirectoryPath GetExtractionRoot (ExternalDownloadSource source, DirectoryPath externalsPath) =>
	externalsPath.Combine (source.ExtractionRootName);

void DownloadAndExtract (ExternalDownloadSource source, string[] outputNames, Action<DirectoryPath, DirectoryPath, DeleteDirectorySettings> copyArtifacts)
{
	var externalsPath = MakeAbsolute (new DirectoryPath ("./externals"));

	var outputs = outputNames.Select (name => MakeAbsolute (externalsPath.Combine (name)).FullPath).ToArray ();
	var stamp = MakeAbsolute (externalsPath.CombineWithFilePath (source.Id + ".download-cache")).FullPath;
	var identity = string.Join ("\n", new [] { "native-download-v1", source.Id, source.Version,
		source.ArchiveKey, source.Url, source.ExpectedSha256, source.ExtractionRootName,
		string.Join ("\n", outputNames) });
	if (NativeCacheRecord.Matches (stamp, identity, outputs)) {
		Information ($"{source.Id} artifacts already available in externals. Skipping download.");
		return;
	}

	EnsureDirectoryExists (externalsPath);
	System.IO.File.Delete (stamp);

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

	// Compare the entire verified extraction with the copy, including all framework
	// slices and resource bundles. A partial copy must never publish a completion record.
	var expected = NativeCacheRecord.Inventory (outputNames.Select (name =>
		MakeAbsolute (extractionRoot.Combine ("Frameworks").Combine (name)).FullPath).ToArray ());
	copyArtifacts (extractionRoot, externalsPath, deleteSettings);
	if (NativeCacheRecord.Inventory (outputs) != expected)
		throw new Exception ($"Incomplete native copy for {source.Id}.");

	DeleteDirectory (extractionRoot, deleteSettings);
	DeleteFile (archivePath);
	NativeCacheRecord.Complete (stamp, identity, expected);
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
		new [] { "FirebaseAnalytics.xcframework" },
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
		new [] { "GoogleAppMeasurementIdentitySupport.xcframework", "GoogleAppMeasurement.xcframework" },
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
