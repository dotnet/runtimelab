using UIKit;
using CoreLocation;
using MapKit;

namespace HikingApp.MacCatalyst.ViewControllers;


internal class TrailViewController : UIViewController
{
    private Models.Trail _trail;
    public string TrailName => _trail.TrailName;
    public string TrailDescription => _trail.Description;
    public CLLocation TrailStartLocation => _trail.StartLocation;

    public TrailViewController(Models.Trail trail)
    {
        _trail = trail;

        View!.BackgroundColor = UIColor.White;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        this.Title = TrailName;

         // Create labels for trail details
        UILabel descriptionLabel = new UILabel
        {
            Text = $"Description: {TrailDescription}",
            Font = UIFont.SystemFontOfSize(16),
            Lines = 0 // Allow multiple lines
        };

        // Create a map view to display the start location
        MKMapView mapView = new MKMapView
        {
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        // Set the map's region to center on the trail's start location
        CLLocationCoordinate2D coordinate = TrailStartLocation.Coordinate;
        MKCoordinateRegion region = MKCoordinateRegion.FromDistance(coordinate, 1000, 1000);
        mapView.SetRegion(region, animated: false);

        // Add a pin annotation to the map at the trail's start location
        MKPointAnnotation annotation = new MKPointAnnotation
        {
            Coordinate = coordinate,
            Title = "START"
        };
        mapView.AddAnnotation(annotation);

        // Add labels and map view to the view
        View!.AddSubviews(descriptionLabel, mapView);

        // Set up constraints for labels and map view
        descriptionLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            descriptionLabel.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 20),
            descriptionLabel.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor, 20),
            descriptionLabel.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, -20),

            mapView.TopAnchor.ConstraintEqualTo(descriptionLabel.BottomAnchor, 20),
            mapView.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor, 20),
            mapView.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, -20),
            mapView.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor, -20)
        });
    }
}