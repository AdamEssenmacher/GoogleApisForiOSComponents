class ExternalDownloadSource
{
	public string Id { get; }
	public string Version { get; }
	public string ArchiveKey { get; }

	public ExternalDownloadSource (string id, string version, string archiveKey)
	{
		Id = id;
		Version = version;
		ArchiveKey = archiveKey;
	}

	public string ArchiveFileName => $"{Id}-{Version}.tar.gz";
	public string ExtractionRootName => $"{Id}-{Version}";
	public string Url => $"https://dl.google.com/firebase/ios/analytics/{ArchiveKey}/{ArchiveFileName}";
}

var ExternalDownloads = new Dictionary<string, ExternalDownloadSource> {
	{ "FirebaseAnalytics", new ExternalDownloadSource ("FirebaseAnalytics", "12.5.0", "1d0a9f91196548b3") },
	{ "GoogleAppMeasurement", new ExternalDownloadSource ("GoogleAppMeasurement", "12.5.0", "4a8fa8d922b0b454") },
};

FilePath GetArchivePath (ExternalDownloadSource source, DirectoryPath externalsPath) =>
	externalsPath.CombineWithFilePath (source.ArchiveFileName);

DirectoryPath GetExtractionRoot (ExternalDownloadSource source, DirectoryPath externalsPath) =>
	externalsPath.Combine (source.ExtractionRootName);

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

void FirebaseAnalyticsDownload ()
{
	var externalsPath = new DirectoryPath ("./externals");
	var source = ExternalDownloads["FirebaseAnalytics"];
	var archivePath = GetArchivePath (source, externalsPath);
	var extractionRoot = GetExtractionRoot (source, externalsPath);
	var frameworkSource = extractionRoot.Combine ("Frameworks").Combine ("FirebaseAnalytics.xcframework");
	var frameworkDestination = externalsPath.Combine ("FirebaseAnalytics.xcframework");

	if (DirectoryExists (frameworkDestination)) {
		Information ("FirebaseAnalytics.xcframework already available in externals. Skipping download.");
		return;
	}

	EnsureDirectoryExists (externalsPath);

	var deleteSettings = new DeleteDirectorySettings { Recursive = true, Force = true };

	if (DirectoryExists (extractionRoot))
		DeleteDirectory (extractionRoot, deleteSettings);

	if (FileExists (archivePath))
		DeleteFile (archivePath);

	DownloadArchive (source, archivePath);

	var exitCode = ExtractArchive (source, externalsPath, archivePath);

	if (exitCode != 0)
		throw new Exception ($"tar failed with exit code {exitCode} while extracting {archivePath.GetFilename ()}.");

	if (!DirectoryExists (frameworkSource))
		throw new Exception ($"Expected FirebaseAnalytics.xcframework at {frameworkSource} after extraction.");

	if (DirectoryExists (frameworkDestination))
		DeleteDirectory (frameworkDestination, deleteSettings);

	CopyDirectory (frameworkSource, frameworkDestination);

	DeleteDirectory (extractionRoot, deleteSettings);
	DeleteFile (archivePath);
}

void GoogleAppMeasurementDownload ()
{
	var externalsPath = new DirectoryPath ("./externals");
	var source = ExternalDownloads["GoogleAppMeasurement"];
	var archivePath = GetArchivePath (source, externalsPath);
	var extractionRoot = GetExtractionRoot (source, externalsPath);
	var frameworkSource = extractionRoot.Combine ("Frameworks");
	var identitySupportSource = frameworkSource.Combine ("GoogleAppMeasurementIdentitySupport.xcframework");
	var measurementSource = frameworkSource.Combine ("GoogleAppMeasurement.xcframework");
	var identitySupportDest = externalsPath.Combine ("GoogleAppMeasurementIdentitySupport.xcframework");
	var measurementDest = externalsPath.Combine ("GoogleAppMeasurement.xcframework");

	if (DirectoryExists (identitySupportDest) && DirectoryExists (measurementDest)) {
		Information ("GoogleAppMeasurement xcframeworks already available in externals. Skipping download.");
		return;
	}

	EnsureDirectoryExists (externalsPath);

	var deleteSettings = new DeleteDirectorySettings { Recursive = true, Force = true };

	if (DirectoryExists (extractionRoot))
		DeleteDirectory (extractionRoot, deleteSettings);

	if (FileExists (archivePath))
		DeleteFile (archivePath);

	DownloadArchive (source, archivePath);

	var exitCode = ExtractArchive (source, externalsPath, archivePath);

	if (exitCode != 0)
		throw new Exception ($"tar failed with exit code {exitCode} while extracting {archivePath.GetFilename ()}.");

	if (!DirectoryExists (identitySupportSource) || !DirectoryExists (measurementSource))
		throw new Exception ($"Expected GoogleAppMeasurement xcframeworks at {frameworkSource} after extraction.");

	if (DirectoryExists (identitySupportDest))
		DeleteDirectory (identitySupportDest, deleteSettings);

	if (DirectoryExists (measurementDest))
		DeleteDirectory (measurementDest, deleteSettings);

	CopyDirectory (identitySupportSource, identitySupportDest);
	CopyDirectory (measurementSource, measurementDest);

	DeleteDirectory (extractionRoot, deleteSettings);
	DeleteFile (archivePath);
}
