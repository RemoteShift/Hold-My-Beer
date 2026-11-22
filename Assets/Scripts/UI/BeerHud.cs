using TMPro;
using UnityEngine;

/// <summary>Shows beers in hand and available interaction prompts.</summary>
public class BeerHud : MonoBehaviour
{
    [SerializeField] private BeerInventory inventory;
    [SerializeField] private BeerHandler handler;
    [SerializeField] private PushHandler pushHandler;

    [Tooltip("Full-size liquid sprites, one per glass. Cropped vertically in whole sprite rows.")]
    [SerializeField] private RectTransform[] glassFills;
    [Tooltip("Optional foam artwork, visible only for a completely full glass.")]
    [SerializeField] private GameObject[] glassFoams;
    [Tooltip("Optional liquid surfaces, parented to the top of each fill.")]
    [SerializeField] private GameObject[] glassSurfaces;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text feedbackText;

    private CocktailBrewer brewer;

    private void OnEnable()
    {
        if (!pushHandler && handler) pushHandler = handler.GetComponent<PushHandler>();
        inventory.OnChanged += RefreshGlasses;
        PromptAppearance.Apply(promptText);
        ConfigurePrompt();

        if (feedbackText) { feedbackText.text = string.Empty; feedbackText.gameObject.SetActive(false); }

    }

    private void ConfigurePrompt()
    {
        if (!promptText) return;
        var rect = promptText.rectTransform;
        rect.sizeDelta = new Vector2(1100f, 120f);
        rect.anchoredPosition = new Vector2(0f, -40f);
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.textWrappingMode = TextWrappingModes.Normal;
        promptText.lineSpacing = 4f;
    }

    private void OnDisable()
    {
        inventory.OnChanged -= RefreshGlasses;


    }

    private void Start()
    {
        if (feedbackText) feedbackText.text = string.Empty;
        RefreshGlasses();
    }

    private void Update()
    {
        if (!brewer && handler) brewer = handler.GetComponent<CocktailBrewer>();
        var beerPrompt = FormatActionPrompt(handler.Prompt, handler.PromptAction);
        var pushPrompt = pushHandler
            ? FormatActionPrompt(pushHandler.Prompt, InputHandler.PromptAction.Push)
            : string.Empty;
        promptText.text = brewer && brewer.OwnsCombatInput ? string.Empty : CombinePrompts(beerPrompt, pushPrompt);



    }

    // Inventory serves its last entry first; display it in the leftmost remaining slot.
    private void RefreshGlasses()
    {
        var glasses = inventory.Glasses;
        for (var i = 0; i < glassFills.Length; i++)
        {
            int inventoryIndex = glassFills.Length - 1 - i;
            SetGlassLevel(i, Mathf.Clamp01(inventoryIndex < glasses.Count ? glasses[inventoryIndex] : 0f));
        }
    }

    /// <summary>Crops full-size sprites at native pixel rows instead of scaling their artwork.</summary>
    private void SetGlassLevel(int index, float fill)
    {
        fill = Mathf.Clamp01(fill);
        var liquid = glassFills[index];
        if (!liquid) return;

        var liquidImage = liquid.GetComponent<UnityEngine.UI.Image>();
        var rows = liquidImage && liquidImage.sprite
            ? Mathf.Max(1, Mathf.RoundToInt(liquidImage.sprite.rect.height))
            : 1;
        // A non-empty glass keeps at least one visible row; a full glass shows every row.
        var visibleRows = fill > 0f ? Mathf.Clamp(Mathf.CeilToInt(fill * rows), 1, rows) : 0;
        var visibleFraction = visibleRows / (float)rows;

        liquid.anchorMax = Vector2.one;
        liquid.gameObject.SetActive(fill > 0f);
        if (liquidImage)
        {
            liquidImage.type = UnityEngine.UI.Image.Type.Filled;
            liquidImage.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            liquidImage.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
            liquidImage.fillAmount = visibleFraction;
        }

        if (glassFoams != null && index < glassFoams.Length && glassFoams[index])
            glassFoams[index].SetActive(fill >= 1f);

        if (glassSurfaces == null || index >= glassSurfaces.Length || !glassSurfaces[index]) return;
        var surfaceObject = glassSurfaces[index];
        surfaceObject.SetActive(fill > 0f && fill < 1f);
        var surface = surfaceObject.transform as RectTransform;
        if (!surface) return;

        // The surface shares the liquid's pixel size and follows the same snapped row.
        var surfaceImage = surface.GetComponent<UnityEngine.UI.Image>();
        var surfaceRows = surfaceImage && surfaceImage.sprite ? surfaceImage.sprite.rect.height : 1f;
        var surfaceHeight = liquid.rect.height * surfaceRows / rows;
        surface.anchorMin = new Vector2(0.5f, 0f);
        surface.anchorMax = surface.anchorMin;
        surface.pivot = new Vector2(0.5f, 1f);
        surface.sizeDelta = new Vector2(liquid.rect.width, surfaceHeight);
        surface.anchoredPosition = new Vector2(0f, liquid.rect.height * visibleFraction);
        surface.localScale = Vector3.one;
        if (surfaceImage)
        {
            // At the final few rows, crop the underside rather than squashing the surface.
            surfaceImage.type = UnityEngine.UI.Image.Type.Filled;
            surfaceImage.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            surfaceImage.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Top;
            surfaceImage.fillAmount = surfaceHeight > 0f
                ? Mathf.Clamp01(liquid.rect.height * visibleFraction / surfaceHeight)
                : 0f;
        }
    }


    private string FormatActionPrompt(string prompt, InputHandler.PromptAction action)
    {
        if (string.IsNullOrEmpty(prompt) || action == InputHandler.PromptAction.None) return prompt;

        return $"{InputPromptText.Control(promptText.spriteAsset, action)} {prompt}";
    }

    private static string CombinePrompts(string first, string second)
    {
        if (string.IsNullOrEmpty(first)) return second;
        if (string.IsNullOrEmpty(second)) return first;

        return $"{first}\n{second}";
    }



}
