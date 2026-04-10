using Foundation;
using UIKit;

namespace FirebaseFoundationE2E;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
#pragma warning disable CA1422
        Window = new UIWindow(UIScreen.MainScreen.Bounds);
#pragma warning restore CA1422

        var statusViewController = new StatusViewController();
        Window.RootViewController = statusViewController;
        Window.MakeKeyAndVisible();
        statusViewController.LoadViewIfNeeded();

        _ = FirebaseSelfTestRunner.RunAsync(statusViewController);

        return true;
    }
}
