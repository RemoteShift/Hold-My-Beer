using UnityEngine;

/// <summary>HUD artwork and baked optical measurements for world pickups and projectiles.</summary>
public sealed class WorldHudArt : ScriptableObject
{
    [System.Serializable]
    public sealed class Art
    {
        public Sprite sprite;
        public Material material;
        public float worldUnitsPerPixel;
        public Vector2 opticalOffsetPixels;
    }

    public Art[] ingredients;
    public Art[] cocktails;
    public Material spriteMaterial;
    private static WorldHudArt instance;

    public static WorldHudArt Instance
    {
        get
        {
            if (!instance) instance = Resources.Load<WorldHudArt>("WorldHudArt");
            return instance;
        }
    }

    public Art Ingredient(IngredientType type) =>
        ingredients != null && (int)type >= 0 && (int)type < ingredients.Length ? ingredients[(int)type] : null;

    public Art Cocktail(CocktailKind kind) =>
        cocktails != null && (int)kind >= 0 && (int)kind < cocktails.Length ? cocktails[(int)kind] : null;
}
