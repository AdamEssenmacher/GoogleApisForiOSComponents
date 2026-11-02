using Foundation;
using UIKit;

namespace GoogleFoundationE2E;

[Register("SceneDelegate")]
public sealed class SceneDelegate : UIWindowSceneDelegate
{
    public override UIWindow? Window { get; set; }

    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene)
            return;

        var statusViewController = new StatusViewController();
        Window = new UIWindow(windowScene) { RootViewController = statusViewController };
        Window.MakeKeyAndVisible();
        statusViewController.LoadViewIfNeeded();

        _ = GoogleSelfTestRunner.RunAsync(statusViewController);
    }
}
