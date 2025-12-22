void FirebaseAnalyticsDownload ()
{
	var externalsPath = new DirectoryPath ("./externals");
	var archiveUrl = "https://dl.google.com/firebase/ios/analytics/1d0a9f91196548b3/FirebaseAnalytics-12.5.0.tar.gz";
	var archivePath = externalsPath.CombineWithFilePath ("FirebaseAnalytics-12.5.0.tar.gz");
	var extractionRoot = externalsPath.Combine ("FirebaseAnalytics-12.5.0");
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

	Information ("Downloading FirebaseAnalytics xcframework tarball...");
	DownloadFile (archiveUrl, archivePath);

	Information ("Extracting FirebaseAnalytics xcframework into externals...");
	var exitCode = StartProcess ("tar", $"-xzf \"{archivePath.FullPath}\" -C \"{externalsPath.FullPath}\"");

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
