using UnityEngine;
using UnityEngine.UI;

/// <summary>Three sprite layers share one optical scale; only the fill is cropped.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CocktailStampView : MonoBehaviour
{
    [SerializeField] private Image background, fill, icon;
    [SerializeField] private CocktailKind kind;
    [SerializeField, Min(1f)] private float stampSize = 104f;
    [Tooltip("Optional full-stamp height. Zero keeps the artwork optical scale.")]
    [SerializeField, Min(0f)] private float stampHeight;
    [Header("HUD icon presentation")]
    [SerializeField] private bool iconOnly;
    [SerializeField, Min(1f)] private float iconHeight = 92f;
    private CocktailStampLibrary.Art displayedArt;
    private float progress = 1f;
    public CocktailKind Kind => kind;
    public float Progress => progress;
    public Image Fill => fill;

    private void OnEnable() { displayedArt = null; Refresh(); }
    private void OnValidate() { displayedArt = null; }
    private void Update() { Refresh(); }

    public void SetKind(CocktailKind value)
    {
        kind = value;
        Refresh();
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        if (!fill) return;
        float rows = fill.sprite ? fill.sprite.rect.height : 0f;
        float amount = rows > 0f ? Mathf.Ceil(progress * rows) / rows : progress;
        if (!Mathf.Approximately(fill.fillAmount, amount)) fill.fillAmount = amount;
    }

    private void Refresh()
    {
        var library = CocktailStampLibrary.Instance;
        var art = library ? library.Get(kind) : null;
        if (art == null || !background || !fill || !icon) return;
        if (displayedArt != art)
        {
            displayedArt = art;
            float scale = iconOnly && art.icon
                ? iconHeight * art.hudIconScale / art.icon.rect.height
                : stampHeight > 0f && art.background
                    ? stampHeight / art.background.rect.height
                    : art.pixelScale * stampSize / 104f;
            SetLayer(background, art.background, scale);
            SetLayer(fill, art.fill, scale);
            SetLayer(icon, art.icon, scale);
            icon.rectTransform.anchoredPosition = (iconOnly ? art.hudIconOffset : art.iconOffset) * scale;
            background.enabled = !iconOnly;
            fill.enabled = !iconOnly;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            SetProgress(progress);
        }
    }

    private static void SetLayer(Image image, Sprite sprite, float scale)
    {
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        if (sprite) rect.sizeDelta = sprite.rect.size * scale;
    }
}
