using System.Text.Json;
using Location = CoreLocation.CLLocation;

namespace HikingApp.MacCatalyst.Models;
internal class Trail
{
    public string TrailName { get; set; }
    public string Description { get; set; }
    public Location StartLocation { get; set; }
    public double Distance { get; set; }
    public string Difficulty { get; set; }
    public string TerrainType { get; set; }

    public Trail(string trailName, string description, Location startLocation, double distance, string difficulty, string terrainType)
    {
        TrailName = trailName;
        Description = description;
        StartLocation = startLocation;
        Distance = distance;
        Difficulty = difficulty;
        TerrainType = terrainType;
    }

    public static IEnumerable<Trail> GetTestTrailData(int count)
    {
        Random random = new Random();
        string[] trailNames = { "Bear Mountain", "Eagle's Peak", "Sunset Ridge", "River Valley", "Pine Forest", "Coastal Cliff", "Highland Path" };
        string[] difficulties = { "Easy", "Moderate", "Hard" };
        string[] terrains = { "Forest", "Mountain", "Hill", "Rocky Path"  };

        for (int i = 0; i < count; ++i)
        {
            string trailName = trailNames[random.Next(trailNames.Length)] + $" Trail {i}";
            string description = $"A trail known for its {terrains[random.Next(terrains.Length)].ToLower()} terrain and {difficulties[random.Next(difficulties.Length)].ToLower()} difficulty.";
            Location startLocation = new Location(
                50.08804 + random.NextDouble() * 0.1,
                14.42076 + random.NextDouble() * 0.1
            );
            double distance = Math.Round(1 + random.NextDouble() * 20, 1);
            string difficulty = difficulties[random.Next(difficulties.Length)];
            string terrain = terrains[random.Next(terrains.Length)];

            yield return new Trail(trailName, description, startLocation, distance, difficulty, terrain);
        }
    }
}