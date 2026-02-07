namespace AppCheckSample;

using FirebaseAppCheck = Firebase.AppCheck.AppCheck;
using FirebaseCoreApp = Firebase.Core.App;
using Xamarin.iOS.Shared.Helpers;
using Xamarin.iOS.Shared.ViewControllers;

[Register ("AppDelegate")]
public class AppDelegate : UIApplicationDelegate {
	const string SelectedModeKey = "SelectedAppCheckMode";
	static bool IsFirebaseConfigured { get; set; }

	public override bool FinishedLaunching (UIApplication application, NSDictionary? launchOptions)
	{
		if (GoogleServiceInfoPlistHelper.FileExist () && !IsFirebaseConfigured) {
			// Configure Firebase with the selected AppCheck provider
			var selectedMode = NSUserDefaults.StandardUserDefaults.StringForKey (SelectedModeKey);
			
			if (!string.IsNullOrEmpty (selectedMode)) {
				ConfigureFirebaseWithMode (selectedMode);
			}
			// If no mode selected yet, Firebase will be configured after user selection
		}

		return true;
	}

	static void ConfigureFirebaseWithMode (string mode)
	{
		switch (mode) {
			case "Debug":
				FirebaseAppCheck.SetAppCheckProviderFactory (new Firebase.AppCheck.AppCheckDebugProviderFactory ());
				break;
			case "Device Check":
				FirebaseAppCheck.SetAppCheckProviderFactory (new Firebase.AppCheck.DeviceCheckProviderFactory ());
				break;
			case "App Attest":
				// Note: AppAttestProviderFactory is not exposed in current bindings, using Device Check as fallback
				FirebaseAppCheck.SetAppCheckProviderFactory (new Firebase.AppCheck.DeviceCheckProviderFactory ());
				break;
			case "Disabled":
				// Don't set any provider
				break;
		}

		FirebaseCoreApp.Configure ();
		IsFirebaseConfigured = true;
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
		if (!GoogleServiceInfoPlistHelper.FileExist ()) {
			return new GoogleServiceInfoPlistNotFoundViewController ();
		}

		var selectedMode = NSUserDefaults.StandardUserDefaults.StringForKey (SelectedModeKey);
		
		// If no mode selected yet, show selection screen
		if (string.IsNullOrEmpty (selectedMode)) {
			return new UINavigationController (CreateModeSelectionViewController ());
		}

		// Otherwise show the token test screen
		return new UINavigationController (CreateTokenViewController (selectedMode));
	}

	static UIViewController CreateModeSelectionViewController ()
	{
		var viewController = new UIViewController ();
		viewController.View!.BackgroundColor = UIColor.White;
		viewController.Title = "Select AppCheck Mode";

		var headerLabel = new UILabel {
			BackgroundColor = UIColor.White,
			TextColor = UIColor.Black,
			TextAlignment = UITextAlignment.Center,
			Lines = 0,
			Font = UIFont.BoldSystemFontOfSize (18),
			TranslatesAutoresizingMaskIntoConstraints = false,
			Text = "Choose an App Check mode to test.\n\nFirebase will be configured with your selection.\n\nTo test another mode, use the Reset button."
		};

		var stackView = new UIStackView {
			Axis = UILayoutConstraintAxis.Vertical,
			Spacing = 16,
			TranslatesAutoresizingMaskIntoConstraints = false,
			Alignment = UIStackViewAlignment.Fill,
			Distribution = UIStackViewDistribution.FillEqually
		};

		var modes = new[] { "Disabled", "Debug", "Device Check", "App Attest" };
		foreach (var mode in modes) {
			var button = UIButton.FromType (UIButtonType.System);
			button.SetTitle (mode, UIControlState.Normal);
			button.TitleLabel!.Font = UIFont.SystemFontOfSize (16);
			button.TranslatesAutoresizingMaskIntoConstraints = false;
			button.TouchUpInside += (_, _) => {
				// Save selection
				NSUserDefaults.StandardUserDefaults.SetString (mode, SelectedModeKey);
				NSUserDefaults.StandardUserDefaults.Synchronize ();

				// Configure Firebase if not already done
				if (!IsFirebaseConfigured) {
					ConfigureFirebaseWithMode (mode);
				}

				// Navigate to token test screen
				var tokenVC = CreateTokenViewController (mode);
				viewController.NavigationController?.PushViewController (tokenVC, true);
			};
			stackView.AddArrangedSubview (button);
		}

		viewController.View.AddSubview (headerLabel);
		viewController.View.AddSubview (stackView);

		NSLayoutConstraint.ActivateConstraints (new [] {
			headerLabel.LeadingAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.LeadingAnchor, 24),
			headerLabel.TrailingAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.TrailingAnchor, -24),
			headerLabel.TopAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.TopAnchor, 40),
			
			stackView.LeadingAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.LeadingAnchor, 40),
			stackView.TrailingAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.TrailingAnchor, -40),
			stackView.TopAnchor.ConstraintEqualTo (headerLabel.BottomAnchor, 40),
			stackView.BottomAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.BottomAnchor, -120)
		});

		return viewController;
	}

	static UIViewController CreateTokenViewController (string mode)
	{
		var viewController = new UIViewController ();
		viewController.View!.BackgroundColor = UIColor.White;
		viewController.Title = $"AppCheck: {mode}";

		var statusLabel = new UILabel {
			BackgroundColor = UIColor.White,
			TextColor = UIColor.Black,
			TextAlignment = UITextAlignment.Center,
			Lines = 0,
			TranslatesAutoresizingMaskIntoConstraints = false,
			Text = mode == "App Attest" 
				? $"Mode: {mode}\n(using Device Check as fallback)\n\nTap the button to fetch a token."
				: mode == "Disabled"
				? "Mode: Disabled\n\nApp Check is not configured.\nNo provider factory was set."
				: $"Mode: {mode}\n\nTap the button to fetch a token."
		};

		UIButton? tokenButton = null;
		if (mode != "Disabled") {
			tokenButton = UIButton.FromType (UIButtonType.System);
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
		}

		var resetButton = UIButton.FromType (UIButtonType.System);
		resetButton.SetTitle ("Reset Mode (Restart Required)", UIControlState.Normal);
		resetButton.SetTitleColor (UIColor.Red, UIControlState.Normal);
		resetButton.TranslatesAutoresizingMaskIntoConstraints = false;
		resetButton.TouchUpInside += (_, _) => {
			var alert = UIAlertController.Create (
				"Reset App Check Mode",
				"This will clear the selected mode. You must restart the app to select a new mode.\n\nNote: Firebase can only be configured once per app session.",
				UIAlertControllerStyle.Alert
			);
			alert.AddAction (UIAlertAction.Create ("Cancel", UIAlertActionStyle.Cancel, null));
			alert.AddAction (UIAlertAction.Create ("Reset & Exit", UIAlertActionStyle.Destructive, _ => {
				NSUserDefaults.StandardUserDefaults.RemoveObject (SelectedModeKey);
				NSUserDefaults.StandardUserDefaults.Synchronize ();
				
				// Exit the app (user must manually restart)
				var exitAlert = UIAlertController.Create (
					"Mode Reset",
					"Please restart the app to select a new mode.",
					UIAlertControllerStyle.Alert
				);
				exitAlert.AddAction (UIAlertAction.Create ("OK", UIAlertActionStyle.Default, _ => {
					// iOS doesn't allow programmatic exit, but we can show this message
				}));
				viewController.PresentViewController (exitAlert, true, null);
			}));
			viewController.PresentViewController (alert, true, null);
		};

		viewController.View.AddSubview (statusLabel);
		if (tokenButton is not null) {
			viewController.View.AddSubview (tokenButton);
		}
		viewController.View.AddSubview (resetButton);

		var constraints = new List<NSLayoutConstraint> {
			statusLabel.LeadingAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.LeadingAnchor, 24),
			statusLabel.TrailingAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.TrailingAnchor, -24),
			statusLabel.TopAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.TopAnchor, 80)
		};

		if (tokenButton is not null) {
			constraints.AddRange (new [] {
				tokenButton.TopAnchor.ConstraintEqualTo (statusLabel.BottomAnchor, 32),
				tokenButton.CenterXAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.CenterXAnchor)
			});
			constraints.Add (resetButton.TopAnchor.ConstraintEqualTo (tokenButton.BottomAnchor, 80));
		} else {
			constraints.Add (resetButton.TopAnchor.ConstraintEqualTo (statusLabel.BottomAnchor, 80));
		}

		constraints.Add (resetButton.CenterXAnchor.ConstraintEqualTo (viewController.View.SafeAreaLayoutGuide.CenterXAnchor));

		NSLayoutConstraint.ActivateConstraints (constraints.ToArray ());

		return viewController;
	}
}
