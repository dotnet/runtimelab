// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UIKit;

namespace HikingApp.MacCatalyst.ViewControllers
{
    public class MockAppleSignInService
    {
        // TODO: Replace with proper AppleID sign-in service implementation
        public async Task<bool> SignInAsync()
        {
            // Simulate a delay for the sign-in process
            await Task.Delay(1000);
            // Simulate a successful sign-in
            return true;
        }
    }
    
    public class LoginViewController : UIViewController
    {
        UIButton appleSignInButton = new UIButton(UIButtonType.System);
        UIActivityIndicatorView activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large);

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            ArgumentNullException.ThrowIfNull(View);
            
            View.BackgroundColor = UIColor.White;

            // Create a button for Apple ID sign-in
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

            // Configure activity indicator
            activityIndicator.TranslatesAutoresizingMaskIntoConstraints = false;
            activityIndicator.HidesWhenStopped = true;
            View.AddSubview(activityIndicator);

            NSLayoutConstraint.ActivateConstraints(new NSLayoutConstraint[]
            {
                appleSignInButton.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
                appleSignInButton.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),
                appleSignInButton.LeadingAnchor.ConstraintGreaterThanOrEqualTo(View.LeadingAnchor, 20),
                appleSignInButton.TrailingAnchor.ConstraintLessThanOrEqualTo(View.TrailingAnchor, -20),
                appleSignInButton.WidthAnchor.ConstraintLessThanOrEqualTo(View.WidthAnchor, multiplier: 0.8f),
                appleSignInButton.HeightAnchor.ConstraintEqualTo(50),

                activityIndicator.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
                activityIndicator.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor)
            });

            appleSignInButton.TouchUpInside += AppleSignInButton_TouchUpInside;
        }

        private async void AppleSignInButton_TouchUpInside(object? sender, EventArgs e)
        {
            // Start the activity indicator animation
            appleSignInButton.Enabled = false;
            appleSignInButton.Hidden = true;
            activityIndicator.StartAnimating();

            var appleSignInService = new MockAppleSignInService();
            bool signInSuccess = await appleSignInService.SignInAsync();

            // Stop the activity indicator animation
            activityIndicator.StopAnimating();
            appleSignInButton.Enabled = true;
            appleSignInButton.Hidden = false;

            if (signInSuccess)
            {
                var mainViewController = new MainViewController();

                // Replace the RootViewController with MainViewController
                if (UIApplication.SharedApplication.Delegate is AppDelegate appDelegate)
                {
                    appDelegate.Window!.RootViewController = mainViewController;
                }
            }
            else
            {
                // Handle sign-in failure
                Console.WriteLine("AppleID sign-in failed.");
            }
        }
    }
}
