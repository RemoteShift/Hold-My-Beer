using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared balance data used by brewing, HUD, previews and projectiles.</summary>
public sealed class CocktailSettings : ScriptableObject
{
    private static CocktailSettings instance;
    public static CocktailSettings Instance
    {
        get
        {
            if (!instance) instance = Resources.Load<CocktailSettings>("CocktailSettings");
            if (!instance) instance = CreateInstance<CocktailSettings>();
            return instance;
        }
    }

    [Header("Recipes — mouse-wheel selection")]
    [SerializeField] private CocktailRecipe honeyBrew = new CocktailRecipe(CocktailKind.HoneyBrew, "Honey Brew", true, 3f, 5f,
            new Color(1f, 0.7f, 0.18f), 1, 0, 2, 0);
    [SerializeField] private CocktailRecipe fizzySodaSplash = new CocktailRecipe(CocktailKind.FizzySodaSplash, "Fizzy Soda Splash", true, 3f, 0f,
            new Color(0.35f, 0.85f, 1f), 0, 2, 1, 0);
    [SerializeField] private CocktailRecipe aggressionTonic = new CocktailRecipe(CocktailKind.AggressionTonic, "Aggression Tonic", true, 4f, 2f,
            new Color(1f, 0.3f, 0.25f), 0, 1, 0, 2);
    [SerializeField] private CocktailRecipe allYouCanDrink = new CocktailRecipe(CocktailKind.AllYouCanDrink, "All-You-Can-Drink", false, 0f, 10f,
            new Color(1f, 0.78f, 0.3f), 3, 0, 0, 0, 1);
    [SerializeField] private CocktailRecipe steelGutVodka = new CocktailRecipe(CocktailKind.SteelGutVodka, "Steel-Gut Vodka", false, 0f, 6f,
            new Color(0.7f, 0.78f, 1f), 2, 0, 0, 1);

    [Header("Shared bottle flight")]
    [Min(0.1f)] public float throwSpeed = 16f;
    public float upwardLift = 2.5f;
    [Min(0.01f)] public float bottleCollisionRadius = 0.12f;
    [Range(0.1f, 30f)] public float bottleLifetime = 6f;
    [Header("Fizzy Soda Splash")]
    [Min(0f)] public float sodaPushStrength = 65f;

    private CocktailRecipe[] recipes;
    public IReadOnlyList<CocktailRecipe> Recipes => recipes ??= new[]
        { honeyBrew, fizzySodaSplash, aggressionTonic, allYouCanDrink, steelGutVodka };
    private void OnValidate() { recipes = null; }
}
