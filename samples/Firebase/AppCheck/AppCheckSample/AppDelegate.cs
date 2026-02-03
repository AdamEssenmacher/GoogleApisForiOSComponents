using Xamarin.iOS.Shared.Helpers;

namespace AppCheckSample;

using FirebaseAppCheck = Firebase.AppCheck.AppCheck;
using FirebaseCoreApp = Firebase.Core.App;

[Register ("AppDelegate")]
public class AppDelegate : UIApplicationDelegate {
	public override UIWindow? Window {
		get;
		set;
	}

	public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
	{
		// create a new window instance based on the screen size
		Window = new UIWindow (UIScreen.MainScreen.Bounds);

		// You can download your GoogleService-Info.plist file following the next link:
		// https://firebase.google.com/docs/ios/setup
		if (!GoogleServiceInfoPlistHelper.FileExist ()) {
			Window = GoogleServiceInfoPlistHelper.CreateWindowWithFileNotFoundMessage ();
			return true;
		}

		FirebaseAppCheck.SetAppCheckProviderFactory (new Firebase.AppCheck.AppCheckDebugProviderFactory ());
		FirebaseCoreApp.Configure ();

		var vc = new UIViewController ();
		vc.View!.BackgroundColor = UIColor.White;

		var statusLabel = new UILabel (new CGRect (24, 140, Window!.Frame.Width - 48, 200)) {
			BackgroundColor = UIColor.White,
			TextColor = UIColor.Black,
			TextAlignment = UITextAlignment.Center,
			Lines = 0,
			Text = "Firebase App Check sample\nMode: Debug provider\nTap the button to fetch a token."
		};

		var tokenButton = UIButton.FromType (UIButtonType.System);
		tokenButton.Frame = new CGRect (24, 360, Window!.Frame.Width - 48, 48);
		tokenButton.SetTitle ("Fetch App Check Token", UIControlState.Normal);
		tokenButton.TouchUpInside += (_, _) => {
			statusLabel.Text = "Fetching App Check token...";
			FirebaseAppCheck.SharedInstance.TokenForcingRefresh (true, (token, error) => {
				UIApplication.SharedApplication.BeginInvokeOnMainThread (() => {
					if (error is not null) {
						statusLabel.Text = $"Token request failed:\n{error.LocalizedDescription}";
						return;
					}

					if (token is null || string.IsNullOrWhiteSpace (token.Token)) {
						statusLabel.Text = "Token request returned an empty response.";
						return;
					}

					var rawToken = token.Token;
					var preview = rawToken.Length > 20 ? $"{rawToken[..12]}...{rawToken[^8..]}" : rawToken;
					statusLabel.Text = $"Token OK\n{preview}\nExpires: {token.ExpirationDate}";
				});
			});
		};

		vc.View.AddSubview (statusLabel);
		vc.View.AddSubview (tokenButton);
		Window.RootViewController = vc;

		// make the window visible
		Window.MakeKeyAndVisible ();

		return true;
	}
}
