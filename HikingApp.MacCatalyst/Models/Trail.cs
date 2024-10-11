using System.Text.Json;
using Location = CoreLocation.CLLocation;

namespace HikingApp.MacCatalyst.Models;
internal class Trail
{
    public string TrailName { get; set; }
    public string Description { get; set;}
    public Location StartLocation { get; set; }

    public Trail(string trailName, string description, Location startLocation)
    {
        TrailName = trailName;
        Description = description;
        StartLocation = startLocation;
    }

    public static IEnumerable<Trail> GetTestTrailData(int count)
    {
        Random random = new Random();
    
        for (int i = 0; i < count; ++i)
        {
            Trail trail = new Trail(
                $"Trail {i}",
                $"This is a description of the trail {i}.",
                new Location(
                    50.08804 + random.NextDouble() * 0.1, 
                    14.42076 + random.NextDouble() * 0.1
                )
            );
            yield return trail;
        }   
    }
}