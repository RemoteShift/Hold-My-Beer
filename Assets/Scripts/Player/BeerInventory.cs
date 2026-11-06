using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The glasses the player is carrying and how full each one is, from 0 (empty) to 1 (full).
/// </summary>
public class BeerInventory : MonoBehaviour
{
    private const float FullGlass = 1f;

    [SerializeField, Min(1)] private int maxGlasses = 3;
    [SerializeField, Min(0)] private int startingGlasses = 3;

    [Tooltip("Fill level of each glass. Set at runtime, shown here for debugging.")]
    [SerializeField, Range(0f, 1f)] private List<float> glasses = new List<float>();

    public int MaxGlasses => maxGlasses;
    public int Count => glasses.Count;
    public bool HasBeer => glasses.Count > 0;
    public bool IsFull => glasses.Count >= maxGlasses;
    public IReadOnlyList<float> Glasses => glasses;

    /// <summary>True when every slot holds a full glass, so a refill would change nothing.</summary>
    public bool IsFullyStocked => IsFull && glasses.TrueForAll(fill => fill >= FullGlass);

    /// <summary>Raised whenever a glass is taken, returned or refilled.</summary>
    public event Action OnChanged;

    private void Awake()
    {
        glasses.Clear();

        var count = Mathf.Min(startingGlasses, maxGlasses);
        for (var i = 0; i < count; i++)
        {
            glasses.Add(FullGlass);
        }
    }

    /// <summary>Removes the rightmost glass on the HUD, however full it is, so it can be handed to an enemy.</summary>
    /// <returns>False if the player has no beer.</returns>
    public bool TryTakeRightmost(out float fill)
    {
        fill = 0f;
        if (!HasBeer) return false;

        var rightmost = glasses.Count - 1;
        fill = glasses[rightmost];
        glasses.RemoveAt(rightmost);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Adds a glass that was taken back from an enemy.</summary>
    /// <returns>False if there is no room or the glass is empty.</returns>
    public bool TryAddGlass(float fill)
    {
        if (IsFull || fill <= 0f) return false;

        glasses.Add(Mathf.Min(fill, FullGlass));
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Tops up every glass and fills any empty slots.</summary>
    /// <returns>False if the player was already fully stocked, so limited sources aren't wasted.</returns>
    public bool Refill()
    {
        var changed = !IsFull;

        for (var i = 0; i < glasses.Count; i++)
        {
            if (glasses[i] < FullGlass)
            {
                glasses[i] = FullGlass;
                changed = true;
            }
        }

        while (!IsFull)
        {
            glasses.Add(FullGlass);
        }

        if (changed) OnChanged?.Invoke();
        return changed;
    }
}
