using TMPro;
using UnityEngine;

/// <summary>Shows the player's health on the HUD as a bar and a number.</summary>
public class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private RectTransform fillBar;
    [SerializeField] private TMP_Text healthText;

    private void OnEnable()
    {
        health.OnHealthChanged += Show;
    }

    private void OnDisable()
    {
        health.OnHealthChanged -= Show;
    }

    private void Start()
    {
        Show(health.CurrentHealth, health.MaxHealth);
    }

    private void Show(float current, float max)
    {
        fillBar.anchorMax = new Vector2(Mathf.Clamp01(current / max), 1f);
        healthText.text = Mathf.CeilToInt(current).ToString();
    }
}
