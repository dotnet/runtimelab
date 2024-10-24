// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace HikingApp.MacCatalyst.ViewControllers;

internal class MyTrailsViewController : UIViewController
{
    public ObservableCollection<ViewControllers.TrailViewController> PurchasedTrails { get;  }
    public MyTrailsViewController()
    {
        PurchasedTrails = new ObservableCollection<ViewControllers.TrailViewController>(Models.Trail.GetMockTrailData(2).Select(trail => new ViewControllers.TrailViewController(trail)));

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

        // Iterate through the PurchasedTrails collection and create a view for each purchased trail
        foreach (var trailViewController in PurchasedTrails)
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

            // Create a button to view details of the trail
            UIButton detailsButton = new UIButton(UIButtonType.System);
            detailsButton.SetTitle("View Details", UIControlState.Normal);
            detailsButton.TitleLabel.Font = UIFont.SystemFontOfSize(16); 
            detailsButton.BackgroundColor = UIColor.FromRGB(0, 128, 0);
            detailsButton.SetTitleColor(UIColor.White, UIControlState.Normal);
            detailsButton.Layer.CornerRadius = 5;
            detailsButton.ClipsToBounds = true;

            trailView.AddSubviews(new UIView[] { imageView, nameLabel, descriptionLabel, detailsButton });

            // Set up constraints for the image, labels, and button within the container view
            nameLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            descriptionLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            detailsButton.TranslatesAutoresizingMaskIntoConstraints = false;
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

                detailsButton.TopAnchor.ConstraintEqualTo(descriptionLabel.BottomAnchor, 10),
                detailsButton.LeadingAnchor.ConstraintEqualTo(imageView.TrailingAnchor, 10),
                detailsButton.TrailingAnchor.ConstraintEqualTo(trailView.TrailingAnchor, -10),
                detailsButton.BottomAnchor.ConstraintEqualTo(trailView.BottomAnchor, -10)
            });

            detailsButton.TouchUpInside += (sender, e) =>
            {
                this.NavigationController!.PushViewController(trailViewController, animated: true);
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
