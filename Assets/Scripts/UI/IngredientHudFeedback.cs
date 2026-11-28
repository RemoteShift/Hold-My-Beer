using TMPro;
using UnityEngine;

/// <summary>Recipe selection, shortage shake, and pixel-stepped spending feedback.</summary>
[DisallowMultipleComponent]
public sealed class IngredientHudFeedback : MonoBehaviour
{
    [System.Serializable]
    public sealed class Item
    {
        public RectTransform Motion;
        public UnityEngine.UI.Image Icon;
        public UnityEngine.UI.Image Flash;
        public TMP_Text Count;
        public UnityEngine.UI.LayoutElement Layout;
        [System.NonSerialized] public float Blend, ShakeLeft, SpendLeft;
        [System.NonSerialized] public int PreviousCount;
        [System.NonSerialized] public float PreviewWidth, PreviewCountWidth;
        [System.NonSerialized] public bool LayoutCaptured;
    }

    [SerializeField] private CocktailBrewer brewer;
    [SerializeField] private IngredientInventory inventory;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private RectTransform panel;
    [SerializeField] private Item[] items;
    private float clock;
    private bool initialized;

    public void Configure(CocktailBrewer source, IngredientInventory stock, PlayerHealth playerHealth,
        RectTransform ingredientPanel, Item[] targets)
    {
        brewer = source;
        inventory = stock;
        health = playerHealth;
        panel = ingredientPanel;
        items = targets;
    }

    private void OnEnable()
    {
        if (brewer) brewer.OnCraftFailed += CraftFailed;
        if (inventory) inventory.OnChanged += InventoryChanged;
        if (items == null || !inventory) return;
        for (int i = 0; i < items.Length; i++)
        {
            items[i].PreviousCount = inventory.GetCount((IngredientType)i);
            var item = items[i];
            if (!item.LayoutCaptured && item.Layout && item.Count)
            {
                item.PreviewWidth = Mathf.Max(item.Layout.minWidth, item.Layout.preferredWidth);
                item.PreviewCountWidth = item.Count.rectTransform.rect.width;
                item.LayoutCaptured = true;
            }
        }
        initialized = true;
    }

    private void InventoryChanged()
    {
        if (!initialized) return;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            int count = inventory.GetCount((IngredientType)i);
            if (count < item.PreviousCount) item.SpendLeft = .3f;
            item.PreviousCount = count;
        }
    }

    private void CraftFailed(CocktailRecipe recipe)
    {
        if (!initialized || recipe == null) return;
        for (int i = 0; i < items.Length; i++)
            if (recipe.GetCost((IngredientType)i) > inventory.GetCount((IngredientType)i))
                items[i].ShakeLeft = .36f;
    }

    private void LateUpdate() { Animate(Time.unscaledDeltaTime); }

    private void Animate(float dt)
    {
        if (!initialized || items == null) return;
        clock = (clock + dt) % 600f;
        float pixelTime = Mathf.Floor(clock * 12f) / 12f;
        bool selecting = brewer && brewer.isActiveAndEnabled && brewer.IsCrafting &&
            Time.timeScale > 0f && (!health || !health.IsDead);
        var recipe = selecting ? brewer.SelectedRecipe : null;
        bool changedLayout = false;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (!item.Motion || !item.Icon || !item.Count || !item.Layout) continue;
            // Preserve the authored preview spacing, expanding only when a longer count needs it.
            float width = Mathf.Max(item.PreviewWidth,
                32f + Mathf.Max(26f, Mathf.Max(item.PreviewCountWidth, item.Count.preferredWidth)));
            if (Mathf.Abs(item.Layout.preferredWidth - width) > .01f)
            {
                item.Layout.preferredWidth = width;
                changedLayout = true;
            }
            item.Count.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 32f);
            bool required = recipe != null && recipe.GetCost((IngredientType)i) > 0;
            item.Blend = Mathf.MoveTowards(item.Blend, required ? 1f : 0f, dt / .12f);
            float bob = Mathf.Round(Mathf.Sin(pixelTime * 3.5f) * 2.5f);
            float y = Mathf.Round((10f + bob) * item.Blend);
            float x = 0f, angle = (i % 2 == 0 ? -2.5f : 2.5f) * item.Blend;
            if (item.ShakeLeft > 0f)
            {
                int frame = Mathf.FloorToInt((.36f - item.ShakeLeft) * 18f);
                float amplitude = frame < 2 ? 5f : frame < 4 ? 3f : 1f;
                x = (frame % 2 == 0 ? -1f : 1f) * amplitude;
                angle = x * .7f;
            }
            if (item.SpendLeft > 0f)
            {
                int frame = Mathf.FloorToInt((.3f - item.SpendLeft) * 16f);
                angle += frame == 0 ? -10f : frame == 1 ? 5f : frame == 2 ? -3f : 0f;
                if (item.Flash)
                    item.Flash.color = new Color(1f, 1f, 1f, frame == 0 ? .85f : frame == 1 ? .45f : frame == 2 ? .2f : 0f);
            }
            else if (item.Flash) item.Flash.color = new Color(1f, 1f, 1f, 0f);
            item.Motion.anchoredPosition = new Vector2(x, y);
            item.Motion.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, item.Blend);
            item.Motion.localRotation = Quaternion.Euler(0f, 0f, angle);
            item.ShakeLeft = Mathf.Max(0f, item.ShakeLeft - dt);
            item.SpendLeft = Mathf.Max(0f, item.SpendLeft - dt);
        }
        if (changedLayout && panel) UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(panel);
    }

    private void OnDisable()
    {
        if (brewer) brewer.OnCraftFailed -= CraftFailed;
        if (inventory) inventory.OnChanged -= InventoryChanged;
        if (items != null)
            foreach (var item in items)
            {
                item.Blend = item.ShakeLeft = item.SpendLeft = 0f;
                if (item.Motion)
                {
                    item.Motion.anchoredPosition = Vector2.zero;
                    item.Motion.localScale = Vector3.one;
                    item.Motion.localRotation = Quaternion.identity;
                }
                if (item.Flash) item.Flash.color = new Color(1f, 1f, 1f, 0f);
                if (item.LayoutCaptured && item.Layout && item.Count)
                {
                    item.Layout.preferredWidth = item.PreviewWidth;
                    item.Count.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, item.PreviewCountWidth);
                }
            }
        if (panel) UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(panel);
        initialized = false;
    }
}
