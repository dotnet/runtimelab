using System.Collections.ObjectModel;

namespace HikingApp.MacCatalyst.ViewControllers;


internal class ExploreViewController : UIViewController
{
    public ObservableCollection<ViewControllers.TrailViewController> AllTrails { get;  }

    public ExploreViewController()
    {
        AllTrails = new ObservableCollection<ViewControllers.TrailViewController>(Models.Trail.GetTestTrailData(5).Select(trail => new ViewControllers.TrailViewController(trail)));

        View!.BackgroundColor = UIColor.White;
    }


     public override void ViewDidLoad ()
    {
        base.ViewDidLoad();

        // Initialize the scroll view
        UIScrollView scrollView = new UIScrollView
        {
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        // Initialize the stack view
        UIStackView stackView = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 10,
            Distribution = UIStackViewDistribution.EqualSpacing,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        // Iterate through the AllTrails collection
        foreach (var trailViewController in AllTrails)
        {
            // Create labels for name and description
            UILabel nameLabel = new UILabel
            {
                Text = trailViewController.TrailName,
                Font = UIFont.BoldSystemFontOfSize(16)
            };

            UILabel descriptionLabel = new UILabel
            {
                Text = trailViewController.TrailDescription,
                Font = UIFont.SystemFontOfSize(14)
            };

            // Create a button for details
            UIButton detailsButton = new UIButton(UIButtonType.System);
            detailsButton.SetTitle("Details", UIControlState.Normal);

            // Create a container view for each trail
            UIView trailView = new UIView();
            trailView.Layer.BorderColor = UIColor.Gray.CGColor;
            trailView.Layer.BorderWidth = 1;
            trailView.Layer.CornerRadius = 5;
            trailView.ClipsToBounds = true;

            trailView.AddSubviews(new UIView[] { nameLabel, descriptionLabel, detailsButton });

            // Set up constraints for labels and button within the container view
            nameLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            descriptionLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            detailsButton.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                nameLabel.TopAnchor.ConstraintEqualTo(trailView.TopAnchor, 10),
                nameLabel.LeadingAnchor.ConstraintEqualTo(trailView.LeadingAnchor, 10),
                nameLabel.TrailingAnchor.ConstraintEqualTo(trailView.TrailingAnchor, -10),

                descriptionLabel.TopAnchor.ConstraintEqualTo(nameLabel.BottomAnchor, 5),
                descriptionLabel.LeadingAnchor.ConstraintEqualTo(trailView.LeadingAnchor, 10),
                descriptionLabel.TrailingAnchor.ConstraintEqualTo(trailView.TrailingAnchor, -10),

                detailsButton.TopAnchor.ConstraintEqualTo(descriptionLabel.BottomAnchor, 10),
                detailsButton.LeadingAnchor.ConstraintEqualTo(trailView.LeadingAnchor, 10),
                detailsButton.TrailingAnchor.ConstraintEqualTo(trailView.TrailingAnchor, -10),
                detailsButton.BottomAnchor.ConstraintEqualTo(trailView.BottomAnchor, -10)
            });

            // Add tap gesture recognizer to the details button
            detailsButton.TouchUpInside += (sender, e) =>
            {
                this.NavigationController!.PushViewController(trailViewController, animated: true);
            };

            // Add the trail view to the stack view
            stackView.AddArrangedSubview(trailView);
        }

        // Add the stack view to the scroll view
        scrollView.AddSubview(stackView);

        // Add the scroll view to the main view
        View!.AddSubview(scrollView);

        // Set up constraints for the scroll view
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            scrollView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
            scrollView.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor),
            scrollView.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor),
            scrollView.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor)
        });

        // Set up constraints for the stack view
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