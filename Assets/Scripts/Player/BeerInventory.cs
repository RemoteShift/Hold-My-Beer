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
    private readonly List<float> infiniteGlasses = new List<float>();
    private float infiniteBeerUntil;
    private bool wasInfinite;

    public int MaxGlasses => maxGlasses;
    public int Count => HasInfiniteBeer ? maxGlasses : glasses.Count;
    public int PhysicalCount => glasses.Count;
    public bool HasBeer => HasInfiniteBeer || glasses.Count > 0;
    public bool IsFull => glasses.Count >= maxGlasses;
    public IReadOnlyList<float> Glasses => HasInfiniteBeer ? infiniteGlasses : glasses;
    public float InfiniteBeerSecondsLeft => Mathf.Max(0f, infiniteBeerUntil - Time.time);
    public bool HasInfiniteBeer => InfiniteBeerSecondsLeft > 0f;

    /// <summary>True when every slot holds a full glass, so a refill would change nothing.</summary>
    public bool IsFullyStocked => HasInfiniteBeer || (IsFull && glasses.TrueForAll(fill => fill >= FullGlass));

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
        for (var i = 0; i < maxGlasses; i++) infiniteGlasses.Add(FullGlass);
    }

    private void Update()
    {
        if (!wasInfinite || HasInfiniteBeer) return;
        wasInfinite = false;
        OnChanged?.Invoke();
    }

    public void GrantInfiniteBeer(float seconds)
    {
        if (seconds <= 0f) return;
        infiniteBeerUntil = Time.time + seconds;
        wasInfinite = true;
        OnChanged?.Invoke();
    }

    public void ClearInfiniteBeer()
    {
        infiniteBeerUntil = 0f;
        if (!wasInfinite) return;
        wasInfinite = false;
        OnChanged?.Invoke();
    }

    /// <summary>Brewing spends actual glasses, even if serving currently uses an infinite supply.</summary>
    public bool TryConsumeForRecipe(int amount)
    {
        if (amount < 0 || glasses.Count < amount) return false;
        if (amount == 0) return true;
        glasses.RemoveRange(glasses.Count - amount, amount);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Removes the rightmost glass on the HUD, however full it is, so it can be handed to an enemy.</summary>
    /// <returns>False if the player has no beer.</returns>
    public bool TryTakeRightmost(out float fill)
    {
        fill = 0f;
        if (!HasBeer) return false;
        if (HasInfiniteBeer)
        {
            fill = FullGlass;
            return true;
        }

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
        // Do not turn temporary unlimited servings into permanent inventory.
        if (HasInfiniteBeer || IsFull || fill <= 0f || float.IsNaN(fill)) return false;

        glasses.Add(Mathf.Min(fill, FullGlass));
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Tops up every glass and fills any empty slots.</summary>
    /// <returns>False if the player was already fully stocked, so limited sources aren't wasted.</returns>
    public bool Refill()
    {
        if (HasInfiniteBeer) return false;
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
