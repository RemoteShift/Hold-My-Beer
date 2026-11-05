using UnityEngine;

/// <summary>Somewhere the player can restock their beers. Taps never run out, barrels have limited refills.</summary>
public class BeerSource : MonoBehaviour
{
    [SerializeField] private bool unlimited;
    [SerializeField, Min(0)] private int refillsLeft = 3;

    public bool IsUnlimited => unlimited;
    public int RefillsLeft => refillsLeft;
    public bool IsEmpty => !unlimited && refillsLeft <= 0;

    /// <summary>Restocks the player. A refill is only used up if the player actually needed one.</summary>
    /// <returns>False if the source is empty or the player is already fully stocked.</returns>
    public bool TryRefill(BeerInventory inventory)
    {
        if (IsEmpty || !inventory.Refill()) return false;

        if (!unlimited) refillsLeft--;
        return true;
    }
}
