using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Hold My Beer/Cocktail Stamp Library")]
public sealed class CocktailStampLibrary : ScriptableObject
{
    [Serializable]
    public sealed class Art
    {
        public CocktailKind kind;
        public Sprite background, fill, icon;
        [Min(0.01f)] public float pixelScale = 1f;
        [Tooltip("Optical icon adjustment in source-art pixels.")] public Vector2 iconOffset;
        [Tooltip("HUD-only correction for comparable visible icon area."), Min(.1f)] public float hudIconScale = 1f;
        [Tooltip("HUD-only optical centering in source-art pixels.")] public Vector2 hudIconOffset;
    }

    [SerializeField] private Art[] stamps;
    private static CocktailStampLibrary instance;
    public static CocktailStampLibrary Instance
    {
        get
        {
            if (!instance) instance = Resources.Load<CocktailStampLibrary>("CocktailStampLibrary");
            return instance;
        }
    }

    public Art Get(CocktailKind kind)
    {
        if (stamps != null)
            foreach (var art in stamps)
                if (art != null && art.kind == kind) return art;
        return null;
    }
}
