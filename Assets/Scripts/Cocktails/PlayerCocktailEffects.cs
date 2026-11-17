using UnityEngine;

/// <summary>Timed drink effects use game time, so pausing also pauses their countdowns.</summary>
[RequireComponent(typeof(BeerInventory), typeof(PlayerHealth))]
[DisallowMultipleComponent]
public class PlayerCocktailEffects : MonoBehaviour
{
    private BeerInventory beer;
    private PlayerHealth health;

    public float InfiniteBeerSecondsLeft => beer ? beer.InfiniteBeerSecondsLeft : 0f;
    public float InvulnerableSecondsLeft => health ? health.InvulnerableSecondsLeft : 0f;

    private void Awake()
    {
        beer = GetComponent<BeerInventory>();
        health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (health) health.OnDied += Clear;
    }

    private void OnDisable()
    {
        if (health) health.OnDied -= Clear;
        Clear();
    }

    public void Apply(CocktailRecipe recipe)
    {
        if (recipe == null || !health || health.IsDead) return;
        switch (recipe.Id)
        {
            case CocktailKind.AllYouCanDrink:
                beer.GrantInfiniteBeer(recipe.Duration);
                break;
            case CocktailKind.SteelGutVodka:
                health.GrantInvulnerability(recipe.Duration);
                break;
        }
    }

    public void Clear()
    {
        if (beer) beer.ClearInfiniteBeer();
        if (health) health.ClearInvulnerability();
    }
}
