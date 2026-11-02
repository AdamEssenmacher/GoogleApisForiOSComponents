using Foundation;
using UIKit;

namespace GoogleFoundationE2E;

// iOS 27 requires the UIScene lifecycle; the window and self-test start in SceneDelegate.
[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions) => true;
}
