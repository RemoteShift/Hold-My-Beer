using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Shows the beers in hand, what the player can do right now, and how their last press went.</summary>
public class BeerHud : MonoBehaviour
{
    [SerializeField] private BeerInventory inventory;
    [SerializeField] private BeerHandler handler;

    [Tooltip("Fill bars, one per glass. Their anchors are moved to show how full each glass is.")]
    [SerializeField] private RectTransform[] glassFills;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackSeconds = 0.9f;

    private readonly List<float> sortedGlasses = new List<float>();
    private float feedbackHideTime;

    private void OnEnable()
    {
        inventory.OnChanged += RefreshGlasses;
        handler.OnFeedback += ShowFeedback;
    }

    private void OnDisable()
    {
        inventory.OnChanged -= RefreshGlasses;
        handler.OnFeedback -= ShowFeedback;
    }

    private void Start()
    {
        feedbackText.text = string.Empty;
        RefreshGlasses();
    }

    private void Update()
    {
        promptText.text = handler.Prompt;

        if (Time.time >= feedbackHideTime)
        {
            feedbackText.text = string.Empty;
        }
    }

    // Fullest first, so the left-most glass is always the next one handed over
    private void RefreshGlasses()
    {
        sortedGlasses.Clear();
        sortedGlasses.AddRange(inventory.Glasses);
        sortedGlasses.Sort((a, b) => b.CompareTo(a));

        for (var i = 0; i < glassFills.Length; i++)
        {
            var fill = i < sortedGlasses.Count ? sortedGlasses[i] : 0f;
            glassFills[i].anchorMax = new Vector2(1f, fill);
        }
    }

    private void ShowFeedback(string message)
    {
        feedbackText.text = message;
        feedbackHideTime = Time.time + feedbackSeconds;
    }
}
