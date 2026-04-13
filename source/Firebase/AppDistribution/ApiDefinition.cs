using System;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Firebase.AppDistribution
{
	// (void (^)(NSError *_Nullable error))completion
	delegate void ErrorHandler ([NullAllowed] NSError error);

	// (void (^)(FIRAppDistributionRelease *_Nullable_result release, NSError *_Nullable error))completion
	delegate void AppDistributionReleaseHandler ([NullAllowed] AppDistributionRelease release, [NullAllowed] NSError error);

	// @interface FIRAppDistribution : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "FIRAppDistribution")]
	interface AppDistribution
	{
		// NSString *const FIRAppDistributionErrorDomain
		[Field ("FIRAppDistributionErrorDomain", "__Internal")]
		NSString ErrorDomain { get; }

		// NSString *const FIRAppDistributionErrorDetailsKey
		[Field ("FIRAppDistributionErrorDetailsKey", "__Internal")]
		NSString ErrorDetailsKey { get; }

		// + (instancetype)appDistribution NS_SWIFT_NAME(appDistribution());
		[Static]
		[Export ("appDistribution")]
		AppDistribution SharedInstance { get; }

		// @property(nonatomic, readonly) BOOL isTesterSignedIn;
		[Export ("isTesterSignedIn")]
		bool IsTesterSignedIn { get; }

		// - (void)signInTesterWithCompletion:(void (^)(NSError *_Nullable error))completion NS_SWIFT_NAME(signInTester(completion:));
		[Export ("signInTesterWithCompletion:")]
		void SignInTester (ErrorHandler completion);

		// - (void)checkForUpdateWithCompletion:(void (^)(FIRAppDistributionRelease *_Nullable_result release, NSError *_Nullable error))completion NS_SWIFT_NAME(checkForUpdate(completion:));
		[Export ("checkForUpdateWithCompletion:")]
		void CheckForUpdate (AppDistributionReleaseHandler completion);

		// - (void)signOutTester;
		[Export ("signOutTester")]
		void SignOutTester ();

		// - (BOOL)application:(UIApplication *)application openURL:(NSURL *)url options:(NSDictionary<NSString *, id> *)options;
		[Export ("application:openURL:options:")]
		bool OpenUrl (UIApplication application, NSUrl url, NSDictionary<NSString, NSObject> options);
	}

	// @interface FIRAppDistributionRelease : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "FIRAppDistributionRelease")]
	interface AppDistributionRelease
	{
		// @property(nonatomic, copy, readonly) NSString *displayVersion;
		[Export ("displayVersion")]
		string DisplayVersion { get; }

		// @property(nonatomic, copy, readonly) NSString *buildVersion;
		[Export ("buildVersion")]
		string BuildVersion { get; }

		// @property(nonatomic, nullable, copy, readonly) NSString *releaseNotes;
		[NullAllowed]
		[Export ("releaseNotes")]
		string ReleaseNotes { get; }

		// @property(nonatomic, strong, readonly) NSURL *downloadURL;
		[Export ("downloadURL")]
		NSUrl DownloadUrl { get; }

		// @property(nonatomic, readonly) BOOL isExpired;
		[Export ("isExpired")]
		bool IsExpired { get; }
	}
}
