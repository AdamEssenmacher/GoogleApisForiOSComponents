﻿using System;

using UIKit;
using Foundation;

using Google.Cast;

namespace CastSample
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the
	// User Interface of the application, as well as listening (and optionally responding) to application events from iOS.
	[Register ("AppDelegate")]
	public class AppDelegate : UIApplicationDelegate, ILoggerDelegate
	{
		// class-level declarations

		// You can add your own app id here that you get by registering
		// with the Google Cast SDK Developer Console https://cast.google.com/publish
		public static readonly string ReceiverApplicationId = "CC1AD845";

		public override UIWindow Window {
			get;
			set;
		}

		#region App Life Cycle

		public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
		{
			// Override point for customization after application launch.
			// If not required for your application you can safely delete this method
			UIApplication.SharedApplication.StatusBarStyle = UIStatusBarStyle.LightContent;

			// Contains options that affect the behavior of the framework.
			var discoveryCreiteria = new DiscoveryCriteria (ReceiverApplicationId);
			var options = new CastOptions (discoveryCreiteria);

			// CastContext coordinates all of the framework's activities.
			CastContext.SetSharedInstance (options);

			// Google Cast Logger
			Logger.SharedInstance.Delegate = this;

			// Use UICastContainerViewController as the Initial Controller.
			// Wraps our View Controllers and add a UIMiniMediaControlsViewController
			// at the bottom; a persistent bar to control remote videos.
			var appStoryboard = UIStoryboard.FromName ("Main", null);
			var navigationController = appStoryboard.InstantiateInitialViewController () as UINavigationController;
			var castContainer = CastContext.SharedInstance.CreateCastContainerController (navigationController);
			castContainer.MiniMediaControlsItemEnabled = true;

			// Use Default Expanded Media Controls
			CastContext.SharedInstance.UseDefaultExpandedMediaControls = true;

			Window = new UIWindow (UIScreen.MainScreen.Bounds);
			Window.RootViewController = castContainer;
			Window.MakeKeyAndVisible ();

			return true;
		}

		#endregion

		#region Logger Delegate

		[Export ("logMessage:fromFunction:")]
		void LogMessage (string message, string function)
		{
			Console.WriteLine ($"{function} {message}");
		}

		#endregion

		// Property to control the visibility of the mini controller.
		public bool CastControlBarsEnabled {
			get {
				var castContainer = Window.RootViewController as UICastContainerViewController;
				return castContainer.MiniMediaControlsItemEnabled;
			}
			set {
				var castContainer = Window.RootViewController as UICastContainerViewController;
				castContainer.MiniMediaControlsItemEnabled = value;
			}
		}
	}
}

