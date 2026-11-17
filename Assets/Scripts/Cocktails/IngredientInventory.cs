using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives with the persistent player across levels. A retry destroys that player and starts a fresh inventory.
/// Throwable costs are reserved on confirmation and refunded if the player cancels before release.
/// </summary>
[DisallowMultipleComponent]
public class IngredientInventory : MonoBehaviour
{
    [SerializeField, Min(0)] private int lifeEssence;
    [SerializeField, Min(0)] private int water;
    [SerializeField, Min(0)] private int sugar;
    [SerializeField, Min(0)] private int gunpowder;

    public event Action OnChanged;

    public int GetCount(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.LifeEssence: return lifeEssence;
            case IngredientType.Water: return water;
            case IngredientType.Sugar: return sugar;
            case IngredientType.Gunpowder: return gunpowder;
            default: return 0;
        }
    }

    public void Add(IngredientType type, int count)
    {
        if (count <= 0 || (int)type < 0 || (int)type >= IngredientInfo.Count) return;
        SetCount(type, (int)Math.Min(int.MaxValue, (long)GetCount(type) + count));
        OnChanged?.Invoke();
    }

    public bool CanAfford(CocktailRecipe recipe, BeerInventory beer, out string error)
    {
        if (recipe == null)
        {
            error = "No recipe for that combination";
            return false;
        }
        if (recipe.Id == CocktailKind.AllYouCanDrink && beer && beer.HasInfiniteBeer)
        {
            error = "All-You-Can-Drink is already active";
            return false;
        }
        var missing = new List<string>();
        for (var i = 0; i < IngredientInfo.Count; i++)
        {
            var type = (IngredientType)i;
            var shortfall = recipe.GetCost(type) - GetCount(type);
            if (shortfall > 0) missing.Add($"{shortfall} {IngredientInfo.Name(type)}");
        }
        var beerShortfall = recipe.BeerCost - (beer ? beer.PhysicalCount : 0);
        if (beerShortfall > 0) missing.Add($"{beerShortfall} Beer");
        error = missing.Count == 0 ? string.Empty : "Need " + string.Join(" + ", missing);
        return missing.Count == 0;
    }

    public bool TryConsume(CocktailRecipe recipe, BeerInventory beer, out string error)
    {
        if (!CanAfford(recipe, beer, out error)) return false;
        // Only real glasses can pay the cost; the temporary infinite supply cannot fund another buff.
        // No inventory events are raised until every resource has been debited.
        for (var i = 0; i < IngredientInfo.Count; i++)
        {
            var type = (IngredientType)i;
            SetCount(type, GetCount(type) - recipe.GetCost(type));
        }
        if (recipe.BeerCost > 0 && !beer.TryConsumeForRecipe(recipe.BeerCost))
        {
            for (var i = 0; i < IngredientInfo.Count; i++)
            {
                var type = (IngredientType)i;
                SetCount(type, GetCount(type) + recipe.GetCost(type));
            }
            error = "Not enough beer";
            return false;
        }
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Refunds a reserved throw. Drinks are applied immediately and cannot be cancelled.</summary>
    public void Refund(CocktailRecipe recipe)
    {
        if (recipe == null || !recipe.IsThrowable) return;
        for (var i = 0; i < IngredientInfo.Count; i++)
        {
            var type = (IngredientType)i;
            SetCount(type, (int)Math.Min(int.MaxValue, (long)GetCount(type) + recipe.GetCost(type)));
        }
        OnChanged?.Invoke();
    }

    private void SetCount(IngredientType type, int count)
    {
        count = Mathf.Max(0, count);
        switch (type)
        {
            case IngredientType.LifeEssence: lifeEssence = count; break;
            case IngredientType.Water: water = count; break;
            case IngredientType.Sugar: sugar = count; break;
            case IngredientType.Gunpowder: gunpowder = count; break;
        }
    }

    private void OnValidate()
    {
        for (var i = 0; i < IngredientInfo.Count; i++) SetCount((IngredientType)i, GetCount((IngredientType)i));
    }
}
