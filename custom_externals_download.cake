class ExternalDownloadSource
{
	public string Id { get; }
	public string Version { get; }
	public string ArchiveKey { get; }
	public string ExpectedSha256 { get; }

	public ExternalDownloadSource (string id, string version, string archiveKey, string expectedSha256 = null)
	{
		Id = id;
		Version = version;
		ArchiveKey = archiveKey;
		ExpectedSha256 = expectedSha256;
	}

	public string ArchiveFileName => $"{Id}-{Version}.tar.gz";
	public string ExtractionRootName => $"{Id}-{Version}";
	public string Url => $"https://dl.google.com/firebase/ios/analytics/{ArchiveKey}/{ArchiveFileName}";
}

// *.tar.gz URLs can be found in the podspecs (e.g., CocoaPods Specs repo paths), such as:
// FirebaseAnalytics: https://github.com/CocoaPods/Specs/tree/master/Specs/e/2/1/FirebaseAnalytics
// GoogleAppMeasurement: https://github.com/CocoaPods/Specs/tree/master/Specs/e/3/b/GoogleAppMeasurement
//
// Everything listed here is packed into a published nupkg, so only SDKs whose license permits
// redistribution belong here; both entries are declared Apache-2.0 in their podspecs. Proprietary
// SDKs such as Maps and Places stay on XamarinBuildDownload, so consumers fetch them from Google
// under Google's terms (scripts/check-xbd-package.py enforces this for those two).
var ExternalDownloads = new Dictionary<string, ExternalDownloadSource> {
	{ "FirebaseAnalytics", new ExternalDownloadSource ("FirebaseAnalytics", "12.16.0", "fde230ef9469d037", expectedSha256: "fde230ef9469d037bbbc0861efa7c715ed193c94fcbc407c7bbdc344f5619a70") },
	{ "GoogleAppMeasurement", new ExternalDownloadSource ("GoogleAppMeasurement", "12.16.0", "55d011d9e0f71e0a", expectedSha256: "55d011d9e0f71e0a89b100129a3d1ba5b0838ca452250cb4766cfe17fbc81c6c") },
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
