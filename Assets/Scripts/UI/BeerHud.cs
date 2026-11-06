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
            var fill = i < glasses.Count ? glasses[i] : 0f;
            glassFills[i].anchorMax = new Vector2(1f, fill);
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
