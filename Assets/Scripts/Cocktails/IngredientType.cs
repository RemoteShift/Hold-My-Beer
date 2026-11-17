using System;
using UnityEngine;

public enum IngredientType
{
    LifeEssence = 0,
    Water = 1,
    Sugar = 2,
    Gunpowder = 3
}

[Flags]
public enum IngredientMask
{
    None = 0,
    LifeEssence = 1,
    Water = 2,
    Sugar = 4,
    Gunpowder = 8
}

public static class IngredientInfo
{
    public const int Count = 4;
    public static IngredientMask Mask(IngredientType type) => (IngredientMask)(1 << (int)type);
    public static string Name(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.LifeEssence: return "Life Essence";
            case IngredientType.Water: return "Water";
            case IngredientType.Sugar: return "Sugar";
            case IngredientType.Gunpowder: return "Gunpowder";
            default: return "Ingredient";
        }
    }

    public static Color Color(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.LifeEssence: return new Color(0.98f, 0.43f, 0.42f);
            case IngredientType.Water: return new Color(0.36f, 0.75f, 0.96f);
            case IngredientType.Sugar: return new Color(1f, 0.85f, 0.5f);
            case IngredientType.Gunpowder: return new Color(0.72f, 0.64f, 0.85f);
            default: return UnityEngine.Color.white;
        }
    }
}
