using UIKit;

namespace HikingApp.MacCatalyst.ViewControllers
{
    public class LoginViewController : UIViewController
    {
        UIButton appleSignInButton = new UIButton(UIButtonType.System);

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            View!.BackgroundColor = UIColor.White;

            appleSignInButton.SetTitle("Sign in with Apple ID", UIControlState.Normal);
            appleSignInButton.TitleLabel.Font = UIFont.BoldSystemFontOfSize(16);
            appleSignInButton.BackgroundColor = UIColor.Black;
            appleSignInButton.SetTitleColor(UIColor.White, UIControlState.Normal);
            var configuration = UIButtonConfiguration.PlainButtonConfiguration;
            configuration.ContentInsets = new NSDirectionalEdgeInsets(10, 20, 10, 20);
            appleSignInButton.Configuration = configuration;
            appleSignInButton.Layer.CornerRadius = 8;

            appleSignInButton.TranslatesAutoresizingMaskIntoConstraints = false;

            View.AddSubview(appleSignInButton);

            NSLayoutConstraint.ActivateConstraints(new NSLayoutConstraint[]
            {
                appleSignInButton.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
                appleSignInButton.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),
                appleSignInButton.LeadingAnchor.ConstraintGreaterThanOrEqualTo(View.LeadingAnchor, 20),
                appleSignInButton.TrailingAnchor.ConstraintLessThanOrEqualTo(View.TrailingAnchor, -20),
                appleSignInButton.WidthAnchor.ConstraintLessThanOrEqualTo(View.WidthAnchor, multiplier: 0.8f),
                appleSignInButton.HeightAnchor.ConstraintEqualTo(50)
            });

            appleSignInButton.TouchUpInside += AppleSignInButton_TouchUpInside;
        }

        private void AppleSignInButton_TouchUpInside(object? sender, EventArgs e)
        {
            var mainViewController = new MainViewController();
            // AppleID sign-in feature

            // Replace the RootViewController with MainViewController
            if (UIApplication.SharedApplication.Delegate is AppDelegate appDelegate)
            {
                appDelegate.Window!.RootViewController = mainViewController;
            }
        }
    }
}
