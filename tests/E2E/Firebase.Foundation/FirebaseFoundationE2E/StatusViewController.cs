using UIKit;

namespace FirebaseFoundationE2E;

public sealed class StatusViewController : UIViewController
{
    UITextView? textView;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        View!.BackgroundColor = UIColor.SystemBackground;

        textView = new UITextView(View.Bounds)
        {
            Editable = false,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            BackgroundColor = UIColor.SystemBackground,
            TextColor = UIColor.Label,
            Font = UIFont.FromName("Menlo-Regular", 15) ?? UIFont.SystemFontOfSize(15),
            TextContainerInset = new UIEdgeInsets(24, 20, 24, 20),
            Text = "Firebase NuGet E2E harness starting..." + Environment.NewLine
        };

        View.AddSubview(textView);
    }

    public Task AppendLineAsync(string line)
    {
        InvokeOnMainThread(() =>
        {
            if (textView is null)
            {
                return;
            }

            var prefix = string.IsNullOrEmpty(textView.Text) ? string.Empty : Environment.NewLine;
            textView.Text += prefix + line;
            var end = new CoreGraphics.CGPoint(0, Math.Max(0, textView.ContentSize.Height - textView.Bounds.Height));
            textView.SetContentOffset(end, false);
        });

        return Task.CompletedTask;
    }
}
