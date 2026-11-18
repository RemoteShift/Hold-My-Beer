using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A top-left ingredient icon row and a cocktail window shown while a number key is held.</summary>
[DisallowMultipleComponent]
public class CocktailHud : MonoBehaviour
{
    private static readonly Color Ink = new Color(.06f, .055f, .045f, .75f);
    private static readonly Color Paper = new Color(.94f, .88f, .76f);
    private static readonly Color Muted = new Color(.68f, .65f, .58f);
    private static readonly Color Error = new Color(1f, .35f, .29f);
    private IngredientInventory inventory;
    private CocktailBrewer brewer;
    private PlayerHealth health;
    private RectTransform root;
    private GameObject content;
    private CocktailIcon cocktailIcon;
    private TMP_Text cocktailName, prompt, errorMark;
    private TMP_Text[] quantities = new TMP_Text[IngredientInfo.Count];
    private CostIcon[] costs = new CostIcon[IngredientInfo.Count + 1];
    private int displayedHotkey;
    private int failedHotkey;
    private float errorUntil;
    private string errorMessage;

    [System.Serializable]
    private sealed class CostIcon
    {
        public GameObject Root;
        public CocktailIcon Icon;
        public TMP_Text Amount;
    }

    public bool IsSelectionEmpty => !content || !content.activeSelf;
    public bool IsShowingFailure => errorMark && errorMark.gameObject.activeInHierarchy;

    private void Awake()
    {
        inventory = GetComponent<IngredientInventory>();
        health = GetComponent<PlayerHealth>();
    }
    private void Start() { Build(); ConnectFeedback(); }
    private void OnEnable()
    {
        ConnectFeedback();
        if (root) root.gameObject.SetActive(true);
    }
    private void OnDisable()
    {
        if (brewer) brewer.OnFeedback -= ShowFeedback;
        if (root) root.gameObject.SetActive(false);
    }
    private void ConnectFeedback()
    {
        if (brewer) brewer.OnFeedback -= ShowFeedback;
        brewer = GetComponent<CocktailBrewer>();
        if (brewer) brewer.OnFeedback += ShowFeedback;
    }
    private void OnDestroy()
    {
        if (brewer) brewer.OnFeedback -= ShowFeedback;
        if (root) Destroy(root.gameObject);
    }

    private void Update()
    {
        if (!root || !brewer) return;
        // Rebuild only when a live script reload changed the shape of the generated UI.
        if (!content || quantities == null || quantities.Length != IngredientInfo.Count || !quantities[0])
        {
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
            Build();
        }
        root.gameObject.SetActive(!health || !health.IsDead);
        for (var i = 0; i < quantities.Length; i++) quantities[i].SetText("{0}", inventory.GetCount((IngredientType)i));

        var recipe = brewer.SelectedRecipe;
        content.SetActive(recipe != null);
        if (recipe == null)
        {
            displayedHotkey = 0;
            errorUntil = 0f;
            return;
        }
        if (displayedHotkey != recipe.Hotkey)
        {
            displayedHotkey = recipe.Hotkey;
            cocktailIcon.SetRecipe(recipe);
            cocktailName.text = recipe.Hotkey + "  " + recipe.Name;
            RefreshCosts(recipe);
        }
        var failed = recipe.Hotkey == failedHotkey && Time.unscaledTime < errorUntil;
        errorMark.gameObject.SetActive(failed);
        prompt.color = failed ? Error : Muted;
        prompt.text = failed ? errorMessage : recipe.IsThrowable ? "LMB to brew & throw" : "LMB to brew & drink";
    }

    private void ShowFeedback(string message, bool error)
    {
        if (!error) { errorUntil = 0f; return; }
        failedHotkey = brewer.SelectedRecipe?.Hotkey ?? 0;
        errorMessage = message.StartsWith("Need ") || message.StartsWith("Not enough") ? "Not enough ingredients" : message;
        errorUntil = Time.unscaledTime + 1.6f;
    }

    private void RefreshCosts(CocktailRecipe recipe)
    {
        foreach (var item in costs) item.Root.SetActive(false);
        var slot = 0;
        for (var i = 0; i < IngredientInfo.Count; i++)
        {
            var type = (IngredientType)i;
            var amount = recipe.GetCost(type);
            if (amount <= 0) continue;
            SetCost(slot++, (CocktailGlyph)i, IngredientInfo.Color(type), amount);
        }
        if (recipe.BeerCost > 0) SetCost(slot, CocktailGlyph.Beer, new Color(1f,.78f,.3f), recipe.BeerCost);
    }
    private void SetCost(int slot, CocktailGlyph glyph, Color tint, int amount)
    {
        var item = costs[slot];
        item.Root.SetActive(true);
        item.Icon.SetGlyph(glyph, tint);
        item.Amount.text = "x" + amount;
    }

    private void Build()
    {
        quantities = new TMP_Text[IngredientInfo.Count];
        costs = new CostIcon[IngredientInfo.Count + 1];
        displayedHotkey = 0;
        root = new GameObject("Cocktail HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = .5f;

        var stack = Rect("Inventory and cocktail", root, 24, 0, 232, 280);
        stack.anchorMin = stack.anchorMax = new Vector2(0, .5f);
        stack.pivot = new Vector2(0, .5f);
        var inventoryPanel = Rect("Ingredient inventory", root, 24, 24, 336, 48);
        Fill(inventoryPanel, Ink);
        for (var i = 0; i < quantities.Length; i++)
        {
            var type = (IngredientType)i;
            var x = 12 + i * 82;
            Icon(IngredientInfo.Name(type) + " icon", inventoryPanel, x, 12, 24, 24)
                .SetGlyph((CocktailGlyph)i, IngredientInfo.Color(type));
            var quantity = Label("0", inventoryPanel, x + 30, 11, 36, 26, 18, Paper);
            quantity.alignment = TextAlignmentOptions.Right;
            quantity.enableAutoSizing = true;
            quantity.fontSizeMin = 11;
            quantity.fontSizeMax = 18;
            quantity.textWrappingMode = TextWrappingModes.NoWrap;
            quantities[i] = quantity;
        }
        var window = Rect("Selected cocktail", stack, 0, 170, 232, 108);
        Fill(window, Ink);
        var contentRect = Rect("Cocktail content", window, 0, 0, 232, 108);
        content = window.gameObject;
        cocktailIcon = Icon("Cocktail logo", contentRect, 10, 12, 34, 34);
        cocktailName = Label("", contentRect, 54, 9, 150, 40, 17, Paper);
        cocktailName.fontStyle = FontStyles.Bold;
        cocktailName.enableAutoSizing = true;
        cocktailName.fontSizeMin = 13;
        cocktailName.fontSizeMax = 17;
        for (var i = 0; i < costs.Length; i++)
        {
            var item = Rect("Recipe ingredient " + i, contentRect, 12 + i * 63, 57, 60, 23);
            costs[i] = new CostIcon
            {
                Root = item.gameObject,
                Icon = Icon("Ingredient logo", item, 0, 0, 22, 22),
                Amount = Label("", item, 28, 0, 30, 22, 15, Paper)
            };
            item.gameObject.SetActive(false);
        }
        prompt = Label("", contentRect, 12, 87, 211, 16, 11, Muted);
        errorMark = Label("X", contentRect, 207, 7, 20, 29, 25, Error);
        errorMark.fontStyle = FontStyles.Bold;
        errorMark.gameObject.SetActive(false);
        content.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static void Fill(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
    private static CocktailIcon Icon(string name, Transform parent, float x, float y, float width, float height)
    {
        var icon = Rect(name, parent, x, y, width, height).gameObject.AddComponent<CocktailIcon>();
        icon.raycastTarget = false;
        return icon;
    }
    private static TMP_Text Label(string text, Transform parent, float x, float y, float width, float height, float size, Color color)
    {
        var label = Rect(text.Length > 0 ? text : "Label", parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }
}
