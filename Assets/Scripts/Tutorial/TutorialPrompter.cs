using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Self-paced explanations followed by short, persistent practice objectives.</summary>
public class TutorialPrompter : MonoBehaviour
{
    [SerializeField] private GameObject banner;
    [SerializeField] private TMP_Text bannerText;
    [SerializeField] private GameObject hint;
    [SerializeField] private TMP_Text hintText;
    [Header("Practice pacing")]
    [Tooltip("Game speed while moving into reach during a beer/unbeer exercise.")]
    [SerializeField, Range(0.1f, 1f)] private float positioningTimeScale = 0.35f;
    [Tooltip("Real seconds to return to normal speed after an exercise.")]
    [SerializeField, Min(0f)] private float speedUpSeconds = 0.4f;

    private const float MinPhysicsStepScale = 0.05f;
    private float normalFixedDeltaTime;
    private float savedTimeScale;
    private bool savedActionsBlocked;
    private string lesson = "TUTORIAL";
    private float hiddenAt;
    public bool IsFrozen { get; private set; }
    public bool IsReading { get; private set; }
    public bool IsShowing => banner && banner.activeSelf;
    public float QuietSeconds => IsShowing ? 0f : Time.realtimeSinceStartup - hiddenAt;

    private void Awake()
    {
        normalFixedDeltaTime = Time.fixedDeltaTime;
        ConfigureBanner();
        Hide();
        HideHint();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        CancelFreeze();
        Hide();
        HideHint();
    }

    private void ConfigureBanner()
    {
        if (!bannerText || !banner) return;
        var scaler = banner.GetComponent<CanvasScaler>();
        if (scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        // Keep the middle of the screen clear for aiming and the ingredient row unobstructed.
        var panel = bannerText.transform.parent as RectTransform;
        if (panel && panel != banner.transform)
        {
            panel.anchorMin = new Vector2(0.20f, 0f);
            panel.anchorMax = new Vector2(0.84f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 32f);
            panel.sizeDelta = new Vector2(0f, 208f);
            var image = panel.GetComponent<Image>();
            if (image) { image.color = new Color(.035f, .03f, .025f, .94f); image.raycastTarget = false; }
        }
        var rect = bannerText.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(28f, 18f);
        rect.offsetMax = new Vector2(-28f, -18f);
        bannerText.color = new Color(.96f, .93f, .86f);
        bannerText.fontSize = 26f;
        bannerText.enableAutoSizing = true;
        bannerText.fontSizeMin = 22f;
        bannerText.fontSizeMax = 26f;
        bannerText.alignment = TextAlignmentOptions.MidlineLeft;
        bannerText.textWrappingMode = TextWrappingModes.Normal;
        bannerText.overflowMode = TextOverflowModes.Overflow;
        bannerText.richText = true;
        bannerText.raycastTarget = false;
    }

    public void SetLesson(string title) => lesson = title;

    private void BeginTimeControl()
    {
        if (IsFrozen) return;
        savedTimeScale = Time.timeScale;
        var input = InputHandler.Instance;
        savedActionsBlocked = input && input.ActionsBlocked;
        IsFrozen = true;
    }

    public void CancelFreeze()
    {
        if (IsFrozen)
        {
            SetTimeScale(savedTimeScale);
            SetActionsBlocked(savedActionsBlocked);
        }
        IsReading = false;
        IsFrozen = false;
    }

    public void Show(string text) => ShowCard(text, "");

    private void ShowCard(string text, string footer)
    {
        HideHint();
        if (bannerText)
        {
            var body = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+)\]", "<b><color=#FFD58A>[$1]</color></b>");
            bannerText.text = "<size=75%><color=#FFD58A>" + lesson + "</color></size>\n" + body +
                (string.IsNullOrEmpty(footer) ? "" : "\n<size=75%><color=#C8C1B5>" + footer + "</color></size>");
        }
        if (banner) banner.SetActive(true);
    }

    public void Hide()
    {
        if (IsShowing) hiddenAt = Time.realtimeSinceStartup;
        if (banner) banner.SetActive(false);
    }
    public void ShowHint(string text)
    {
        if (IsShowing) return;
        if (hintText) hintText.text = text;
        if (hint) hint.SetActive(true);
    }
    public void HideHint() { if (hint) hint.SetActive(false); }

    /// <summary>No timeout. A fresh Enter press advances; combat clicks cannot dismiss the card.</summary>
    public IEnumerator Explain(string text)
    {
        BeginTimeControl();
        IsReading = true;
        SetTimeScale(0f);
        SetActionsBlocked(true);
        ShowCard(text, "PAUSED  •  Press [Enter] when ready");
        // Ignore a held key and the press that opened this card.
        yield return null;
        while (ContinueHeld()) yield return null;
        while (!ContinuePressed()) yield return null;
        // Do not hand the confirmation frame to the next gameplay prompt.
        yield return null;
        Hide();
        CancelFreeze();
    }

    private static bool ContinueHeld() => Keyboard.current?.enterKey.isPressed == true ||
        Keyboard.current?.numpadEnterKey.isPressed == true;
    private static bool ContinuePressed() => Keyboard.current?.enterKey.wasPressedThisFrame == true ||
        Keyboard.current?.numpadEnterKey.wasPressedThisFrame == true;

    public IEnumerator Prompt(string text, Func<bool> done)
    {
        if (done()) yield break;
        ShowCard(text, "PRACTICE  •  Complete the action to continue");
        while (!done()) yield return null;
        Hide();
    }

    public IEnumerator FreezePrompt(string text, Func<bool> done, Func<bool> canAct = null)
    {
        if (done()) yield break;
        BeginTimeControl();
        var previousReady = (bool?)null;
        while (!done())
        {
            var ready = canAct == null || canAct();
            // Reach and occlusion still come from PlayerAim. Outside reach, allow repositioning.
            SetTimeScale(ready ? 0f : positioningTimeScale);
            if (previousReady != ready)
            {
                ShowCard(text, ready ? "IN REACH  •  Time is paused — try the action" :
                    "MOVE CLOSER & AIM AT HIM  •  Time is slowed");
                previousReady = ready;
            }
            yield return null;
        }
        Hide();
        var from = Time.timeScale;
        for (var t = 0f; t < speedUpSeconds; t += Time.unscaledDeltaTime)
        {
            SetTimeScale(Mathf.Lerp(from, savedTimeScale, t / speedUpSeconds));
            yield return null;
        }
        CancelFreeze();
    }

    private static void SetActionsBlocked(bool blocked)
    {
        var input = InputHandler.Instance;
        if (input) input.ActionsBlocked = blocked;
    }
    private void SetTimeScale(float scale)
    {
        Time.timeScale = Mathf.Clamp01(scale);
        Time.fixedDeltaTime = normalFixedDeltaTime * Mathf.Max(Time.timeScale, MinPhysicsStepScale);
    }
}
