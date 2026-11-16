using TMPro;
using UnityEngine;

/// <summary>Shows the player's health on the HUD as a bar and a number.</summary>
[ExecuteAlways]
public class PlayerHealthBar : MonoBehaviour
{
    public enum HealthDisplayStyle { Bar, Heart }
    [SerializeField] private PlayerHealth health;
    [SerializeField] private HealthDisplayStyle displayStyle = HealthDisplayStyle.Bar;
    [SerializeField] private bool showHealthNumber;
    [SerializeField] private GameObject barVisual;
    [SerializeField] private GameObject heartVisual;
    [SerializeField] private UnityEngine.UI.Image barFillImage;
    [SerializeField] private UnityEngine.UI.Image heartFillImage;
    [SerializeField] private RectTransform fillBar;
    [SerializeField] private TMP_Text healthText;


    private HealthDisplayStyle appliedStyle;
    private bool appliedNumber;

    private void OnEnable()
    {
        if (Application.isPlaying && health) health.OnHealthChanged += Show;
        Refresh();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && health) health.OnHealthChanged -= Show;
    }

    private void Start() { Refresh(); }



private void Update()
    {
        // Transform edits and Undo must not force a visibility refresh.
        if (appliedStyle != displayStyle || appliedNumber != showHealthNumber)
            Refresh();
    }

    public void SetDisplayStyle(HealthDisplayStyle style)
    {
        displayStyle = style;
        Refresh();
    }

private void Refresh()
    {
        float maximum = health ? health.MaxHealth : 100f;
        Show(Application.isPlaying && health ? health.CurrentHealth : maximum, maximum);
    }

    private void Show(float current, float max)
    {
        float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (barVisual) barVisual.SetActive(displayStyle == HealthDisplayStyle.Bar);
        if (heartVisual) heartVisual.SetActive(displayStyle == HealthDisplayStyle.Heart);
        if (barFillImage) barFillImage.fillAmount = fraction;
        else if (fillBar) fillBar.anchorMax = new Vector2(fraction, 1f);
        if (heartFillImage) heartFillImage.fillAmount = fraction;
        if (healthText)
        {
            healthText.text = Mathf.CeilToInt(Mathf.Max(0f, current)).ToString();
            healthText.gameObject.SetActive(showHealthNumber);
        }
        appliedStyle = displayStyle;
        appliedNumber = showHealthNumber;
    }
}
