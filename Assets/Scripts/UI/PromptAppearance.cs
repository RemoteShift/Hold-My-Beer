using TMPro;
using UnityEngine;

/// <summary>Shared appearance for gameplay instructions and input prompts.</summary>
public static class PromptAppearance
{
    public static readonly Color TextColor = new Color32(0xFB, 0xFF, 0x8A, 0xFF);
    private static TMP_FontAsset font;
    private static TMP_SpriteAsset icons;
    public static TMP_SpriteAsset Icons => icons ? icons : icons = Resources.Load<TMP_SpriteAsset>("PromptStyle/Bit Input Icons");

    public static void Apply(TMP_Text text)
    {
        if (!text) return;
        if (!font) font = Resources.Load<TMP_FontAsset>("PromptStyle/NotJamOldStyle14 SDF");
        if (font)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
        text.fontSize = 36f;
        text.enableAutoSizing = false;
        text.fontSizeMin = text.fontSizeMax = 36f;
        text.color = TextColor;
        text.fontStyle = FontStyles.Normal;
        text.enableVertexGradient = false;
        text.spriteAsset = Icons;
        text.tintAllSprites = false;
        text.richText = true;
        text.extraPadding = true;
        text.raycastTarget = false;
    }
}
