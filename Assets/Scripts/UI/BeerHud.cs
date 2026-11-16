using TMPro;
using UnityEngine;

/// <summary>Shows the beers in hand, what the player can do right now, and how their last press went.</summary>
public class BeerHud : MonoBehaviour
{
    [SerializeField] private BeerInventory inventory;
    [SerializeField] private BeerHandler handler;
    [SerializeField] private PushHandler pushHandler;

    [Tooltip("Fill bars, one per glass. Their anchors are moved to show how full each glass is.")]
    [SerializeField] private RectTransform[] glassFills;
    [Tooltip("Optional foam artwork, visible only for a completely full glass.")]
    [SerializeField] private GameObject[] glassFoams;
    [Tooltip("Optional liquid surfaces, parented to the top of each fill.")]
    [SerializeField] private GameObject[] glassSurfaces;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackSeconds = 0.9f;

    private float feedbackHideTime;

    private void OnEnable()
    {
        if (!pushHandler && handler) pushHandler = handler.GetComponent<PushHandler>();
        inventory.OnChanged += RefreshGlasses;
        handler.OnFeedback += ShowFeedback;
        if (pushHandler) pushHandler.OnFeedback += ShowFeedback;
    }

    private void OnDisable()
    {
        inventory.OnChanged -= RefreshGlasses;
        handler.OnFeedback -= ShowFeedback;
        if (pushHandler) pushHandler.OnFeedback -= ShowFeedback;
    }

    private void Start()
    {
        feedbackText.text = string.Empty;
        RefreshGlasses();
    }

    private void Update()
    {
        promptText.text = CombinePrompts(handler.Prompt, pushHandler ? pushHandler.Prompt : string.Empty);

        if (Time.time >= feedbackHideTime)
        {
            feedbackText.text = string.Empty;
        }
    }

    // Same order as the inventory, so the rightmost filled glass is always the next one handed over
private void RefreshGlasses()
    {
        var glasses = inventory.Glasses;
        for (var i = 0; i < glassFills.Length; i++)
        {
            var fill = Mathf.Clamp01(i < glasses.Count ? glasses[i] : 0f);
            if (glassFills[i])
            {
                glassFills[i].anchorMax = new Vector2(1f, fill);
                glassFills[i].gameObject.SetActive(fill > 0f);
            }
            if (glassFoams != null && i < glassFoams.Length && glassFoams[i])
                glassFoams[i].SetActive(fill >= 1f);
            if (glassSurfaces != null && i < glassSurfaces.Length && glassSurfaces[i])
            {
                glassSurfaces[i].SetActive(fill > 0f && fill < 1f);
                // Keep the surface inside the mug even when only a few drops remain.
                var surface = glassSurfaces[i].transform as RectTransform;
                if (surface && glassFills[i])
                    surface.localScale = new Vector3(1f, Mathf.Clamp01(glassFills[i].rect.height / Mathf.Max(0.001f, surface.sizeDelta.y)), 1f);
            }
        }
    }

    private static string CombinePrompts(string first, string second)
    {
        if (string.IsNullOrEmpty(first)) return second;
        if (string.IsNullOrEmpty(second)) return first;

        return $"{first}     {second}";
    }

    private void ShowFeedback(string message)
    {
        feedbackText.text = message;
        feedbackHideTime = Time.time + feedbackSeconds;
    }
}
