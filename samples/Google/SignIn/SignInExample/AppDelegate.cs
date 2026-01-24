using System;
using Foundation;
using UIKit;
using Google.SignIn;

namespace SignInExample
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the
	// User Interface of the application, as well as listening (and optionally responding) to application events from iOS.
	[Register ("AppDelegate")]
	public class AppDelegate : UIApplicationDelegate
	{
		// class-level declarations

		public override UIWindow? Window {
			get;
			set;
		}

		public override bool FinishedLaunching (UIApplication application, NSDictionary? launchOptions)
		{
			// For a runnable sample, add your own GoogleService-Info.plist (not committed) and set CLIENT_ID.
			var googleServiceInfoPath = NSBundle.MainBundle.PathForResource ("GoogleService-Info", "plist");
			var googleServiceDictionary = !string.IsNullOrWhiteSpace (googleServiceInfoPath)
				? NSMutableDictionary.FromFile (googleServiceInfoPath)
				: null;

			var clientId = googleServiceDictionary?["CLIENT_ID"]?.ToString ();

			if (!string.IsNullOrWhiteSpace (clientId))
				SignIn.SharedInstance.Configuration = new Configuration (clientId);

			return true;
		}

		// For iOS 9 or newer
		public override bool OpenUrl (UIApplication app, NSUrl url, NSDictionary options)
		{
			var openUrlOptions = new UIApplicationOpenUrlOptions (options);
			return SignIn.SharedInstance.HandleUrl (url);
		}

		// For iOS 8 and older
		public override bool OpenUrl (UIApplication application, NSUrl url, string sourceApplication, NSObject annotation)
		{
			return SignIn.SharedInstance.HandleUrl (url);
		}
	}
}
