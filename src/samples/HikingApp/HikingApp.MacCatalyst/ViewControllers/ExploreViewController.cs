// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace HikingApp.MacCatalyst.ViewControllers;

internal class ExploreViewController : UIViewController
{
    public ObservableCollection<ViewControllers.TrailViewController> AllTrails { get;  }

    public ExploreViewController()
    {
        AllTrails = new ObservableCollection<ViewControllers.TrailViewController>(Models.Trail.GetMockTrailData(5).Select(trail => new ViewControllers.TrailViewController(trail)));

        View!.BackgroundColor = UIColor.FromRGB(240, 240, 240);
    }


     public override void ViewDidLoad ()
    {
        base.ViewDidLoad();
        ArgumentNullException.ThrowIfNull(View);

        UIScrollView scrollView = new UIScrollView
        {
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        UIStackView stackView = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 15,
            Distribution = UIStackViewDistribution.EqualSpacing,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        // Iterate through the AllTrails collection and create a view for each trail
        foreach (var trailViewController in AllTrails)
        {
            UIView trailView = new UIView
            {
                BackgroundColor = UIColor.White,
                Layer = {
                    BorderColor = UIColor.LightGray.CGColor,
                    BorderWidth = 1,
                    CornerRadius = 8,
                },
                ClipsToBounds = true,
            };

            UIImageView imageView = new UIImageView
            {
                ContentMode = UIViewContentMode.ScaleAspectFill,
                ClipsToBounds = true,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            imageView.Image = UIImage.FromBundle("TrailImage");

            UILabel nameLabel = new UILabel
            {
                Text = trailViewController.TrailName,
                Font = UIFont.BoldSystemFontOfSize(18),
                TextColor = UIColor.FromRGB(0, 128, 0),
                Lines = 1
            };

            UILabel descriptionLabel = new UILabel
            {
                Text = trailViewController.TrailDescription,
                Font = UIFont.SystemFontOfSize(14),
                TextColor = UIColor.DarkGray,
                Lines = 0 
            };

            // Create a button for purchasing the trail
            UIButton purchaseButton = new UIButton(UIButtonType.System);
            purchaseButton.SetTitle("Purchase", UIControlState.Normal);
            purchaseButton.TitleLabel.Font = UIFont.SystemFontOfSize(16);
            purchaseButton.BackgroundColor = UIColor.FromRGB(0, 128, 0);
            purchaseButton.SetTitleColor(UIColor.White, UIControlState.Normal);
            purchaseButton.Layer.CornerRadius = 5;
            purchaseButton.ClipsToBounds = true;

            trailView.AddSubviews(new UIView[] { imageView, nameLabel, descriptionLabel, purchaseButton });

            // Set up constraints for labels and button within the container view
            nameLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            descriptionLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            purchaseButton.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                imageView.TopAnchor.ConstraintEqualTo(trailView.TopAnchor),
                imageView.LeadingAnchor.ConstraintEqualTo(trailView.LeadingAnchor, 10),
                imageView.WidthAnchor.ConstraintEqualTo(150),
                imageView.HeightAnchor.ConstraintEqualTo(150),

                nameLabel.TopAnchor.ConstraintEqualTo(trailView.TopAnchor, 10),
                nameLabel.LeadingAnchor.ConstraintEqualTo(imageView.TrailingAnchor, 10),
                nameLabel.TrailingAnchor.ConstraintEqualTo(trailView.TrailingAnchor, -10),

                descriptionLabel.TopAnchor.ConstraintEqualTo(nameLabel.BottomAnchor, 5),
                descriptionLabel.LeadingAnchor.ConstraintEqualTo(imageView.TrailingAnchor, 10),
                descriptionLabel.TrailingAnchor.ConstraintEqualTo(trailView.TrailingAnchor, -10),

                purchaseButton.TopAnchor.ConstraintEqualTo(descriptionLabel.BottomAnchor, 10),
                purchaseButton.LeadingAnchor.ConstraintEqualTo(imageView.TrailingAnchor, 10),
                purchaseButton.TrailingAnchor.ConstraintEqualTo(trailView.TrailingAnchor, -10),
                purchaseButton.BottomAnchor.ConstraintEqualTo(trailView.BottomAnchor, -10)
            });

            // Add tap gesture recognizer to the purchase button
            purchaseButton.TouchUpInside += (sender, e) =>
            {
                // In-app purchase
                // TODO: Implement in-app purchase functionality
            };
            stackView.AddArrangedSubview(trailView);
        }

        scrollView.AddSubview(stackView);
        View.AddSubview(scrollView);

        // Set up constraints for the scroll view
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            scrollView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
            scrollView.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor, 10),
            scrollView.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, -10),
            scrollView.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor)
        });

        // Set up constraints for the stack view within the scroll view
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            stackView.TopAnchor.ConstraintEqualTo(scrollView.TopAnchor),
            stackView.LeadingAnchor.ConstraintEqualTo(scrollView.LeadingAnchor),
            stackView.TrailingAnchor.ConstraintEqualTo(scrollView.TrailingAnchor),
            stackView.BottomAnchor.ConstraintEqualTo(scrollView.BottomAnchor),
            stackView.WidthAnchor.ConstraintEqualTo(scrollView.WidthAnchor)
        });
    }
}
