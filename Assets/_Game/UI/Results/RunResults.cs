using System;
using UnityEngine;

/// <summary>A frozen result, independent of the player and the scene it came from.</summary>
[Serializable]
public struct RunResults
{
    public float seconds;
    public int beersHeld;
    public int cocktailsMixed;
    public int bruisesEarned;
    public int tripsToTap;
    public float damageTaken;

    public static RunResults From(RunStats stats) => new RunResults
    {
        seconds = stats.SecondsSurvived, beersHeld = stats.EnemiesBeered,
        cocktailsMixed = stats.CocktailsMixed, bruisesEarned = stats.BruisesEarned,
        tripsToTap = stats.RefillsUsed, damageTaken = stats.DamageTaken
    };
}
