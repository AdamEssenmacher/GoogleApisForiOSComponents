namespace AppCheckSample;

using FirebaseAppCheck = Firebase.AppCheck.AppCheck;
using FirebaseCoreApp = Firebase.Core.App;
using Xamarin.iOS.Shared.Helpers;
using Xamarin.iOS.Shared.ViewControllers;

[Register ("AppDelegate")]
public class AppDelegate : UIApplicationDelegate {
	static bool IsFirebaseConfigured { get; set; }

	public override bool FinishedLaunching (UIApplication application, NSDictionary? launchOptions)
	{
		if (GoogleServiceInfoPlistHelper.FileExist () && !IsFirebaseConfigured) {
			FirebaseAppCheck.SetAppCheckProviderFactory (new Firebase.AppCheck.AppCheckDebugProviderFactory ());
			FirebaseCoreApp.Configure ();
			IsFirebaseConfigured = true;
		}

		return true;
	}

	public override UISceneConfiguration GetConfiguration (
		UIApplication application,
		UISceneSession connectingSceneSession,
		UISceneConnectionOptions options)
	{
		return UISceneConfiguration.Create ("Default Configuration", connectingSceneSession.Role);
	}

	internal static UIViewController CreateRootViewController ()
	{
		return GoogleServiceInfoPlistHelper.FileExist ()
			? CreateTokenViewController ()
			: new GoogleServiceInfoPlistNotFoundViewController ();
	}

	static UIViewController CreateTokenViewController ()
	{
		var viewController = new UIViewController ();
		viewController.View!.BackgroundColor = UIColor.White;

		var statusLabel = new UILabel {
			BackgroundColor = UIColor.White,
			TextColor = UIColor.Black,
			TextAlignment = UITextAlignment.Center,
			Lines = 0,
			TranslatesAutoresizingMaskIntoConstraints = false,
			Text = "Firebase App Check sample\nMode: Debug provider\nTap the button to fetch a token."
		};

		var tokenButton = UIButton.FromType (UIButtonType.System);
		tokenButton.SetTitle ("Fetch App Check Token", UIControlState.Normal);
		tokenButton.TranslatesAutoresizingMaskIntoConstraints = false;
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

		viewController.View.AddSubview (statusLabel);
		viewController.View.AddSubview (tokenButton);

		NSLayoutConstraint.ActivateConstraints (new [] {
			statusLabel.LeadingAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.LeadingAnchor, 24),
			statusLabel.TrailingAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.TrailingAnchor, -24),
			statusLabel.TopAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.TopAnchor, 120),
			tokenButton.TopAnchor.ConstraintEqualTo (statusLabel.BottomAnchor, 24),
			tokenButton.CenterXAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.CenterXAnchor)
		});

		return viewController;
	}
}
