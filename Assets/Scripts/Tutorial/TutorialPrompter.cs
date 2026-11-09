using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Puts tutorial instructions on screen. A freezing prompt slows the game to a stop and holds it
/// there until the player does what it says, then brings the game back up to speed.
/// The player can still look around while frozen, but nothing else moves.
/// </summary>
public class TutorialPrompter : MonoBehaviour
{
    [SerializeField] private GameObject banner;
    [SerializeField] private TMP_Text bannerText;
    [Tooltip("A smaller line for tips that can show alongside the main prompt.")]
    [SerializeField] private GameObject hint;
    [SerializeField] private TMP_Text hintText;
    [Tooltip("Real seconds to slow from full speed to a stop.")]
    [SerializeField, Min(0f)] private float slowDownSeconds = 0.5f;
    [Tooltip("Real seconds to get back to full speed once the player acts.")]
    [SerializeField, Min(0f)] private float speedUpSeconds = 0.2f;

    // Keeps physics steps short while slowed, so motion stays smooth instead of stepping
    private const float MinPhysicsStepScale = 0.05f;

    private float normalFixedDeltaTime;

    public bool IsFrozen { get; private set; }

    private void Awake()
    {
        normalFixedDeltaTime = Time.fixedDeltaTime;
        Hide();
        HideHint();
    }

    // Never leave the game stuck at a stop if this goes away mid-prompt
    private void OnDisable()
    {
        CancelFreeze();
    }

    /// <summary>Puts the game straight back to full speed, for when a freezing prompt is cut short.</summary>
    public void CancelFreeze()
    {
        if (IsFrozen) SetTimeScale(1f);
        IsFrozen = false;
    }

    public void Show(string text)
    {
        if (bannerText) bannerText.text = text;
        if (banner) banner.SetActive(true);
    }

    public void Hide()
    {
        if (banner) banner.SetActive(false);
    }

    public void ShowHint(string text)
    {
        if (hintText) hintText.text = text;
        if (hint) hint.SetActive(true);
    }

    public void HideHint()
    {
        if (hint) hint.SetActive(false);
    }

    /// <summary>Shows the prompt while the game keeps running, until <paramref name="done"/> returns true.</summary>
    public IEnumerator Prompt(string text, Func<bool> done)
    {
        Show(text);
        while (!done()) yield return null;
        Hide();
    }

    /// <summary>Slows the game to a stop with the prompt up, until <paramref name="done"/> returns true.</summary>
    public IEnumerator FreezePrompt(string text, Func<bool> done)
    {
        Show(text);
        IsFrozen = true;

        for (var t = 0f; t < slowDownSeconds && !done(); t += Time.unscaledDeltaTime)
        {
            SetTimeScale(1f - t / slowDownSeconds);
            yield return null;
        }

        if (!done()) SetTimeScale(0f);
        while (!done()) yield return null;

        Hide();

        var from = Time.timeScale;
        for (var t = 0f; t < speedUpSeconds; t += Time.unscaledDeltaTime)
        {
            SetTimeScale(Mathf.Lerp(from, 1f, t / speedUpSeconds));
            yield return null;
        }

        SetTimeScale(1f);
        IsFrozen = false;
    }

    private void SetTimeScale(float scale)
    {
        Time.timeScale = Mathf.Clamp01(scale);
        Time.fixedDeltaTime = normalFixedDeltaTime * Mathf.Max(Time.timeScale, MinPhysicsStepScale);
    }
}
