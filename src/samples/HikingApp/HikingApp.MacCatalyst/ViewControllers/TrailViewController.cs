// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    public double TrailDistance => _trail.Distance;
    public string TrailDifficulty => _trail.Difficulty;
    public string TrailTerrainType => _trail.TerrainType;

    public TrailViewController(Models.Trail trail)
    {
        _trail = trail;
        View!.BackgroundColor = UIColor.White;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        ArgumentNullException.ThrowIfNull(View);

        this.Title = TrailName;

        // Create labels for trail details
        UILabel descriptionLabel = new UILabel
        {
            Text = $"Description: {TrailDescription}",
            Font = UIFont.SystemFontOfSize(16),
            Lines = 0 // Allow multiple lines
        };

        UILabel distanceLabel = new UILabel
        {
            Text = $"Distance: {TrailDistance} km",
            Font = UIFont.SystemFontOfSize(16)
        };

        UILabel difficultyLabel = new UILabel
        {
            Text = $"Difficulty: {TrailDifficulty}",
            Font = UIFont.SystemFontOfSize(16)
        };

        UILabel terrainTypeLabel = new UILabel
        {
            Text = $"Terrain Type: {TrailTerrainType}",
            Font = UIFont.SystemFontOfSize(16)
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

        View.AddSubviews(descriptionLabel, distanceLabel, difficultyLabel, terrainTypeLabel, mapView);

        // Set up constraints for labels and map view
        descriptionLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        distanceLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        difficultyLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        terrainTypeLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            descriptionLabel.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 20),
            descriptionLabel.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor, 20),
            descriptionLabel.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, -20),

            distanceLabel.TopAnchor.ConstraintEqualTo(descriptionLabel.BottomAnchor, 10),
            distanceLabel.LeadingAnchor.ConstraintEqualTo(descriptionLabel.LeadingAnchor),
            distanceLabel.TrailingAnchor.ConstraintEqualTo(descriptionLabel.TrailingAnchor),

            difficultyLabel.TopAnchor.ConstraintEqualTo(distanceLabel.BottomAnchor, 10),
            difficultyLabel.LeadingAnchor.ConstraintEqualTo(descriptionLabel.LeadingAnchor),
            difficultyLabel.TrailingAnchor.ConstraintEqualTo(descriptionLabel.TrailingAnchor),

            terrainTypeLabel.TopAnchor.ConstraintEqualTo(difficultyLabel.BottomAnchor, 10),
            terrainTypeLabel.LeadingAnchor.ConstraintEqualTo(descriptionLabel.LeadingAnchor),
            terrainTypeLabel.TrailingAnchor.ConstraintEqualTo(descriptionLabel.TrailingAnchor),

            mapView.TopAnchor.ConstraintEqualTo(terrainTypeLabel.BottomAnchor, 20),
            mapView.LeadingAnchor.ConstraintEqualTo(descriptionLabel.LeadingAnchor),
            mapView.TrailingAnchor.ConstraintEqualTo(descriptionLabel.TrailingAnchor),
            mapView.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor, -20)
        });
    }
}
