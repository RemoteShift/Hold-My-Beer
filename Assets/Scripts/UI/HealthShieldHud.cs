using UnityEngine;

/// <summary>Muted SGV outline that flickers shortly before protection expires.</summary>
[DisallowMultipleComponent]
public sealed class HealthShieldHud : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private UnityEngine.UI.Image[] outlines;
    [SerializeField, Min(0f)] private float warningSeconds = 1.5f;

    public void Configure(PlayerHealth source, UnityEngine.UI.Image[] targets)
    {
        health = source;
        outlines = targets;
    }

    private void LateUpdate()
    {
        float remaining = health ? health.InvulnerableSecondsLeft : 0f;
        bool visible = health && !health.IsDead && remaining > 0f;
        // Hard, pixel-style flicker follows game time and therefore freezes while paused.
        if (visible && remaining <= warningSeconds)
            visible = Mathf.FloorToInt((warningSeconds - remaining) * 6f) % 2 == 0;
        if (outlines == null) return;
        foreach (var outline in outlines)
            if (outline)
            {
                outline.fillAmount = 1f;
                outline.enabled = visible;
            }
    }

    private void OnDisable()
    {
        if (outlines == null) return;
        foreach (var outline in outlines)
            if (outline) { outline.enabled = false; outline.fillAmount = 1f; }
    }
}
