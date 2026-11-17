using System.Collections.Generic;
using UnityEngine;

public enum CocktailKind
{
    HoneyBrew,
    FizzySodaSplash,
    AggressionTonic,
    AllYouCanDrink,
    SteelGutVodka
}

/// <summary>The one source of truth for recipe costs, selection and effect parameters.</summary>
[System.Serializable]
public sealed class CocktailRecipe
{
    [SerializeField, HideInInspector] private CocktailKind id;
    [SerializeField] private string displayName;
    [SerializeField, HideInInspector] private bool isThrowable;
    [SerializeField, Min(0f), Tooltip("Area radius in metres. Unused by drinks.")] private float radius;
    [SerializeField, Min(0f), Tooltip("Effect duration in seconds. Unused by soda.")] private float duration;
    [SerializeField] private Color color;
    [Header("Ingredient costs")]
    [SerializeField, Min(0)] private int lifeEssence;
    [SerializeField, Min(0)] private int water;
    [SerializeField, Min(0)] private int sugar;
    [SerializeField, Min(0)] private int gunpowder;
    [SerializeField, Min(0)] private int beer;

    public CocktailKind Id => id;
    public int Hotkey => (int)Id + 1;
    public string Name => displayName;
    public bool IsThrowable => isThrowable;
    public float Radius => Mathf.Max(0f, radius);
    public float Duration => Mathf.Max(0f, duration);
    public Color Color => color;
    public int BeerCost => Mathf.Max(0, beer);
    public IngredientMask Mask
    {
        get
        {
            IngredientMask mask = 0;
            for (var i = 0; i < 4; i++)
                if (GetCost((IngredientType)i) > 0) mask |= IngredientInfo.Mask((IngredientType)i);
            return mask;
        }
    }
    public string CostDescription
    {
        get
        {
            var parts = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var type = (IngredientType)i;
                if (GetCost(type) > 0) parts.Add($"{GetCost(type)} {IngredientInfo.Name(type)}");
            }
            if (BeerCost > 0) parts.Add($"{BeerCost} Beer");
            return string.Join(" + ", parts);
        }
    }

    public CocktailRecipe(CocktailKind id, string name, bool isThrowable, float radius,
        float duration, Color color, int lifeEssence, int water, int sugar, int gunpowder, int beer = 0)
    {
        this.id = id;
        displayName = name;
        this.isThrowable = isThrowable;
        this.radius = radius;
        this.duration = duration;
        this.color = color;
        this.lifeEssence = lifeEssence;
        this.water = water;
        this.sugar = sugar;
        this.gunpowder = gunpowder;
        this.beer = beer;
    }

    public int GetCost(IngredientType type)
    {
        switch ((int)type)
        {
            case 0: return Mathf.Max(0, lifeEssence);
            case 1: return Mathf.Max(0, water);
            case 2: return Mathf.Max(0, sugar);
            case 3: return Mathf.Max(0, gunpowder);
            default: return 0;
        }
    }

}

public static class CocktailRecipes
{
    public const int HotkeyCount = 5;

    public static IReadOnlyList<CocktailRecipe> All => CocktailSettings.Instance.Recipes;

    public static CocktailRecipe GetByHotkey(int hotkey)
    {
        foreach (var recipe in All)
            if (recipe.Hotkey == hotkey) return recipe;
        return null;
    }

    public static CocktailRecipe Find(IngredientMask mask)
    {
        foreach (var recipe in All)
            if (recipe.Mask == mask) return recipe;
        return null;
    }
}
