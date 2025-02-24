// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Location = CoreLocation.CLLocation;

namespace HikingApp.MacCatalyst.Models;

/// <summary>
/// Represents a hiking trail with various properties such as name, description, location, distance, difficulty, and terrain type.
/// </summary>
internal class Trail
{
    /// <summary>
    /// Gets or sets the name of the trail.
    /// </summary>
    public string TrailName { get; set; }

    /// <summary>
    /// Gets or sets the description of the trail.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the starting location of the trail.
    /// </summary>
    public Location StartLocation { get; set; }

    /// <summary>
    /// Gets or sets the distance of the trail in kilometers.
    /// </summary>s
    public double Distance { get; set; }

    /// <summary>
    /// Gets or sets the difficulty level of the trail.
    /// </summary>
    public string Difficulty { get; set; }

    /// <summary>
    /// Gets or sets the type of terrain the trail covers.
    /// </summary>
    public string TerrainType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Trail"/> class with specified properties.
    /// </summary>
    /// <param name="trailName">The name of the trail.</param>
    /// <param name="description">The description of the trail.</param>
    /// <param name="startLocation">The starting location of the trail.</param>
    /// <param name="distance">The distance of the trail in kilometers.</param>
    /// <param name="difficulty">The difficulty level of the trail.</param>
    /// <param name="terrainType">The type of terrain the trail covers.</param>
    public Trail(string trailName, string description, Location startLocation, double distance, string difficulty, string terrainType)
    {
        TrailName = trailName;
        Description = description;
        StartLocation = startLocation;
        Distance = distance;
        Difficulty = difficulty;
        TerrainType = terrainType;
    }

    /// <summary>
    /// Generates a collection of mock trail data.
    /// </summary>
    /// <param name="count">The number of mock trails to generate.</param>
    /// <returns>An enumerable collection of <see cref="Trail"/> objects.</returns>
    public static IEnumerable<Trail> GetMockTrailData(int count)
    {
        Random random = Random.Shared;
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
