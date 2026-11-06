using UnityEngine;

/// <summary>The slide-to-rest rules shared by everything the player can shove.</summary>
public static class SlideMotion
{
    private const float BlockedDistanceRatio = 0.25f;

    /// <summary>The speed that comes to rest exactly at the end of the distance left.</summary>
    public static float SpeedToStopWithin(float distanceLeft, float deceleration)
    {
        return Mathf.Sqrt(2f * deceleration * distanceLeft);
    }

    /// <summary>True if something stopped the slide from covering most of the step it expected.</summary>
    public static bool IsBlocked(float moved, float expectedStep)
    {
        return moved < expectedStep * BlockedDistanceRatio;
    }
}
