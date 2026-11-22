using TMPro;
using UnityEngine;

/// <summary>Drives the saved cocktail and ingredient UI on the same canvas as health and beer.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CocktailHud : MonoBehaviour
{
    private static readonly Color Paper = new Color(.94f, .88f, .76f);
    private static readonly Color Muted = new Color(.72f, .69f, .62f);
    private static readonly Color Error = new Color(1f, .42f, .34f);

    [Header("Scene UI")]
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform ingredientPanel;
    [SerializeField] private RectTransform slot;
    [SerializeField] private RectTransform details;
    [SerializeField] private CocktailIcon cocktailIcon;
    [SerializeField] private CocktailStampView cocktailStamp;
    [SerializeField] private TMP_Text cocktailName, detailName, prompt, errorMark, controlHint;
    [SerializeField] private TMP_Text[] quantities;
    [SerializeField] private CostIcon[] costs;
    [Header("Edit mode preview")]
    [SerializeField] private CocktailKind previewRecipe = CocktailKind.HoneyBrew;
    [SerializeField] private bool previewCraftingDetails;

    private IngredientInventory inventory;
    private BeerInventory beers;
    private CocktailBrewer brewer;
    private PlayerHealth health;
    private CocktailRecipe displayedRecipe, failedRecipe;
    private float errorUntil;
    private string errorMessage;

    [System.Serializable]
    private sealed class CostIcon
    {
        public GameObject Root;
        public CocktailIcon Icon;
        public TMP_Text Amount;
        [System.NonSerialized] public IngredientType Type;
        [System.NonSerialized] public int Required;
        [System.NonSerialized] public bool IsBeer;
    }

    public bool IsSelectionEmpty => !slot || !slot.gameObject.activeSelf;
    public bool IsShowingFailure => errorMark && errorMark.gameObject.activeInHierarchy;
    public bool IsCraftingVisible => details && details.gameObject.activeInHierarchy;
    public RectTransform SelectedSlot => slot;
    public RectTransform IngredientPanel => ingredientPanel;

    private void OnEnable()
    {
        inventory = GetComponent<IngredientInventory>();
        beers = GetComponent<BeerInventory>();
        health = GetComponent<PlayerHealth>();
        brewer = GetComponent<CocktailBrewer>();
        displayedRecipe = null;
        if (Application.isPlaying && brewer) brewer.OnFeedback += ShowFeedback;
        RefreshUI();
    }

    private void OnDisable()
    {
        if (brewer) brewer.OnFeedback -= ShowFeedback;
        if (Application.isPlaying && root) root.gameObject.SetActive(false);
    }

    private void OnValidate() { displayedRecipe = null; }
    private void Update() { RefreshUI(); }

    private void RefreshUI()
    {
        if (!root || !slot || !inventory || !brewer || quantities == null) return;
        root.gameObject.SetActive(!Application.isPlaying || !health || !health.IsDead);
        for (int i = 0; i < quantities.Length; i++)
            if (quantities[i]) quantities[i].SetText("x{0}", inventory.GetCount((IngredientType)i));

        // The cocktail display is artwork only. Ingredient inventory remains separate.
        if (cocktailIcon) cocktailIcon.gameObject.SetActive(false);
        if (cocktailName) cocktailName.gameObject.SetActive(false);
        if (controlHint) controlHint.gameObject.SetActive(false);
        if (details) details.gameObject.SetActive(false);
        var recipe = brewer.SelectedRecipe;
        if (!Application.isPlaying)
            foreach (var candidate in CocktailRecipes.All)
                if (candidate.Id == previewRecipe) { recipe = candidate; break; }
        slot.gameObject.SetActive(recipe != null);
        if (recipe == null || !cocktailStamp) return;
        displayedRecipe = recipe;
        cocktailStamp.SetKind(recipe.Id);
        float progress = 1f;
        if (Application.isPlaying && recipe.Duration > 0f)
        {
            if (recipe.Id == CocktailKind.AllYouCanDrink && beers && beers.HasInfiniteBeer)
                progress = beers.InfiniteBeerSecondsLeft / recipe.Duration;
            else if (recipe.Id == CocktailKind.SteelGutVodka && health && health.InvulnerableSecondsLeft > 0f)
                progress = health.InvulnerableSecondsLeft / recipe.Duration;
        }
        cocktailStamp.SetProgress(progress);
    }

    private void ShowFeedback(string message, bool error)
    {
        if (!error) { errorUntil = 0f; return; }
        failedRecipe = brewer.SelectedRecipe;
        errorMessage = message.StartsWith("Need ") || message.StartsWith("Not enough")
            ? "Not enough ingredients" : message;
        errorUntil = Time.unscaledTime + 1.6f;
    }

    private void RefreshCosts(CocktailRecipe recipe)
    {
        foreach (var item in costs) if (item.Root) item.Root.SetActive(false);
        int index = 0;
        for (int i = 0; i < IngredientInfo.Count; i++)
        {
            var type = (IngredientType)i;
            int amount = recipe.GetCost(type);
            if (amount > 0) SetCost(index++, (CocktailGlyph)i, IngredientInfo.Color(type), amount, type, false);
        }
        if (recipe.BeerCost > 0)
            SetCost(index, CocktailGlyph.Beer, new Color(1f, .78f, .3f), recipe.BeerCost, default, true);
    }

    private void SetCost(int index, CocktailGlyph glyph, Color tint, int amount, IngredientType type, bool isBeer)
    {
        if (index >= costs.Length) return;
        var item = costs[index];
        item.Root.SetActive(true);
        item.Icon.SetGlyph(glyph, tint);
        item.Amount.text = "x" + amount;
        item.Type = type;
        item.Required = amount;
        item.IsBeer = isBeer;
    }
}
